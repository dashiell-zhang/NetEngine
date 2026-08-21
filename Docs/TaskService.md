# TaskService 任务开发

TaskService 是独立的 Worker Service 宿主，用于执行周期性定时任务和持久化队列任务

- 宿主与具体任务：`Presentation/TaskService`
- 调度、队列和任务初始化能力：`ProjectCore/TaskService.Core`
- 任务配置与队列记录：`Repository/Database/TaskSetting.cs`、`Repository/Database/QueueTask.cs`
- 管理接口：`Presentation/Admin.WebAPI/Controllers/OperationsController.cs`

## 开发前准备

TaskService 至少需要正确配置 PostgreSQL 和 Redis：

```text
Presentation/TaskService/appsettings.json
Presentation/TaskService/appsettings.Development.json
```

- `dbConnection`：读写数据库连接
- `dbReadConnection`：只读连接；为空时回退到 `dbConnection`；宿主构建读取连接时会自动追加 `Options=-c default_transaction_read_only=on`
- `redisConnection`：队列任务跨实例并发锁和其他 Redis 能力

本地依赖、数据库迁移和初始化方式见根目录 [README](../README.md#快速开始)

读写上下文的选择、只读账号要求和多读库配置见 [数据库读写分离](DatabaseReadWriteSeparation.md)

新增具体任务时，在 `Presentation/TaskService/Tasks` 中创建 `public` 类并继承 `TaskBase`。宿主会把当前程序集中的公开任务类注册为 Scoped 服务，初始化服务再扫描任务方法：

```csharp
public class ProductTask(ProductService productService) : TaskBase
{
}
```

任务类可以通过构造函数注入应用服务和基础设施抽象。业务逻辑仍应放在 Application 层，任务方法主要负责触发和编排

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
| `Name` | 全局唯一任务名称，建议使用 `领域.动作` |
| `Cron` | 包含秒的 Cron 表达式 |
| `SkipIfRunning` | 上一次执行未结束时是否跳过本次触发 |

不要使用 `async void`，异步任务使用 `Task`

`SkipIfRunning` 只防止同一个 TaskService 进程内的任务重入。部署多个 TaskService 实例时，每个实例都会独立触发定时任务；需要全局只执行一次时，应在任务调用链中使用 `IDistributedLock`，或只部署一个负责该任务的实例

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

带参数任务不会仅凭特性创建一个可执行实例。系统会在 `TaskSetting` 中维护一条 `Parameter = "__args_default__"` 的模板记录，实际执行实例需要通过后台管理功能或 `POST /Operations/CreateScheduleTask` 新建

每条实例记录需要：

- `Name` 与特性中的任务名称一致
- `Parameter` 是可以反序列化为方法参数类型的 JSON
- 可选地覆盖 `Cron`
- `IsEnable = true`

实际运行时名称为 `<Name>:<TaskSetting.Id>`，因此同一个带参数方法可以有多份独立配置

运行期间新建的带参实例会由配置同步服务动态加入，但当前动态加入逻辑不会复制特性中的 `SkipIfRunning`。如果该实例依赖防重入，需要在创建后重启 TaskService，使初始化流程重新载入完整特性配置

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
| `Name` | 全局唯一队列名称，生产者入队时必须完全一致 |
| `Semaphore` | 该任务在所有 TaskService 实例上的允许并发数，默认 `1`，必须大于 `0` |

任务方法应当无参数或只有一个可 JSON 序列化的参数，返回类型使用 `void`、`Task` 或 `Task<T>`，不要使用 `async void`

### 从业务代码入队

API 或 Application 层使用 `Application.Service.TaskCenter.QueueTaskService`：

```csharp
await queueTaskService.CreateSingleAsync(
    "Message.SendEmail",
    sendEmailDto,
    planTime: null,
    callbackName: null,
    callbackParameter: null);
```

`CreateSingleAsync` 使用独立 DbContext 立即保存任务，适合不需要和当前业务事务保持一致的场景

Application 层的 `CreateSingleAsync` 以 `bool` 表示写入结果，调用方需要检查返回值；保存异常时返回 `false`

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
await queueTaskService.CreateSingleAsync(
    "Product.Rebuild",
    productId,
    callbackName: "Product.RebuildCompleted",
    callbackParameter: null);
```

当主任务成功且没有未完成子任务时，系统写入回调队列记录。如果未显式提供 `callbackParameter`，并且主任务有返回值，系统会把返回值序列化后作为回调参数

回调名称也必须对应一个 `[QueueTask]` 方法，并且该任务已经启用

### 在任务内部创建子任务

TaskService 内部的任务类使用 `TaskService.Core.QueueTask.QueueTaskService`。它比 Application 层同名服务多一个 `isChild` 参数：

```csharp
await queueTaskService.CreateSingleAsync(
    "Product.BuildPart",
    partId,
    callbackName: null,
    callbackParameter: null,
    isChild: true);
```

`isChild: true` 会把新任务关联到当前正在执行的队列任务。父任务的回调会等待其子任务全部成功后再创建。该参数只能在队列任务执行上下文中使用，否则因缺少 `CurrentTaskId` 而抛出异常

注意两个同名服务的命名空间：

- 普通业务代码：`Application.Service.TaskCenter.QueueTaskService`
- TaskService 任务内部及子任务：`TaskService.Core.QueueTask.QueueTaskService`

## 启用任务

仅添加特性并不代表任务会立即运行。任务的最终启用状态来自 `TaskSetting.IsEnable`

### Debug 模式

Debug 启动后，控制台会列出发现的队列任务和定时任务。输入序号并回车即可在当前进程中启用，再次输入可继续启用其他任务

Debug 模式不运行每分钟的数据库配置同步，因此控制台选择是本次调试进程的主要启用方式

### 非 Debug 模式

后台服务每 60 秒同步一次 `TaskSetting`：

- 新发现的任务会写入配置表，默认不启用
- 队列任务可从数据库覆盖 `Semaphore`
- 定时任务可从数据库覆盖 `Cron`
- `IsEnable` 决定任务是否执行

可以通过管理后台的任务配置功能，或 Admin.WebAPI 的 `Operations` 接口查看和修改配置。修改后最多等待一个同步周期生效

### PostgreSQL 分区自动维护

PostgreSQL 分区维护由 Repository 内置后台服务负责，不属于 TaskService 的队列任务或定时任务，也不写入 `TaskSetting`

TaskService 通过 `BatchRegisterBackgroundServices()` 自动承载该服务，启动完成前检查一次，此后每 10 分钟检查。`Client.WebAPI` 和 `Admin.WebAPI` 也会承载同一服务，多宿主和多实例由分布式锁协调。完整行为见 [PostgreSQL 分区表](PostgreSqlPartitionTable.md)

## 队列执行和失败处理

- 执行器每秒扫描可执行记录
- 创建未满 1 秒的记录暂不领取
- `Semaphore` 同时限制当前实例和通过 Redis 锁协调的跨实例并发
- 任务通过数据库 Worker 标识和 5 分钟租约领取，执行期间自动续期数据库租约和分布式锁
- 单条队列任务最多执行 3 次
- 第一次失败约 5 分钟后重试，第二次失败约 10 分钟后重试
- 第三次仍失败则标记为 `Failed`
- 可通过管理后台或 `POST /Operations/RetryQueueTask` 手动重试失败任务

队列消费可能因租约恢复、进程终止或外部故障发生再次执行，任务实现应尽量保持幂等。不要只依赖“正常情况下执行一次”来保证数据正确性

## 新增任务检查清单

- 任务类是否位于 TaskService 宿主可加载范围、声明为 `public` 并继承 `TaskBase`
- `Name` 是否全局唯一，生产者和消费者是否完全一致
- 是否避免了 `async void` 和多个方法参数
- 参数是否可以从入队 JSON 或 `TaskSetting.Parameter` 正确反序列化
- 队列任务是否根据实际下游容量设置了 `Semaphore`
- 重复执行是否安全，涉及多步数据修改时是否使用事务
- 回调任务与子任务是否也已声明并启用
- TaskService 的 PostgreSQL 和 Redis 配置是否正确
- 代表用户执行的任务是否通过任务参数传递可信 `actorUserId`
- Debug 时是否在控制台启用，非 Debug 时是否在任务配置中启用
