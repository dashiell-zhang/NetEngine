# PostgreSQL 分区表

本文说明 NetEngine 中基于 EF Core、Npgsql 和雪花 `Id` 的 PostgreSQL `RANGE` 分区表使用方式

## 功能概览

给新实体增加 `[PartitionTable]` 后，NetEngine 会完成以下工作：

1. 源码生成器把实体声明转换为 EF Core 模型 Annotation
2. EF Migration 创建 `PARTITION BY RANGE ("Id")` 父表
3. Migration 创建 SQL 生成时刻所在范围的一个初始子分区
4. Repository 后台服务保证当前范围和三个后续范围可写

声明、迁移和自动维护当前支持：

- PostgreSQL `RANGE` 分区
- 单列雪花 `long Id` 分区键
- 独立实体及同表 TPH 继承映射，分区声明放在 EF 根实体上
- 小时、天、自然月和自然年周期
- 固定 UTC+8 分区边界
- 新表随 EF Migration 直接创建为分区表
- `Interval` 和 `Unit` 向后续新分区生效
- 多宿主和多实例通过可续期分布式锁协调维护

声明、迁移和自动维护当前不支持：

- 自动把已有普通表转换为分区表
- `LIST`、`HASH`、表达式、多字段或多级分区
- 自动拆分、合并、删除或归档已有子分区
- 自动创建早于最老已有分区的历史范围
- `DEFAULT` 分区
- 分区实体的 TPT/TPC 多表继承，或仅在派生实体上声明分区

## DBA 管理的分区表与启动识别

xmin 保存适配与分区维护独立决定：三个服务端宿主在注册上下文池之前，使用写库连接执行一次只读目录查询，生成 `PartitionTableSnapshot`。实际 `pg_class.relkind = 'p'` 的表自动参与 xmin 适配，不要求实体具有 `[PartitionTable]`；只有显式分区声明才生成分区专用迁移元数据并进入自动维护。未声明实体仍遵循 EF 常规模型和迁移规则，自动识别不会将其排除出 Migration

| 实际表结构 | 实体声明 | 保存行为 | 分区维护 |
|---|---|---|---|
| 普通表 | 无 | Npgsql 原生返回 xmin | 不维护 |
| 分区表 | 无 | 插入后在同一事务批量回读 xmin | DBA 负责 |
| 分区表 | 有 | 插入后在同一事务批量回读 xmin | 按声明自动维护 |

启动快照按 schema 和表名匹配；未指定 schema 的映射根据写库启动连接的 `search_path` 和对象可见性解析，避免误匹配其他 schema 的同名分区表。快照与 EF 模型分离，不修改共享模型，也不把探测结果写入分区 Annotation

- 每个宿主实例在启动时查询一次写库，池化上下文、池化工厂和读上下文共用该只读快照，读库不单独探测；保存和定时维护不会刷新它。读写连接应对应同一套逻辑表结构，快照不参与查询路由
- 启动查询失败直接中止启动，不把未知结构当作普通表，也不回退到实体声明
- DBA 调整父表类型、表名、schema 或连接的 `search_path` 后，需要重启全部受影响的宿主实例；正常增删子分区不改变父表身份，不需要刷新快照
- 不支持运行期间通过 `SET search_path` 或更换数据库连接改变写入目标；新的写库配置应加载自己的快照，不能跨独立数据库共用
- DBA 表仍须保留当前模板的实体列映射、xmin 并发映射及预先确定的主键。自动识别不转换表结构、不处理迁移漂移、不扩展所有 EF 高级映射
- DBA 表缺少目标分区时，保存保留数据库原始错误，后台服务不会为其创建子分区

前面的 `RANGE`、雪花 ID 和周期限制属于框架声明与维护规则；自动识别只判断实际分区父表，不推断 DBA 的分区策略或维护周期

## 选择分区周期

分区周期由数量 `Interval` 和单位 `PartitionUnit` 共同决定

