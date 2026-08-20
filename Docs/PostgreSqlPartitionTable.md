# PostgreSQL 分区表

本文说明 NetEngine 中基于 EF Core、Npgsql 和雪花 `Id` 的 PostgreSQL `RANGE` 分区表使用方式

## 功能概览

给新实体增加 `[PartitionTable]` 后，NetEngine 会完成以下工作：

1. 源码生成器把实体声明转换为 EF Core 模型 Annotation
2. EF Migration 创建 `PARTITION BY RANGE ("Id")` 父表
3. Migration 创建 SQL 生成时刻所在范围的一个初始子分区
4. Repository 后台服务保证当前范围和三个后续范围可写

当前支持：

- PostgreSQL `RANGE` 分区
- 单列雪花 `long Id` 分区键
- 固定小时数间隔
- 新表随 EF Migration 直接创建为分区表
- `IntervalHours` 向后续新分区生效
- 多宿主和多实例通过可续期分布式锁协调维护

当前不支持：

- 自动把已有普通表转换为分区表
- `LIST`、`HASH`、表达式、多字段或多级分区
- 自动拆分、合并、删除或归档已有子分区
- 自动创建早于最老已有分区的历史范围
- `DEFAULT` 分区

## 选择分区间隔

分区间隔使用 `IntervalHours` 表示固定小时数，不表示自然月、季度或自然周

| 示例 | 含义 | 常见场景 |
|---|---|---|
| `1` | 每小时一个分区 | 写入量很高且需要细粒度维护 |
| `6` | 每 6 小时一个分区 | 小时间隔与对象数量之间的折中 |
| `24` | 每 24 小时一个分区 | 通常建议优先采用的起点 |
| `168` | 每 168 小时，也就是 7 天一个分区 | 日写入量较低的表 |
| `720` | 每 720 小时，也就是 30 天一个分区 | 写入量较低且不要求自然月对齐 |

选择时需要平衡：

- 间隔越小，单个分区越小，但数据库对象和后续维护次数越多
- 间隔越大，分区数量越少，但单个分区数据量和索引体积更大
- Repository 后台服务启动后立即检查一次，随后每 10 分钟检查，因此首版允许的最小单位是小时

建议先根据预估写入量选择 `24` 或 `168`，上线后可以调整，新间隔只影响尚未创建的后续分区

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
[PartitionTable(IntervalHours = 24)]
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
- 每个新子分区覆盖固定 1 天
- 分区时间边界统一使用 UTC
- `Id` 按 `SnowflakeIdLayout` 中的雪花纪元和位布局换算

### 2. 加入 DatabaseContext

实体必须作为直接 `DbSet<TEntity>` 加入 `DatabaseContext`，源码生成器才会为该上下文生成分区模型配置：

```csharp
public DbSet<VisitLog> VisitLog { get; set; }
```

不需要在 `OnModelCreating` 中手工调用 `PartitionModelBuilder.Configure`

### 3. 构建检查

先构建 Repository 或整个解决方案，让源码生成器和 EF Core 模型校验尽早发现配置错误：

