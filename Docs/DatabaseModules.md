# 业务模块与数据库接入

每个业务模块拥有自己的实体、写上下文、读上下文和数据库。宿主可以组合多个模块，但模块间只通过普通请求和结果契约交互，不传递 EF 实体、IQueryable、连接或事务

## 存储归属

| 模型 | 所属项目 | 表和迁移归属 |
|---|---|---|
| 业务模型 | 模块 Repository | 本模块业务表 |
| QueueTask、TaskSetting | `QueueTask.Database` | 接入模块的业务库和迁移 |
| Log | `Logger.Database` | `LogDatabaseContext` |
| EventBusStream、EventBusMessage | `EventBus.Database` | `EventBusDatabaseContext` |

`Infrastructure/Repository.Core` 提供 CD、CUD、基础上下文、PostgreSQL 分区保存适配、维护及连接检查。它不包含具体业务实体，当前只支持 EF Core 与 PostgreSQL。CD 是 CLR 公共基类，不要单独将它加入 EF 实体模型

日志和事件可以与业务共用物理数据库，也可部署到独立数据库，各自管理自己的表集合。读上下文复用本模块写模型，不建立自己的迁移集合。修改连接不会创建或搬迁表和数据

多个业务上下文映射到同库、同 schema、同名 QueueTask 和 TaskSetting 表时，会共享这套任务存储。独立业务使用不同任务名称，接受管理查询能看到双方任务，并由一个明确的迁移入口管理共享物理表。不同上下文不会因此自动共享事务，详见 [架构图中的任务存储关系](ArchitectureDiagrams.md#43-多个上下文的目标选择)

## 模块上下文

写上下文可以继承 `Repository.Core.Bases.RepositoryDbContext`，直接声明自己的 `DbSet<T>`，并在 `OnModelCreating` 中调用本项目生成的映射。生成器会识别引用程序集内的实体，例如 QueueTask 和 TaskSetting

接入任务能力时，需要声明 `DbSet<QueueTask.Database.Models.QueueTask>`、`DbSet<QueueTask.Database.Models.TaskSetting>`，并调用 `modelBuilder.ConfigureQueueTasks()`。保留生成的软删除映射。TaskSetting 保存操作人 ID，由应用解释其身份，不建立指向模块 User 表的公共导航

读上下文派生自对应写上下文，通过独立具体类型注册。不要让两个模块共用一个会动态换库的上下文类型

```csharp
using QueueTask.Database;
using Repository.Core.Extensions;
using Repository.Core.Partitioning;

// OrderDatabaseContext 和 OrderReadDatabaseContext 是模块自己的具体类型
var snapshot = PartitionTableSnapshot.Load(orderWriteConnection);
services.AddPostgreSqlContext<OrderDatabaseContext>(orderWriteConnection,
    snapshot: snapshot, migrationHistoryTable: "__EFMigrationsHistory_Order");
services.AddPostgreSqlContext<OrderReadDatabaseContext>(orderReadConnection,
    readOnly: true, snapshot: snapshot, migrationHistoryTable: "__EFMigrationsHistory_Order");
services.AddQueueTaskDatabase<OrderDatabaseContext>();
services.AddPartitionMaintenance<OrderDatabaseContext>("Orders.Production");
```

写入分区的上下文必须配置本目标的分区快照；加载失败会阻止启动。仅普通表的模块可不启用分区保存扩展。只读查询不需要为此探测主库；模板业务读上下文复用对应写库快照，日志查询入口不加载快照

`AddPostgreSqlContext<TContext>` 提供该类型的作用域上下文和池化工厂。业务应用与事务入队服务从同一作用域解析，才能共用同一个实例；后台和独立操作通过工厂创建并释放实例。每个具体类型只注册一个固定目标，交换不同模块的注册顺序不会改变绑定

每个上下文固定配置自己的连接字符串，数据源与连接池由 Npgsql 提供程序管理，不保证每个上下文类型独占一个连接池。读取连接追加默认只读事务且关闭默认跟踪。各连接池的 `Maximum Pool Size` 应按整个宿主的资源预算配置。只读账号和物理副本仍是数据库写权限的最终边界

## 本地模块契约

模块 A 只引用模块 B 的契约，由宿主注册 B 的本地实现，调用方通过依赖注入获取接口并正常调用。仅在需要适配已有服务时增加本地适配器，不为普通 C# 方法调用增加统一包装入口

是否新建 DI 作用域、开启事务或抑制环境事务，由具体业务用例决定并显式处理。框架不自动隔离跨模块调用，也不保证跨库原子提交；新建作用域本身不等于隔离环境事务。若 B 已独立提交，A 后续回滚不会撤销 B 的结果

需要业务提交后可靠推进的跨模块动作，可以在调用方业务事务内创建本地 QueueTask，再由对应 Worker 调用目标契约。重试、接收方幂等及必要的补偿由业务实现，QueueTask 不自动提供跨服务原子提交。远程部署时由宿主选择实际客户端，并按业务处理超时、取消和通信失败

## Redis 与共享资源

一个宿主集中注册一套默认业务缓存和锁，由注册配置决定 Redis 地址、库编号和前缀。业务服务、自动代理、HTTP 过滤器及任务 Worker 使用默认依赖；同进程不同业务通过具体 key 区分，不随数据库上下文切换 Redis。接入多个业务数据库不需要增加缓存或锁注册，参见 [分布式锁](DistributedLock.md) 与 [源码生成器](SourceGenerator.md)

| 用途 | 配置关系 |
|---|---|
| 业务锁、缓存和队列并发锁 | 同进程共用默认服务并通过 key 区分；不同宿主可配置不同 Redis 库编号，需要共享或互斥的宿主保持配置一致 |
| ID 节点分配与续期 | 使用独立共享配置，写入共同日志或事件库的服务必须共用节点范围 |
| 共享日志表维护 | 各服务竞争相同锁目标、库编号、前缀及数据库标识 |
| Redis EventBus | 需要互通的实例使用相同 Redis 目标、库编号和 Scope，不跟随业务锁隔离 |

三个模板宿主支持以下可选连接项；未配置时由宿主明确选择表中的默认目标，组件自身不读取业务配置节

| 配置项 | 用途 | 未配置时 |
|---|---|---|
| `dbConnection` | 当前业务主库 | 必填 |
| `dbReadConnection` | 当前业务读库 | 当前业务主库 |
| `logConnection` | 日志写库 | 当前业务主库 |
| `logReadConnection` | 管理端日志读库 | 日志写库 |
| `eventBusConnection` | 数据库广播主库 | 当前业务主库 |
| `redisConnection` | 宿主默认业务缓存与锁的 Redis | 必填 |
| `idRedisConnection` | ID 专用 Redis | 当前业务 Redis |
| `logRedisConnection` | 日志维护锁 Redis | ID Redis |
| `eventBusRedisConnection` | Redis 广播目标 | 当前业务 Redis |

除 `dbReadConnection`、`logReadConnection` 支持空白回退外，其他可选连接应省略配置项以使用默认目标，不要填写空字符串。环境变量使用 `ConnectionStrings__logConnection` 等双下划线形式，部署示例见 [部署配置生成器](Deployment.md#业务与共享组件连接)

拆分部署时要显式配置共享用途，不能依赖各宿主不同的默认业务 Redis。`DatabaseIdentity:Business` 和 `DatabaseIdentity:Log` 是稳定维护目标标识；同一物理维护目标使用相同值，值中不包含凭据。不同表的锁还包含实际 database、schema 和表名

## 工具与健康检查

数据库工具独立选择写模型，默认读取 Repository.Tool/appsettings.json，参见 [数据库工具](RepositoryTool.md)。模板三个模型使用 public 中不同的迁移历史表。迁移只在本机 Debug 下进行，保留当前 Debug 模型注释和外键约定

宿主显式注册 `DatabaseHealthCheck<TContext>` 并为每个目标命名。它只检查连接可访问性，不查询 User 表、不建表，也不证明模型、迁移或复制延迟正确。WebAPI.Core 提供健康检查管线，不默认注册任何业务数据库