| 示例 | 含义 | 常见场景 |
|---|---|---|
| `[PartitionTable(1, PartitionUnit.Hour)]` | 每小时一个分区 | 写入量很高且需要细粒度维护 |
| `[PartitionTable(6, PartitionUnit.Hour)]` | 每 6 小时一个分区 | 小时间隔与对象数量之间的折中 |
| `[PartitionTable(1, PartitionUnit.Day)]` | 每天一个分区 | 通常建议优先采用的起点 |
| `[PartitionTable(7, PartitionUnit.Day)]` | 每 7 天一个分区 | 日写入量较低的表 |
| `[PartitionTable(1, PartitionUnit.Month)]` | 每个自然月一个分区 | 按月查询或归档的表 |
| `[PartitionTable(1, PartitionUnit.Year)]` | 每个自然年一个分区 | 写入量很低且长期保存的表 |

选择时需要平衡：

- 周期越小，单个分区越小，但数据库对象和后续维护次数越多
- 周期越大，分区数量越少，但单个分区数据量和索引体积更大
- Repository 后台服务启动后立即检查一次，随后每 10 分钟检查，因此首版允许的最小单位是小时

建议先根据预估写入量选择 `1 Day` 或 `7 Day`，上线后可以调整，新周期只影响尚未创建的后续分区

## 新表接入流程

### 1. 创建实体

实体通常继承 `CD`，直接使用现有雪花 `Id`：

```csharp
using Repository.Attributes;
using Repository.Bases;

namespace Repository.Database;

/// <summary>
/// 访问记录表
/// </summary>
[PartitionTable(1, PartitionUnit.Day)]
public class VisitLog : CD
{

    /// <summary>
    /// 请求路径
    /// </summary>
    public string Path { get; set; }

}
```

这个声明表示：

- PostgreSQL 父表使用 `PARTITION BY RANGE ("Id")`
- 每个新子分区覆盖 UTC+8 下的一个自然日
- 分区时间边界和名称统一使用固定 UTC+8
- `Id` 按 `SnowflakeIdLayout` 中的雪花纪元和位布局换算

### 2. 加入 DatabaseContext

实体必须作为直接 `DbSet<TEntity>` 加入 `DatabaseContext`，源码生成器才会为该上下文生成分区模型配置：

```csharp
public DbSet<VisitLog> VisitLog { get; set; }
```

不需要在 `OnModelCreating` 中手工调用 `PartitionModelBuilder.Configure`

使用 TPH（基类和派生类共用一张表）时，在 EF 继承根实体上声明 `[PartitionTable]`，并把该根实体作为直接 `DbSet` 加入上下文。派生类型按 EF 正常方式纳入模型，不重复声明分区；即使只新增派生类型，也会按根表执行 xmin 回填和事务保护。继承 `CD` 但未把 `CD` 映射为实体，不属于这里的 EF 实体继承层级

### 3. 构建检查

先构建 Repository，检查源码生成器诊断和生成代码是否可以编译：

```powershell
dotnet build Repository/Repository.csproj
```

编译成功不会执行 `DatabaseContext.OnModelCreating`。主键、唯一索引等 EF Core 模型约束需要在后续 EF 工具或宿主实际构建模型时校验；变更影响生成器或多个宿主时，再扩大构建范围

### 4. 创建 Migration

`Repository.Tool` 已启用 `UsePostgreSqlPartitioning()`，应继续使用该项目创建 Migration：

```powershell
dotnet ef migrations add AddVisitLog --project Repository.Tool --startup-project Repository.Tool --context DatabaseContext
```

不要改用未注册分区 Migration 扩展的启动项目

### 5. 审核 Migration 和 SQL

生成的 `CreateTable` 操作应包含 `PartitionTable:*` Annotation。建议在执行前生成 SQL：

```powershell
dotnet ef migrations script --project Repository.Tool --startup-project Repository.Tool --context DatabaseContext --output partition-migration.sql
```

父表 SQL 应包含：

