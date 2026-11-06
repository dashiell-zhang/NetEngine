# 数据库日志

`Infrastructure/Logger.Database` 通过 `AddDatabaseLogger` 接入三个宿主，采用“调用现场快照 → 有界内存缓冲 → 本地文件 → 数据库”的写入链路

## 业务线程与缓冲

- 业务线程捕获消息、时间、Scope 和异常内容，只尝试立即入队，不等待队列空位、磁盘或数据库
- 机器名在每个记录器创建时获取并复用，运行期间沿用该值；标准日志级别使用固定字符串，未定义级别仍按枚举原有规则转换
- EF 日志保持原有 `StackTrace(true)`、筛选带源码信息的帧及选取 `relevantFrames[1]` 的行为；这部分 CPU 和内存分配仍发生在业务线程
- 日志 ID 在后台首次尝试写出该条日志时生成，重试保持同一 ID；`CreateTime` 仍取调用现场时间
- 所有级别先使用共用队列；共用队列容量或字节预算耗尽后，只有 `Warning`、`Error`、`Critical` 可以使用预留队列
- 预留队列优先消费，跨队列不保证严格时间顺序；重要日志也无法在所有缓冲耗尽时保证接收
- 缓冲同时限制队列条数与估算字节数，正在落盘或重试的批次继续占用字节预算；队列外最多另有一个批次
- 字节预算按字符串 UTF-16 长度和每条固定开销估算，不代表进程总内存上限，序列化临时对象和调用现场快照也会分配内存
- 超过预算的单条日志直接计入舍弃数量，不截断原文；不创建无限队列或为每条日志启动后台任务

## 配置

写入与查询分别注册。写入前按 [ID 生成器](IdentifierGenerator.md) 注册宿主共享 ID 服务，并通过 [数据库工具](RepositoryTool.md) 准备日志模型与表。日志分区维护使用明确的共享锁；写入注册会加载目标分区快照，数据库不可访问时启动失败：

```csharp
using DistributedLock.Redis;
using Logger.Database;

builder.Services.AddRedisLock("NetEngine.Log", options =>
{
    options.Configuration = sharedLogRedisConnection;
    options.InstanceName = "NetEngine.Log.Maintenance";
});
builder.Logging.AddDatabaseLogger(logWriteConnection, "Logs.Production", options =>
{
    options.QueueCapacity = 8192;
    options.MaxBufferedBytes = 48 * 1024 * 1024;
    options.ImportantQueueCapacity = 2048;
    options.ImportantMaxBufferedBytes = 16 * 1024 * 1024;
    options.BatchSize = 1000;
    options.FlushInterval = TimeSpan.FromSeconds(1);
}, partitionLockKey: "NetEngine.Log");
```

