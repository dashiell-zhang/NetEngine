# TaskService 任务开发

TaskService 是独立的 Worker Service 宿主，用于执行周期性定时任务和持久化队列任务

- 宿主与具体任务：`Presentation/TaskService`
- 调度、队列和任务初始化能力：`ProjectCore/TaskService.Core`
- 任务配置与队列记录：`Infrastructure/QueueTask.Database/Models`，映射到所属模块的业务库
- 管理接口：`Presentation/Admin.WebAPI/Controllers/OperationsController.cs`

TaskService 同时接入独立的 [进程广播事件](EventBus.md)，让各实例分别处理本地状态变化通知并支持短暂断线补收。该能力通过 EventSubscribe 方法监听，与下面竞争领取的 QueueTask 独立，不受 Debug 控制台的任务启用选择限制

## 开发前准备

TaskService 至少需要正确配置 PostgreSQL 和 Redis：

```text
Presentation/TaskService/appsettings.json
Presentation/TaskService/appsettings.Development.json
```

- `dbConnection`：读写数据库连接
- `dbReadConnection`：只读连接；为空时回退到 `dbConnection`；宿主构建读取连接时会自动追加 `Options=-c default_transaction_read_only=on`
- `redisConnection`：队列任务跨实例并发锁和其他 Redis 能力