```sql
CREATE TABLE "VisitLog" (
    "Id" bigint NOT NULL,
    ...
    CONSTRAINT "PK_VisitLog" PRIMARY KEY ("Id")
) PARTITION BY RANGE ("Id");
```

随后应出现一个初始子分区：

```sql
CREATE TABLE "VisitLog_p2026080100"
PARTITION OF "VisitLog"
FOR VALUES FROM (303077051596800000) TO (308694063513600000);
```

具体名称和数字边界由 SQL 生成时刻、UTC+8 边界及 Attribute 周期决定，不要照抄示例值

### 6. 执行 Migration

确认 SQL 后执行：

```powershell
dotnet ef database update --project Repository.Tool --startup-project Repository.Tool --context DatabaseContext
```

Migration 只创建一个初始子分区，不会批量创建历史或未来分区

### 7. 确认自动维护

分区维护由 Repository 内置后台服务自动执行，不依赖 `TaskSetting`，也不需要在 TaskService 中手工启用

现有 `Client.WebAPI`、`Admin.WebAPI` 和 `TaskService` 都会聚合注册该后台服务。每个宿主会在启动完成前检查一次，随后每 10 分钟检查一次；多宿主和多实例通过分布式锁保证同一时间只有一个实例实际维护

首次检查失败会记录错误并等待后续周期重试，不会仅因维护失败而阻止宿主启动。因此宿主启动成功或 `/healthz` 正常，不代表当前与未来分区已经创建成功

`Repository.Tool` 只承载自身的数据库工具任务，不注册分区维护后台服务。创建或执行 Migration 时仍只生成一个初始子分区

新增服务端宿主若要参与自动维护，必须同时调用 `BatchRegisterServices()` 和 `BatchRegisterBackgroundServices()`，并注册可写 `DatabaseContext` 与 `IDistributedLock`

生产环境启用流量前，应通过日志和数据库子分区范围确认自动维护至少成功执行过一次，尤其是执行提前生成的 Migration SQL 文件时

## Attribute 参数

```csharp
[PartitionTable(interval, unit)]
```

| 参数 | 当前允许值 | 说明 |
|---|---|---|
| `interval` | 大于 `0` 的整数常量 | 单个新分区包含的周期数量 |
| `unit` | `Hour`、`Day`、`Month`、`Year` | 分区周期单位 |

分区策略固定为 PostgreSQL `RANGE`，分区键固定为实体的雪花 `long Id`，因此 Attribute 不暴露策略和分区键参数

两个参数都是必填构造参数。一天填写 `(1, PartitionUnit.Day)`，七天填写 `(7, PartitionUnit.Day)`，自然月填写 `(1, PartitionUnit.Month)`

EF Core 模型和 Migration 使用 `PartitionTable:Interval` 保存整数数量，使用 `PartitionTable:Unit` 保存 `Hour`、`Day`、`Month` 或 `Year` 字符串

## EF Core 模型约束

构建模型时会校验：

- 内部策略 Annotation 必须是固定的 `Range`
- `Id` 属性必须存在并映射到当前表
- `Id` 属性 CLR 类型必须是 `long`
- 主键必须包含分区键
- 所有唯一约束和唯一索引必须包含分区键
- 分区父表不能配置为 Npgsql `UNLOGGED` 表

当前直接使用 `Id` 作为唯一分区键，因此继承 `CD` 的实体不需要为了分区把 `CreateTime` 加入联合主键

如果未来扩展为其他分区键，仍需遵守 PostgreSQL 对父表主键、唯一约束和唯一索引必须包含全部分区键的要求

### RowVersion 并发控制

`CD.RowVersion` 使用 `[Timestamp] uint`，映射到 PostgreSQL 系统列 `xmin`。沿用普通表的查询、新增、更新、删除及同步/异步 `SaveChanges` 即可：分区新增会在同一事务内批量回读真实 xmin，更新和删除保留原生并发检查，无需业务代码手工回填版本