```powershell
dotnet build NetEngine.slnx
```

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
CREATE TABLE "VisitLog_p2026082000"
PARTITION OF "VisitLog"
FOR VALUES FROM (306580134297600000) TO (306761328230400000);
```

具体名称和数字边界由 SQL 生成时的 UTC 时间及注解间隔决定，不要照抄示例值

### 6. 执行 Migration

确认 SQL 后执行：

```powershell
dotnet ef database update --project Repository.Tool --startup-project Repository.Tool --context DatabaseContext
```

Migration 只创建一个初始子分区，不会批量创建历史或未来分区

### 7. 确认自动维护

分区维护由 Repository 内置后台服务自动执行，不依赖 `TaskSetting`，也不需要在 TaskService 中手工启用

现有 `Client.WebAPI`、`Admin.WebAPI` 和 `TaskService` 都会聚合注册该后台服务。每个宿主会在启动完成前检查一次，随后每 10 分钟检查一次；多宿主和多实例通过分布式锁保证同一时间只有一个实例实际维护

`Repository.Tool` 只承载自身的数据库工具任务，不注册分区维护后台服务。创建或执行 Migration 时仍只生成一个初始子分区

新增服务端宿主若要参与自动维护，必须同时调用 `BatchRegisterServices()` 和 `BatchRegisterBackgroundServices()`，并注册可写 `DatabaseContext` 与 `IDistributedLock`

生产环境启用流量前，应通过日志和数据库子分区范围确认自动维护至少成功执行过一次，尤其是执行提前生成的 Migration SQL 文件时

## Attribute 参数

```csharp
[PartitionTable(IntervalHours = intervalHours)]
```

| 参数 | 当前允许值 | 说明 |
|---|---|---|
| `IntervalHours` | 大于 `0` 的整数 | 单个新分区包含的小时数 |

分区策略固定为 PostgreSQL `RANGE`，分区键固定为实体的雪花 `long Id`，因此 Attribute 不暴露策略和分区键参数

`IntervalHours` 应显式填写，不要依赖整数默认值。一天填写 `24`，七天填写 `168`

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

普通 PostgreSQL 表可以把 `CD.RowVersion` 映射到系统列 `xmin`，但分区父表执行 `INSERT ... RETURNING xmin` 时无法取得实际子分区的系统列

现有写宿主通过 `PostgresPatchInterceptor` 把插入返回值替换为当前事务 ID，它与新行的 `xmin` 一致，因此分区实体仍沿用现有 `RowVersion` 乐观并发机制。`Client.WebAPI`、`Admin.WebAPI` 和 `TaskService` 均已注册该拦截器

新增其他写宿主时也必须注册 `PostgresPatchInterceptor`，否则通过 EF 插入继承 `CD` 的分区实体会失败

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

### 固定时长

`IntervalHours` 始终按固定小时数计算：

```text
分区时长 = IntervalHours × 60 × 60 × 1000 毫秒
```

`168` 小时不保证从周一开始，`720` 小时也不等于自然月

### 子分区名称

- 子分区使用 `{父表名}_pyyyyMMddHH`
- 名称使用实际范围的 UTC 开始时间
- 超过 PostgreSQL 63 字节标识符限制时会截短并增加稳定哈希

## Migration 的时间语义

初始子分区以 Migration SQL 的生成时间为准：

- `dotnet ef database update`：以命令生成并执行 SQL 时的 UTC 时间为准
- `dotnet ef migrations script`：以生成 SQL 文件时的 UTC 时间为准

如果 SQL 文件在生成后跨越了一个或多个分区周期才执行，脚本中的初始分区可能已经过期。此时必须在开放写入前确认 Repository 后台服务已经补齐当前和后续三个范围

系统不创建 `DEFAULT` 分区。缺少匹配范围时，PostgreSQL 会明确拒绝写入，避免数据长期堆积在一个无法有效裁剪的兜底分区中

## Repository 自动维护行为

后台服务在宿主启动完成前执行一次，随后每 10 分钟执行。每次维护时会：

1. 从 `DatabaseContext.Model` 发现全部分区实体
2. 获取分布式锁，并在执行期间定期续期
3. 续期失败时取消本轮数据库操作并报告明确错误
4. 查询 PostgreSQL 系统目录验证父表、策略、分区键和直接子分区
5. 验证所有子分区范围有限、不重叠，且边界是完整毫秒对应的最小雪花 ID
6. 保证当前雪花 ID 有可写分区
7. 保证当前分区之后至少存在三个连续子分区

如果所有服务端宿主停机跨越多个周期，后台服务会在任一宿主恢复后从数据库实际最右上界按当前 Attribute 策略向前补齐。已有分区可以使用不同粒度，但从当前写入范围到预建范围必须连续；自动维护不会修复最右边界之前的历史空洞

单张父表一次最多创建 32 个子分区。超过安全上限时，该父表在本轮不会创建任何分区，需要人工确认停机跨度和目标粒度后先补充部分范围，再恢复自动维护

自动维护只向前创建，不会自动补充早于最老已有分区的范围，也不会删除历史分区

## 调整 IntervalHours

`IntervalHours` 可以修改，但只对尚未创建的后续分区生效：

- 已有子分区保持原边界
- 已经预建的旧粒度未来分区保持不变
- 不自动拆分、合并、删除或搬迁数据
- 新策略从数据库实际最右上界继续创建

例如已有两个日分区：

```text
[2026-08-19, 2026-08-20)
[2026-08-20, 2026-08-21)
```

把 `IntervalHours` 改成 `168` 后，下一个新分区是：

```text
[2026-08-21, 2026-08-28)
```

只修改 `IntervalHours` 时，Migration 会记录策略 Annotation 变化并更新 ModelSnapshot。执行 Migration 时不会修改父表和已有子表，只会在 Migration History 中登记这次 Migration

推荐发布顺序：

1. 停止使用旧分区策略的全部服务端宿主，避免旧后台服务继续预建分区
2. 修改实体 Attribute
3. 创建并审核 Migration
4. 部署使用新模型的服务端宿主
5. 确认新版本后台服务执行成功

不要让使用不同分区间隔的新旧服务端宿主同时运行。分布式锁只能让它们串行执行，不能判断哪一个版本的策略更新

## Migration 回滚注意事项

- 回滚首次创建分区表的 Migration 会删除父表及其子分区，表内数据也会随之删除
- 回滚仅修改 `IntervalHours` 的 Migration 不会重建已有子分区
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
| 调整间隔后旧分区没有变化 | 这是预期行为 | 新策略只影响最右边界之后尚未创建的分区 |

## 上线检查清单

- [ ] 目标是新表，不是数据库中已经存在的普通表
- [ ] 实体已加入 `DatabaseContext` 的直接 `DbSet<TEntity>`
- [ ] Attribute 明确配置了 `IntervalHours`
- [ ] 主键和全部唯一约束、唯一索引都包含 `Id`
- [ ] 所有写宿主均已注册 `PostgresPatchInterceptor`
- [ ] `dotnet build NetEngine.slnx` 已通过
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

支持。例如 `IntervalHours = 6` 表示每 6 小时一个固定时长分区

### 是否可以从每天一张改成每 7 天一张

可以。把 `IntervalHours` 从 `24` 改为 `168` 即可；已有日分区保持不变，新建分区从数据库实际最右上界开始使用 168 小时间隔

### 为什么不创建 DEFAULT 分区

`DEFAULT` 分区会让缺少正式范围的问题不再立即失败，数据可能持续进入兜底表并降低裁剪和维护效果。当前选择明确拒绝越界写入，并通过后台服务提前创建后续三个范围

### 服务端宿主启动时会立即检查分区吗

会。`Client.WebAPI`、`Admin.WebAPI` 和 `TaskService` 都会在启动完成前执行一次检查，此后每 10 分钟检查一次；分布式锁会协调多个宿主和实例

### 自动维护会删除旧分区吗

不会。自动维护只向前创建必要分区。删除、归档、备份或 `DETACH PARTITION` 需要单独设计和审核

## 关键代码位置

| 位置 | 职责 |
|---|---|
| `Repository/Attributes/PartitionTableAttribute.cs` | 实体分区声明 |
| `SourceGenerator/SourceGenerator.Core/PartitionTableGenerator.cs` | 生成 EF Core 分区模型配置 |
| `Repository/Partitioning` | 模型校验、边界计算、Migration SQL 生成与运行维护 |
| `Infrastructure/IdentifierGenerator/SnowflakeIdLayout.cs` | 雪花 ID 持久化布局和时间换算 |
| `Repository/Partitioning/PartitionMaintenanceBackgroundService.cs` | 自动维护后台服务入口 |