日志、事件和共享 ID 可配置独立连接，默认目标及跨模块共享关系见 [业务模块接入](DatabaseModules.md#redis-与共享资源)。QueueTask 与 TaskSetting 始终随所属业务模型迁移

本地依赖、数据库迁移和初始化方式见根目录 [README](../README.md#快速开始)

读写上下文的选择、只读账号要求和多读库配置见 [数据库读写分离](DatabaseReadWriteSeparation.md)

新增具体任务时，在 `Presentation/TaskService/Tasks` 中创建 `public` 类并继承 `TaskBase`。宿主通过 `AddTaskWorker<DatabaseContext>(taskTypes)` 显式选择任务类型并注册为 Scoped 服务，初始化只扫描所选类型，并发控制使用宿主默认 `IDistributedLock`。当前模板选择入口程序集中的公开、非抽象 TaskBase 子类；将任务移到独立类库时，需要调整宿主传入的类型集合，加载类库本身不会扩大消费者范围。一个 Worker 进程只允许选择一个任务库，重复选择会报错：

```csharp
public class ProductTask(ProductService productService) : TaskBase
{
}
```

任务类可以通过构造函数注入应用服务和所需的任务框架能力。业务逻辑与业务编排仍放在 Application 层，任务方法主要负责参数传递和触发应用用例

TaskService 不读取当前 HTTP 用户上下文。任务代表某个用户继续执行时，应由 WebAPI 入队端从可信认证上下文取得 `actorUserId`，写入任务参数并继续传给 Application Service；真正的系统任务调用允许系统身份的方法时传入 `null`，不要使用 `0` 伪造用户身份

## 定时任务

### 声明无参数任务

```csharp
[ScheduleTask(Name = "Product.Sync", Cron = "0 0/5 * * * ?", SkipIfRunning = true)]
public async Task SyncAsync()
{
    await productService.SyncAsync();
}
```

| 参数 | 说明 |
|---|---|
| `Name` | 本业务模块内唯一的定时任务名称，建议使用 `领域.动作` |
| `Cron` | 包含秒的 Cron 表达式 |
| `SkipIfRunning` | 上一次执行未结束时是否跳过本次触发 |

不要使用 `async void`，异步任务使用 `Task`

`SkipIfRunning` 只防止同一个 TaskService 进程内的任务重入。部署多个 TaskService 实例时，每个实例都会独立触发定时任务。分布式锁限制并发，但不消除释放锁后另一副本的重复调用；需要同一触发批次只产生一次业务结果时，应按批次标识实现持久化幂等，或只部署一个负责该任务的实例

### Cron 格式

Cron 使用 6 个必填字段和 1 个可选年份字段：

```text
秒 分 时 日 月 星期 [年]
```

例如：

```text
0/10 * * * * ?    每 10 秒
0 0/5 * * * ?     每 5 分钟
0 0 2 * * ?       每天 02:00
```

“日”和“星期”不能同时指定具体规则，其中一个应使用 `?`。触发时间按 TaskService 所在机器的本地时区计算，因此部署时应确认服务器时区

### 声明带参数任务

任务方法可以声明一个参数：

```csharp
[ScheduleTask(Name = "Product.SyncByTenant", Cron = "0 0 * * * ?", SkipIfRunning = true)]
public Task SyncByTenantAsync(TenantTaskParameter parameter)
{
    return productService.SyncAsync(parameter.TenantId);
}
```

带参数任务不会仅凭特性创建一个可执行实例。配置同步服务运行时会在 `TaskSetting` 中维护一条 `Parameter = "__args_default__"` 的模板记录，实际执行实例需要通过后台管理功能或 `POST /Operations/CreateScheduleTask` 新建。创建接口要求该任务名称已存在于配置表中，同步服务的启动条件见下文“非 Debug 模式”

每条实例记录需要：

- `Name` 与特性中的任务名称一致
- `Parameter` 是可以反序列化为方法参数类型的 JSON
- 可选地覆盖 `Cron`
- `IsEnable = true`

实际运行时名称为 `<Name>:<TaskSetting.Id>`，因此同一个带参数方法可以有多份独立配置

非 Debug 模式下，声明带参方法即可启动配置同步与调度等待。新建并启用的实例会动态加入执行，并继承特性中的 `SkipIfRunning`。模板保留参数标识不可编辑；无参定义不能通过修改参数变成带参实例

## 队列任务

### 声明消费者

```csharp
[QueueTask(Name = "Message.SendEmail", Semaphore = 4)]
public Task SendEmailAsync(SendEmailDto parameter)
{
    return messageService.SendEmailAsync(parameter);
}
```

| 参数 | 说明 |
|---|---|
| `Name` | 本业务模块内唯一的队列名称，生产者入队时必须完全一致 |
| `Semaphore` | 该任务在所有 TaskService 实例上的允许并发数，默认 `1`，必须大于 `0` |

任务方法应当无参数或只有一个可 JSON 序列化的参数，返回类型使用 `void`、`Task` 或 `Task<T>`，不要使用 `async void`

### 从业务代码入队

业务宿主先注册本库上下文及其工厂、共享 `IdService`，再通过 `AddQueueTaskDatabase<DatabaseContext>()` 注册本库能力，Application 注入 `QueueTaskProducer<DatabaseContext>`。上下文需包含任务模型及映射，完整接入见 [模块上下文](DatabaseModules.md#模块上下文)。该注册不启动 Worker，组合宿主可为不同具体上下文分别注册：

```csharp
long taskId = await queueTaskService.CreateSingleAsync(
    "Message.SendEmail",
    sendEmailDto,
    planTime: null,
    callbackName: null,
    callbackParameter: null);

```

`CreateSingleAsync` 在抑制外层环境事务的范围内创建、保存并释放本模块新上下文，返回任务 ID；调用方回滚不会撤销这条任务

参数校验、序列化和保存失败统一抛出异常，由应用边界处理，不吞异常返回 false

需要让业务数据和队列记录在同一事务提交时，使用 `Create`：

```csharp
await using var transaction = await db.Database.BeginTransactionAsync();

// 修改业务数据
queueTaskService.Create("Message.SendEmail", sendEmailDto);

await db.SaveChangesAsync();
await transaction.CommitAsync();
```

`Create` 要求当前 `DatabaseContext` 已经开启显式事务，否则会抛出异常

`planTime` 使用 `DateTimeOffset`，为空表示尽快执行；业务层的入队服务不接受早于当前时间的计划时间

### 回调任务

创建任务时可以指定另一个队列任务作为回调：

```csharp
long taskId = await queueTaskService.CreateSingleAsync(
    "Product.Rebuild",
    productId,
    callbackName: "Product.RebuildCompleted",
    callbackParameter: null);

```

当主任务成功且没有未完成子任务时，系统写入回调队列记录。如果未显式提供 `callbackParameter`，并且主任务有返回值，系统会把返回值序列化后作为回调参数

回调名称也必须对应一个 `[QueueTask]` 方法，并且该任务已经启用

### 在任务内部创建子任务

TaskService 内部的任务类使用 `TaskService.Core.QueueTask.QueueTaskService`。它包装选定模块的生产者，并提供 `isChild` 参数：

```csharp
await queueTaskService.CreateSingleAsync(
    "Product.BuildPart",
    partId,
    callbackName: null,
    callbackParameter: null,
    isChild: true);
```

`isChild: true` 会把新任务关联到当前正在执行的队列任务。父任务的回调会等待其子任务全部成功后再创建。该参数只能在队列任务执行上下文中使用，否则因缺少 `CurrentTaskId` 而抛出异常

业务服务使用按具体上下文绑定的生产者，任务执行范围使用 Worker 包装服务，二者复用相同的校验和失败契约。事务入队必须和业务使用同一个 Scoped 写上下文，仅类型或连接相同不足以保证原子提交

父任务、子任务和回调始终位于本库。完成状态与回调入队在数据库事务中提交，父任务自身与全部子树成功后才生成回调记录；重复完成不重复入队回调。这不保证任务的外部业务副作用恰好执行一次。向另一模块发起工作时调用其契约，不传递本库父任务身份

## 启用任务

仅添加特性并不代表任务会立即运行。非 Debug 模式由 `TaskSetting.IsEnable` 控制，Debug 模式由控制台选择临时启用

### Debug 模式

Debug 启动后，控制台会列出发现的队列任务和定时任务。输入序号并回车即可在当前进程中启用，再次输入可继续启用其他任务

Debug 模式不运行每分钟的数据库配置同步，因此控制台选择是本次调试进程的主要启用方式

### 非 Debug 模式

后台服务在初始化完成后开始同步 `TaskSetting`，每轮同步结束后等待 60 秒再进行下一轮：

- 新发现的任务会写入配置表，默认不启用
- 队列任务可从数据库覆盖 `Semaphore`
- 定时任务可从数据库覆盖 `Cron`
- `IsEnable` 决定任务是否执行

可以通过管理后台的任务配置功能，或 Admin.WebAPI 的 `Operations` 接口查看和修改配置。循环正常运行时，修改会在后续同步中生效；同步耗时或数据库异常会延后生效时间，60 秒不是严格上限

`Semaphore` 保存为 `null` 时恢复任务特性声明的并发值；Cron 保存为 `null`、空串或空白时统一使用任务特性声明的默认表达式，包括带参实例。非空 Cron 在保存前按调度使用的同一语法校验，无效配置不会写入数据库

运行中遇到无效或无法继续计算触发时间的配置时，只暂停当前进程中的该任务并报告错误，其他任务继续调度；后续配置同步会重新应用启用状态，修正配置后即可恢复。Debug 下由控制台重新启用

配置同步与动态实例的启动规则：

- 配置同步服务在存在队列、无参定时或带参定时定义时运行；多副本并发补充定义由数据库唯一约束协调，不覆盖管理端已有配置
- 仅有带参定义而暂无实例时，调度服务继续等待后续新增实例，不需要重启

### PostgreSQL 分区自动维护

PostgreSQL 分区维护由 Repository.Core 的显式后台服务负责，不属于 TaskService 的队列任务或定时任务，也不写入 `TaskSetting`

宿主通过 `AddPartitionMaintenance<TContext>(databaseIdentity, lockServiceKey)` 明确注册该服务，启动完成前检查一次，此后每 10 分钟检查。`Client.WebAPI` 和 `Admin.WebAPI` 也会承载同一服务，多宿主和多实例由分布式锁协调。完整行为见 [PostgreSQL 分区表](PostgreSqlPartitionTable.md)

## 队列执行和失败处理

- 执行器每秒扫描可执行记录
- 创建未满 1 秒的记录暂不领取
- `Semaphore` 同时限制当前实例和通过 Redis 锁协调的跨实例并发
- 任务通过数据库 Worker 标识和 5 分钟租约领取，执行期间自动续期数据库租约和分布式锁
- 单条队列任务自动执行最多 3 次，手动重试可重置计数
- 异常后回到 `Pending` 的任务，按上次执行开始时间 `LastTime` 加 `5 × Count` 分钟判断重试资格：第一次开始后超过 5 分钟、第二次开始后超过 10 分钟；不是从失败时刻重新计时，长任务失败后可能在下一轮扫描就符合条件
- 实际领取还受计划时间、并发名额和扫描时机影响；已过期的 `Running` 租约按恢复分支处理，不等待上述退避间隔
- 第三次执行异常被执行器捕获并成功保存状态后，标记为 `Failed`
- 第三次执行期间进程退出或无法收尾，租约到期后由该任务的后续扫描标记为 `Failed`，不再自动执行第四次；任务类型需仍由启用的 Worker 扫描，关闭的任务可通过管理端人工处理
- 可通过管理后台或 `POST /Operations/RetryQueueTask` 手工重试未成功任务；有效执行租约内拒绝重置，避免与正在执行的任务竞争

队列消费可能因租约恢复、进程终止或外部故障发生再次执行，任务实现应尽量保持幂等。不要只依赖“正常情况下执行一次”来保证数据正确性

Redis 并发锁失效时，已经开始的业务仍继续执行，数据库租约在可以续期时继续保持；并发锁不再续期，其他实例可能获得新的名额。因此 `Semaphore` 不保证锁失效期间仍严格限制全局并发，业务一致性需要幂等或数据库约束保障

## 停机

宿主开始停止时，队列领取和定时调度同时停止，不等待其他后台服务先完成停机。已开始的任务在宿主停机期限内继续执行、释放作用域并完成队列状态及回调入库，队列在此期间保持续租

超过停机期限后，队列停止续租和新的收尾提交，未完成记录由后续 Worker 在租约到期后恢复或标记失败。框架不能强制终止已经进入的业务方法，也不保证撤销它已产生的副作用；需给宿主足够的正常收尾时间，业务仍需处理重复执行

## 新增任务检查清单

- 任务类是否位于 TaskService 宿主可加载范围、声明为 `public` 并继承 `TaskBase`
- `Name` 是否在本业务模块的同类任务定义中唯一，生产者和消费者是否完全一致
- 是否避免了 `async void` 和多个方法参数
- 参数是否可以从入队 JSON 或 `TaskSetting.Parameter` 正确反序列化
- 队列任务是否根据实际下游容量设置了 `Semaphore`
- 重复执行是否安全，涉及多步数据修改时是否使用事务
- 回调任务与子任务是否也已声明并启用
- TaskService 的 PostgreSQL 和 Redis 配置是否正确
- 代表用户执行的任务是否通过任务参数传递可信 `actorUserId`
- Debug 时是否在控制台启用，非 Debug 时是否在任务配置中启用

## 管理与模块绑定

`QueueTaskManagement<TContext>` 提供本模块队列列表、手工重试和 TaskSetting 管理；应用层负责 DTO 转换、鉴权和操作人身份传入。查询与状态判断使用主库，管理能力不启动消费和配置同步。各模块可以注册自己的管理服务，不通过运行时连接串或全局默认服务选择任务库

同一任务名称的队列配置、无参定义或带参模板只允许一条有效记录，实际带参实例可以多条。配置修改不允许改变定义与实例的身份；新增带参实例必须有对应模板。跨模块事务和 Redis 配置见 [业务模块接入](DatabaseModules.md)