三个服务端宿主已为读写上下文、上下文池和池化工厂配置 `UsePostgreSqlPartitioning(snapshot)`。新增宿主或自行构建上下文选项时，先在启动阶段加载一次快照，再复用到该数据库的上下文配置：

```csharp
var snapshot = PartitionTableSnapshot.Load(writeConnectionString);

services.AddDbContextPool<DatabaseContext>(options =>
{
    options.UseNpgsql(writeConnectionString);
    options.UsePostgreSqlPartitioning(snapshot);
});
```

`Repository.Tool` 使用无参数的 `UsePostgreSqlPartitioning()` 进行离线建模和迁移 SQL 生成，不查询数据库。无快照可以建模和查询，但通过 `SaveChanges` 写入会被拒绝；需要保存数据的独立工具也必须先加载快照

#### 实体与保存约定

- 分区新增主键必须在保存前确定；当前模板的雪花 Id 满足要求，不支持数据库生成或临时主键
- 默认保存返回及成功通知中，RowVersion 已回填。`SaveChanges(false)` 只在 EF 中暂存生成值，调用 `AcceptAllChanges` 后才写入 CLR 属性。断开跟踪后更新或删除，仍须像普通表一样提供读取时持有的旧版本
- 支持 TPH 同表继承及 Table Splitting 同表拆分。需要框架创建和维护分区时，仅在 TPH 根实体或共享主实体上声明分区；其他实体沿用同表关系及并发映射，无需重复声明
- 分区适配不支持 TPT/TPC 多表继承和 Entity Splitting（`SplitToTable`）。显式分区声明及涉及其共享表的映射在模型构建阶段校验；DBA 自动发现的分区在保存时解析新增行的回读映射并校验相关限制，不保证所有不支持的映射都会在模型构建时被拒绝
- `ExecuteUpdate`、`ExecuteDelete` 及原始 SQL 不经过此保存流程，不自动同步跟踪实体或添加 xmin 并发条件；需要自行提供版本条件并检查影响行数

#### 事务与失败处理

- 默认配置下，新增写入和 xmin 回读共用事务；已有显式事务或单库环境事务时，通过保存点保护本次保存。成功通知仅表示本次保存完成，不代表外层事务已提交；之后回滚外层事务，需要丢弃或重新加载上下文
- `AutoTransactionBehavior.Never` 且没有可用外层事务时，新增分区行会在整批写入前被拒绝。请使用默认 `WhenNeeded`，或通过 `BeginTransaction`、`UseTransaction`、有效登记的环境事务提供保护。只有普通表操作或分区更新、删除时沿用原生行为
- 关闭 `AutoSavepointsEnabled` 后，已有事务中的失败保存可能保留本次写入，需调用方整体回滚
- 失败后的回滚成功时，保留原数据库或取消异常，取消沿用 EF 取消通知；保存和回滚同时失败时抛出保留两者的 `AggregateException`，应停止使用当前上下文及事务，不能据此假定执行策略会自动重试
- 异步 `TransactionScope` 使用 `TransactionScopeAsyncFlowOption.Enabled`。连接配置 `Enlist=false` 且未显式登记时不属于该环境事务；允许自动事务时，分区新增会独立提交
- 使用执行策略重试外层事务时，把整个事务放入 `CreateExecutionStrategy`，每次尝试创建新的事务范围和上下文。`SavedChanges` 回调自身抛错不会撤销已经提交的保存
- 保存点可能改变同一事务内的 xmin，旧跟踪实体因此可能发生真实并发冲突；xmin 不是每次 UPDATE 都递增的应用版本号
- 多连接可能涉及两阶段提交，当前不承诺分布式事务的透明可靠性；物理网络中断、提交结果不确定及崩溃恢复不在已验证范围内

升级 EF Core、Npgsql 或 PostgreSQL 后，应重新验证分区识别、xmin 回填、真实并发冲突和失败回滚