三个模板宿主在注册回调中通过 `builder.Configuration.GetSection("DatabaseLogger").Bind(options)` 绑定配置；组件自身不自动读取配置节。上面直接赋值的示例列出缓冲默认值。省略 `partitionLockKey` 时解析默认 `IDistributedLock`；共享日志库应显式选择共同维护锁，完整关系见 [业务模块接入](DatabaseModules.md#redis-与共享资源)

容量、字节预算、批量大小必须为正数，间隔必须介于 1 毫秒与 4294967294 毫秒之间；启动时校验。配置在服务创建时固定，修改后需要重启

`Project` 保持默认入口程序集名，`SaveDays` 默认 14，只允许正数或 -1，错误值会阻止宿主启动；设为 -1 时不执行数据库日志清理。清理仅针对本 Project 中未软删除的过期记录，遵循模型的软删除过滤器；同 Project 副本须使用相同保留策略，不通过删除分区绕过 Project 范围。极大保留天数以最早日期为删除边界，避免日期计算溢出。清理失败通过 cleanup 指标及限频诊断报告，正常停机取消不算失败。容量应结合日志大小、峰值持续时间和进程内存预算调整

## 本地落盘与恢复

后台达到 `BatchSize` 或 `FlushInterval` 时写出批次，目录为进程工作目录下的 `Logs/Database/<目标哈希>`。目标由稳定数据库标识、Project、主机、端口、库名、账号和搜索路径相关配置确定；密码不参与目录标识，密码轮换不改变积压位置。先写 `batch-*.log.tmp`，关闭文件后重命名为 `batch-*.log`，入库任务只消费后者

入库扫描、临时文件恢复和积压统计统一按完整文件名识别，只扫描本目标哈希目录的顶层：

- 正常批次：`batch-yyyyMMddHHmmssfff-<32 位无连字符 GUID>.log`，时间戳按固定文化的 UTC 公历生成并校验有效日期
- 恢复批次：`batch-recovered-<32 位无连字符 GUID>.log`
- 临时文件：仅正常批次名称追加 `.tmp`，恢复批次不产生临时文件
- 异常保留文件：正常或恢复批次名称追加 `.rejected`，只计入异常文件统计，不再次入库

固定前缀和后缀区分大小写，GUID 接受大小写十六进制字符。其他名称、无效日期或 GUID、额外后缀、LocalFile 小时文件及子目录内容均不处理、不计入积压。上述名称属于本组件保留范围，不要用于其他文件；文件名校验不能识别刻意使用完整合法名称的外部文件

单个后台消费者跨批次复用 UTF-8 缓冲和 JSON 写入器，每条日志完整序列化后再写入文件，减少中间 JSON 字符串分配；仍使用公共 JSON 转换规则、无 BOM 编码和逐行格式。每次文件写入尝试成功、失败或取消后均重置缓冲，容量超过 64 KiB 时换回初始 4 KiB 缓冲，避免偶发大日志长期占用内存；这不是单条日志长度限制

临时文件的 FileStream 写入缓冲按批次数据量选择：固定及继承字段、字符串转义和换行的保守估算不超过 16 KiB 时使用 16 KiB，否则使用 64 KiB。大批次先根据字符串长度快速判断，只有可能较小的批次才扫描字符估算，不新增配置，也不改变临时文件发布或失败恢复方式

落盘失败时保留当前批次，按 1、2、4、8、16、30 秒退避重试，之后维持 30 秒。重试产生的部分临时文件不删除，超过 5 分钟且可独占打开时恢复为待入库文件。同一条日志在重试文件中使用相同 ID，入库时去重

正常停机先关闭队列接收，再使用宿主剩余停机时间尽量排空到文件，无需等待数据库。期限耗尽时取消后台处理；剩余内存条目计入舍弃数量，其中已经部分写入临时文件的记录可能在下一次启动恢复

## 后台入库

- 按 UTF-8 字节逐行读取并使用公共 JSON 转换规则解析，支持文件开头的 UTF-8 BOM、CR/LF/CRLF 和末行无换行；不自动识别其他编码
- 普通行直接从字节解析，跨缓冲区长行复用连续字节缓冲；保留空白行跳过及无效 UTF-8 字节的替换字符处理
- 每个文件使用独立作用域和数据库上下文，避免一轮积累大量跟踪实体
- 按 ID 查询已有记录时包括软删除数据，同一文件中的重复 ID 也只提交一次
- 数据库已有 ID 和文件内新增 ID 共用去重集合；日志显式标记为新增后，仅在当前上下文保存期间关闭自动变更检测，并在成功、异常或取消后恢复原值
- 有积压时持续消费，空闲或失败后等待 5 秒，数据库异常时保留原文件
- 有效记录提交成功后才删除文件；取消或读取失败时不删除原文件
- 对于含损坏行的文件，先保存其中有效记录，再将整个原文件改名为 `.log.rejected`，保留原文供人工核对；异常文件不自动清理
- 数据库提交成功但文件删除失败时，下轮通过去重再次处理

## 独立查询

```csharp
services.AddDatabaseLogQuery(logWriteConnection, logReadConnection);
```

查询使用组件的 LogQueryService 和 ReadLogDatabaseContext。未配置日志读连接时只回退对应日志写库，不回退业务读库。只注册查询不会加载写入分区快照，也不要求 ID、文件消费者、清理或维护任务。管理端 LogManageService 由 Admin.WebAPI 显式注册，负责前端 DTO 转换

调用方注入 `Logger.Database.Query.LogQueryService`，通过 `QueryAsync(LogQueryRequest, CancellationToken)` 获取总数和分页列表。`Skip` 不小于 0，`Take` 为 1～100；可按 Project、机器名、内容、级别和时间过滤。不传 Project 时可查询整个日志库，Project 查询使用包含匹配；它是检索条件，访问权限由调用方控制

一个宿主只允许一个日志写入目标。更换目标后不会自动消费旧目录的积压；应在切换前排空，或恢复原目标配置处理旧积压。主机别名和搜索路径改变也可能产生新目录，切换时检查未处理文件

日志记录遵循宿主的 Logging 级别与过滤配置。日志自己的 EF 诊断及日志分区维护诊断不回写同一数据库日志链路，业务数据库诊断仍按宿主配置记录。写入连接不加入业务环境事务，日志不作为业务提交成功的证明

## 指标与故障报告

类库通过 `System.Diagnostics.Metrics` 暴露名为 `Logger.Database` 的 Meter，无需额外监控依赖。接入现有指标采集器后可读取：

| 指标 | 含义 |
|---|---|
| `logger.database.dropped` | 按 `level` 分组的累计未接收或停机未完成落盘条数 |
| `logger.database.failures` | 按 `operation` 分组的累计失败次数 |
| `logger.database.buffered` | 队列及当前落盘批次持有的日志条数 |
| `logger.database.buffered.bytes` | 当前缓冲的估算字节数 |
| `logger.database.spool.files` | 待入库文件及临时文件数量 |
| `logger.database.spool.bytes` | 待入库文件及临时文件总字节数 |
| `logger.database.rejected.files` | 需要人工检查的异常文件数量 |

计数在进程重启后归零。业务线程只更新原子计数，指标由采集端拉取；磁盘积压在后台维护轮次刷新，最短间隔 30 秒，长时间入库可能延迟刷新。失败阶段包括 `spool`、`database`、`recovery`、`shutdown`、`quarantine` 和 `cleanup`，并按阶段每 30 秒最多向标准错误输出一次简要诊断，不通过本日志 Provider 回写

应关注舍弃数量增长、落盘失败、磁盘积压持续增长和异常文件出现，同时监控日志目录所在磁盘的剩余容量

## 可靠性边界

这是以业务响应为先的尽力持久化日志。正常运行、短时高峰、数据库暂时不可用及正常停机时尽量保留日志，但不保证绝不丢失：

- ID 服务租约失效会阻止新日志首次落盘；记录在有界缓冲内等待，容量或停机期限耗尽可能丢弃。已有 ID 的积压文件仍可恢复
- 内存缓冲全部耗尽、单条日志超过预算或队列已关闭时会舍弃日志
- 强制结束进程会丢失尚未落盘的内存日志
- 文件关闭与重命名不等于抗断电持久化，本实现没有逐批强制物理刷盘
- 磁盘持续不可写时有限缓冲最终会耗尽；不会为了日志无限占用内存或阻塞业务等待存储

每个进程应使用独立工作目录和日志目录，不支持多个进程同时消费同一个目录。日志使用独立上下文，连接池由 Npgsql 提供程序管理；与业务共用物理库时仍会竞争数据库资源，后台处理不能消除数据库、磁盘或 CPU 资源竞争；需要绝对可靠的审计事件应使用单独的业务持久化机制

## 相关文档

- [WebAPI 公共能力](WebAPI.md)
- [数据库读写分离](DatabaseReadWriteSeparation.md)