## 分区边界与命名

### 雪花 ID 边界

当前雪花布局为：

```text
时间戳 | 数据中心 5 位 | 机器 5 位 | 序列号 11 位
```

低位共 21 位，雪花纪元为：

```text
2022-01-01 00:00:00 UTC
```

时间边界会通过 `SnowflakeIdLayout.GetMinIdByTime` 转换为该毫秒对应的最小雪花 ID。PostgreSQL 范围使用下限包含、上限不包含的 `[startId, endId)` 语义

雪花纪元和各段位数属于持久化数据布局。已有数据投入使用后，不应把它们当成普通配置随意修改

### UTC+8 周期边界

所有分区周期使用固定 `UTC+8`，不读取服务器本地时区，也不依赖 Windows 或 Linux 时区名称：

- `Hour` 从 UTC+8 的整点开始
- `Day` 从 UTC+8 的 00:00 开始
- `Month` 从 UTC+8 每月 1 日 00:00 开始，并使用真实自然月长度
- `Year` 从 UTC+8 每年 1 月 1 日 00:00 开始，并使用真实自然年长度

多周期使用 `1970-01-01 00:00:00 +08:00` 作为固定对齐锚点。`7 Day` 表示从该锚点连续划分的七天范围，不额外表达自然周；`3 Month` 会对齐到一月、四月、七月和十月

UTC+8 只影响时间边界的计算和名称。最终 PostgreSQL 范围仍由两个绝对时间对应的雪花 ID 表示

### 子分区名称

- 子分区使用 `{父表名}_pyyyyMMddHH`
- 名称使用实际范围的 UTC+8 开始时间
- 超过 PostgreSQL 63 字节标识符限制时会截短并增加稳定哈希

## Migration 的时间语义

初始子分区以 Migration SQL 的生成时刻为准，再对齐到该时刻所在的 UTC+8 周期：

- `dotnet ef database update`：以命令生成并执行 SQL 时的时刻为准
- `dotnet ef migrations script`：以生成 SQL 文件时的时刻为准

如果 SQL 文件在生成后跨越了一个或多个分区周期才执行，脚本中的初始分区可能已经过期。此时必须在开放写入前确认 Repository 后台服务已经补齐当前和后续三个范围

系统不创建 `DEFAULT` 分区。缺少匹配范围时，PostgreSQL 会明确拒绝写入，避免数据长期堆积在一个无法有效裁剪的兜底分区中

## Repository 自动维护行为

后台服务在宿主启动完成前执行一次，随后每 10 分钟执行。每次维护时会：

1. 从 `DatabaseContext.Model` 发现全部显式声明的分区实体，不读取 xmin 启动快照
2. 获取分布式锁，并在执行期间定期续期
3. 续期失败时取消本轮数据库操作并报告明确错误
4. 查询 PostgreSQL 系统目录验证父表、策略、分区键和直接子分区
5. 验证所有子分区范围有限、不重叠，且边界是完整毫秒对应的最小雪花 ID
6. 保证当前雪花 ID 有可写分区
7. 保证当前分区之后至少存在三个连续子分区

如果所有服务端宿主停机跨越多个周期，后台服务会在任一宿主恢复后从数据库实际最右上界按当前 Attribute 策略向前补齐。已有分区可以使用不同粒度，但从当前写入范围到预建范围必须连续；自动维护不会修复最右边界之前的历史空洞

单张父表一次最多创建 32 个子分区。超过安全上限时，该父表在本轮不会创建任何分区，需要人工确认停机跨度和目标粒度后先补充部分范围，再恢复自动维护

自动维护只向前创建，不会自动补充早于最老已有分区的范围，也不会删除历史分区

三个后续分区表示三个实际范围。例如月策略会预建后续三个自然月，年策略会预建后续三个自然年；过渡分区存在时也计为一个实际范围

## 调整分区周期

`Interval` 和 `Unit` 可以修改，但只对尚未创建的后续分区生效：

- 已有子分区保持原边界
- 已经预建的旧粒度未来分区保持不变
- 不自动拆分、合并、删除或搬迁数据
- 新策略从数据库实际最右上界继续创建
- 最右上界不符合新策略自然边界时，先创建一个过渡分区衔接到下一个自然边界

例如已有两个日分区：

```text
[2026-08-19, 2026-08-20)
[2026-08-20, 2026-08-21)
```

把策略改成 `[PartitionTable(1, PartitionUnit.Month)]`，假设数据库实际最右上界是 `2026-08-21 00:00:00 +08:00`，新分区依次为：

```text
过渡分区：[2026-08-21 00:00:00 +08:00, 2026-09-01 00:00:00 +08:00)
月分区：  [2026-09-01 00:00:00 +08:00, 2026-10-01 00:00:00 +08:00)
```

只修改 `Interval` 或 `Unit` 时，Migration 会记录策略 Annotation 变化并更新 ModelSnapshot。执行 Migration 时不会修改父表和已有子表，只会在 Migration History 中登记这次 Migration

推荐发布顺序：

1. 停止使用旧分区策略的全部服务端宿主，避免旧后台服务继续预建分区
2. 修改实体 Attribute
3. 创建并审核 Migration
4. 执行审核后的 Migration，登记策略变更并更新迁移历史
5. 部署使用新模型的服务端宿主
6. 确认新版本后台服务执行成功

不要让使用不同分区周期的新旧服务端宿主同时运行。分布式锁只能让它们串行执行，不能判断哪一个版本的策略更新

## Migration 回滚注意事项

- 回滚首次创建分区表的 Migration 会删除父表及其子分区，表内数据也会随之删除
- 回滚仅修改 `Interval` 或 `Unit` 的 Migration 不会重建已有子分区
- 回滚策略 Migration 时，应同时回滚实体 Attribute 和服务端宿主版本，避免代码模型与 Migration 版本不一致
- 生产环境执行任何 Down Migration 前都应单独审核生成 SQL 和数据影响

## 不能直接修改的配置

以下变更不能通过普通 Migration 自动处理：

- 给数据库中已有普通表增加 `[PartitionTable]`
- 从已有分区表移除 `[PartitionTable]`
- 把父表改成其他 PostgreSQL 分区策略
- 修改分区键属性或实际列
- 修改雪花纪元或位布局

Migration SQL 生成阶段会拒绝对分区策略、分区键或 Attribute 移除进行普通 `AlterTable`

## 已有普通表转换

不要直接给数据库中已经存在的普通表增加 `[PartitionTable]`

PostgreSQL 不能把普通表原地转换为分区父表。转换需要单独设计：

- 停机或双写窗口
- 新分区父表和子表创建
- 历史数据复制与校验
- 主键、唯一索引、普通索引和外键迁移
- 表名切换及失败回滚

当前能力会拒绝普通 Migration 转换，避免自动删除原表或隐式搬迁数据

## 历史数据导入

数据路由只由雪花 `Id` 决定。导入历史数据前需要注意：

- 自动维护不会向最老分区之前反向创建历史范围
- 历史雪花 `Id` 没有匹配子分区时，PostgreSQL 会拒绝写入
- 需要提前通过专项 Migration 或审核后的 SQL 创建准确历史范围
- 分区边界必须是完整毫秒对应的最小雪花 ID
- 预生成或延迟写入的 ID 应按 ID 自身携带的时间判断目标分区

不要仅依据记录的 `CreateTime` 判断它会写入哪个分区

## 查询与分区裁剪

PostgreSQL 只根据 `Id` 条件裁剪当前分区。仅按 `CreateTime` 查询不会自动推导雪花 ID 范围

时间范围查询建议同时加入 ID 和业务时间条件：

```csharp
long minId = SnowflakeIdLayout.GetMinIdByTime(startTime);
long maxId = SnowflakeIdLayout.GetMinIdByTime(endTime);

var logs = await readDb.VisitLog
    .Where(item => item.Id >= minId && item.Id < maxId)
    .Where(item => item.CreateTime >= startTime && item.CreateTime < endTime)
    .ToListAsync();
```

- `Id` 条件用于分区裁剪
- `CreateTime` 条件用于保持业务时间语义
- 如果 ID 是预生成、延迟写入或历史导入的，需要先确认 ID 时间和业务时间的关系

可以使用 `EXPLAIN` 检查查询计划是否只访问预期子分区

## 数据库验证 SQL

### 确认父表

```sql
SELECT parent_namespace.nspname AS schema_name,
       parent.relname AS table_name,
       parent.relkind,
       pg_catalog.pg_get_partkeydef(parent.oid) AS partition_key
FROM pg_catalog.pg_class AS parent
INNER JOIN pg_catalog.pg_namespace AS parent_namespace
    ON parent_namespace.oid = parent.relnamespace
WHERE parent_namespace.nspname = current_schema()
  AND parent.relname = 'VisitLog';
```

预期：

- `relkind` 为 `p`
- `partition_key` 为 `RANGE ("Id")`

### 查看子分区边界

```sql
SELECT child_namespace.nspname AS schema_name,
       child.relname AS partition_name,
       pg_catalog.pg_get_expr(child.relpartbound, child.oid) AS partition_bound
FROM pg_catalog.pg_inherits AS inheritance
INNER JOIN pg_catalog.pg_class AS parent
    ON parent.oid = inheritance.inhparent
INNER JOIN pg_catalog.pg_namespace AS parent_namespace
    ON parent_namespace.oid = parent.relnamespace
INNER JOIN pg_catalog.pg_class AS child
    ON child.oid = inheritance.inhrelid
INNER JOIN pg_catalog.pg_namespace AS child_namespace
    ON child_namespace.oid = child.relnamespace
WHERE parent_namespace.nspname = current_schema()
  AND parent.relname = 'VisitLog'
ORDER BY child.relname;
```

## 常见问题排查

| 现象 | 常见原因 | 处理方式 |
|---|---|---|
| `no partition of relation ... found for row` | 当前 ID 没有匹配范围 | 检查后台服务错误日志、子分区边界和写入 ID 时间 |
| 表不存在或不是分区父表 | 给已有普通表直接增加了 Attribute，或执行 Migration 的宿主不正确 | 停止自动转换，检查 Migration 和 `Repository.Tool` 配置 |
| 实际分区键与模型不一致 | 数据库父表由其他 SQL 创建，或模型已修改分区键 | 对照 `pg_get_partkeydef` 和实体配置，单独设计结构迁移 |
| 边界不是完整毫秒最小雪花 ID | 子分区由错误的人工 SQL 创建 | 停止维护，核对数据后重新设计正确边界，不能自动删除 |
| 本次需要创建数量超过 32 | 全部服务端宿主停机时间过长或间隔过小 | 人工审核并先补充部分连续范围，再恢复维护 |
| 子分区名称已存在但范围不一致 | 同名表或人工分区与期望冲突 | 核对对象所属父表和实际范围，不要让后台服务自动覆盖 |
| 分布式锁续期失败 | Redis 不可用、网络异常或维护耗时异常 | 恢复 Redis，检查数据库 DDL 阻塞后等待下轮维护 |
| Npgsql SQL 结构兼容异常 | Npgsql 升级改变了建表 SQL 形态 | 暂停生成或执行 Migration，适配并重新做真实数据库验证 |
| 调整周期后旧分区没有变化 | 这是预期行为 | 新策略只影响最右边界之后尚未创建的分区 |

## 上线检查清单

以下清单适用于通过显式声明创建并自动维护的新分区表；DBA 管理的表按前文启动识别约定接入

- [ ] 目标是新表，不是数据库中已经存在的普通表
- [ ] 实体已加入 `DatabaseContext` 的直接 `DbSet<TEntity>`
- [ ] Attribute 明确配置了大于 `0` 的 `Interval` 和受支持的 `PartitionUnit`
- [ ] 主键和全部唯一约束、唯一索引都包含 `Id`
- [ ] 写宿主已加载目标数据库的启动快照，并通过 `UsePostgreSqlPartitioning(snapshot)` 注册到上下文及工厂
- [ ] 已针对目标数据库验证 xmin 回填、真实并发冲突和失败回滚
- [ ] Repository 及受影响宿主构建已通过，EF Core 模型约束已在工具或宿主中验证
- [ ] Migration 中包含全部 `PartitionTable:*` Annotation
- [ ] Migration SQL 中父表包含 `PARTITION BY RANGE ("Id")`
- [ ] Migration SQL 中包含一个初始子分区
- [ ] 目标数据库中的父表和子分区边界已查询确认
- [ ] Repository 分区维护后台服务已成功执行
- [ ] 时间范围查询已根据需要加入 `Id` 裁剪条件
- [ ] 已确认历史数据、归档和删除不依赖自动分区维护

## 常见问题

### 是否需要把 CreateTime 加入联合主键

不需要。当前使用雪花 `Id` 作为分区键，而现有主键本身就是 `Id`，已经满足 PostgreSQL 的唯一约束要求

### 是否支持小时分区

支持。例如 `[PartitionTable(6, PartitionUnit.Hour)]` 表示每 6 小时一个分区，边界按照固定 UTC+8 对齐

### 是否可以从每天一张改成每 7 天一张

可以。把 `[PartitionTable(1, PartitionUnit.Day)]` 改为 `[PartitionTable(7, PartitionUnit.Day)]` 即可；已有日分区保持不变，新建分区从数据库实际最右上界连续衔接到新的七天周期

### 月分区是固定 30 天或 31 天吗

不是。`PartitionUnit.Month` 使用 UTC+8 下的自然月，会正确处理 28、29、30 和 31 天月份；`PartitionUnit.Year` 同样使用自然年并正确处理闰年

### 为什么不创建 DEFAULT 分区

`DEFAULT` 分区会让缺少正式范围的问题不再立即失败，数据可能持续进入兜底表并降低裁剪和维护效果。当前选择明确拒绝越界写入，并通过后台服务提前创建后续三个范围

### 服务端宿主启动时会立即检查分区吗

会。`Client.WebAPI`、`Admin.WebAPI` 和 `TaskService` 都会在启动完成前执行一次检查，此后每 10 分钟检查一次；分布式锁会协调多个宿主和实例

### 自动维护会删除旧分区吗

不会。自动维护只向前创建必要分区。删除、归档、备份或 `DETACH PARTITION` 需要单独设计和审核

## 关键代码位置

| 位置 | 职责 |
|---|---|
| `Repository/Attributes/PartitionTableAttribute.cs` | 实体分区声明和分区周期单位 |
| `SourceGenerator/SourceGenerator.Core/EntityFrameworkCore/PartitionTableGenerator.cs` | 生成 EF Core 分区模型配置 |
| `Repository/Partitioning` | 模型校验、边界计算、Migration SQL 生成与运行维护 |
| `Repository/Partitioning/PartitionXminDatabase.cs` | 保存通知前批量回读新增分区行的 xmin |
| `Repository/Partitioning/PartitionTableSnapshot.cs` | 启动时只读发现实际分区父表，供 xmin 适配使用 |
| `Repository/Partitioning/PartitionModelCustomizer.cs` | 模型配置完成后校验分区继承策略 |
| `Repository/Partitioning/PartitionTimeLayout.cs` | 固定 UTC+8 偏移和对齐锚点 |
| `Infrastructure/IdentifierGenerator/SnowflakeIdLayout.cs` | 雪花 ID 持久化布局和时间换算 |
| `Repository/Partitioning/PartitionMaintenanceBackgroundService.cs` | 自动维护后台服务入口 |
