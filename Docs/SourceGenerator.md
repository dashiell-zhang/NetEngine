# 源码生成器

仓库使用 `SourceGenerator.Core` 在编译期生成重复代码，使用 `SourceGenerator.Runtime` 提供特性和代理运行时。常规业务项目不需要手动引用这两个项目，根目录 `Directory.Build.props` 会自动接入

## 自动接入范围

默认情况下，仓库内项目会同时获得：

- `SourceGenerator.Core` 的 Analyzer 引用
- `SourceGenerator.Runtime` 的普通项目引用

以下项目会被排除，避免生成器自引用或让独立工具携带不需要的运行时依赖：

- `SourceGenerator` 目录下的项目
- `Infrastructure` 目录下的项目
- `Deployment` 目录下的项目

Debug 构建会把生成源码输出到：

```text
<项目>/obj/Debug/<TargetFramework>/Generated/
```

遇到自动注册或代理行为不符合预期时，先构建目标项目，再检查该目录中的 `.g.cs` 文件和编译诊断

## 自动注册服务

在实现类上标记 `[RegisterService]`：

```csharp
[RegisterService(Lifetime = ServiceLifetime.Scoped)]
public class UserService
{
}
```

启动项目调用一次：

```csharp
builder.Services.BatchRegisterServices();
```

该方法会聚合当前启动项目及其引用程序集生成的服务注册代码

### 服务类型选择规则

未显式指定服务类型时：

- 没有直接实现的业务接口：按自身类型注册
- 只有一个直接实现的业务接口：按该接口注册
- 有多个直接实现的业务接口：生成 `RegisterService001` 编译错误，必须显式选择

显式指定接口：

```csharp
[RegisterService(typeof(IUserService), Lifetime = ServiceLifetime.Scoped)]
public class UserService : IUserService
{
}
```

指定的服务类型必须是实现类自身或其实现的接口

### 生命周期和 Keyed Service

`Lifetime` 默认是 `Transient`，可设置为 `Singleton` 或 `Scoped`

```csharp
[RegisterService(Lifetime = ServiceLifetime.Singleton, Key = "primary")]
public class PrimaryClock : IClock
{
}
```

设置 `Key` 后生成 Keyed Service 注册。消费方应通过 .NET keyed DI API 获取对应实例

## 自动注册后台服务

继承 `BackgroundService` 的非抽象类会被后台服务生成器发现，不需要额外特性：

```csharp
public class DataSyncBackgroundService : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.CompletedTask;
    }
}
```

启动项目调用：

```csharp
builder.Services.BatchRegisterBackgroundServices();
```

该方法会注册当前项目及引用程序集中的可访问后台服务。后台服务不能是开放泛型，并且至少需要一个 `public` 实例构造函数；不满足条件时生成器会给出编译诊断

不要再对同一个后台服务重复调用 `AddHostedService`

## AutoProxy 方法代理

`[AutoProxy]` 会为服务生成派生代理，并由 `[RegisterService]` 生成的 DI 代码注册代理实现：

```csharp
[AutoProxy]
[RegisterService(Lifetime = ServiceLifetime.Scoped)]
public class ProductService
{
    [Logging]
    [Retry(MaxRetries = 2, DelaySeconds = 1)]
    public virtual Task<ProductDto> GetAsync(long id, CancellationToken cancellationToken)
    {
        // 业务逻辑
        throw new NotImplementedException();
    }
}
```

需要拦截的普通类方法应声明为 `public virtual`，并通过 DI 获取服务，不要在业务代码中直接 `new` 实现类

目标类不能是 `static`、`sealed`、`abstract` 或 `record class`，并且至少包含一个 `public` 构造函数。不支持的目标或方法签名会产生 `AutoProxy001` 等编译错误，应按诊断调整声明，不要绕过代理手写重复逻辑

### Logging

`[Logging]` 记录 executing、executed 和 exception 阶段的结构化日志，包括方法、追踪标识、参数、调用链、耗时和结果等信息

```csharp
[Logging]
public virtual Task SyncAsync(CancellationToken cancellationToken)
{
    // 业务逻辑
    return Task.CompletedTask;
}
```

### Cacheable

`[Cacheable]` 对有返回值的方法使用 `IDistributedCache` 缓存结果，缓存键包含方法和参数摘要：

```csharp
[Cacheable(TtlSeconds = 300)]
public virtual Task<ProductDto> GetAsync(long id, CancellationToken cancellationToken)
{
    // 查询逻辑
    throw new NotImplementedException();
}
```

- `TtlSeconds` 默认 `60`，必须大于 `0`
- `void` 和无结果的 `Task` 不写入缓存
- 未注册 `IDistributedCache` 时跳过缓存并记录日志
- 参数无法生成稳定摘要时跳过缓存
- 已注册 `IDistributedLock` 时会自动使用锁防止缓存击穿，未注册时仍可缓存但没有该保护

### ConcurrencyLimit

`[ConcurrencyLimit]` 基于 `IDistributedLock` 限制方法并发：

```csharp
[ConcurrencyLimit(IsUseParameter = true, IsBlock = true, ExpirySeconds = 60, Semaphore = 1)]
public virtual Task ProcessAsync(long orderId, CancellationToken cancellationToken)
{
    // 业务逻辑
    return Task.CompletedTask;
}
```

| 参数 | 默认值 | 说明 |
|---|---:|---|
| `IsUseParameter` | `false` | 是否将参数摘要加入锁键 |
| `IsBlock` | `false` | `true` 时获取失败立即抛出“请勿频繁操作”，`false` 时等待 |
| `ExpirySeconds` | `0` | 小于等于 `0` 时使用 60 秒租约和等待时长 |
| `Semaphore` | `1` | 同一锁键允许的并发数，小于等于 `0` 时按 `1` 处理 |

运行期间会自动续期锁。使用该特性的宿主必须注册 `IDistributedLock`，否则调用时抛出 `InvalidOperationException`

### Retry

`[Retry]` 在非取消异常发生后重试：

```csharp
[Retry(MaxRetries = 3, DelaySeconds = 2)]
public virtual Task SendAsync(CancellationToken cancellationToken)
{
    // 外部调用
    return Task.CompletedTask;
}
```

- `MaxRetries` 默认 `3`，表示首次失败后最多额外执行 3 次
- `DelaySeconds` 默认 `0`
- 两个参数都不能小于 `0`
- `OperationCanceledException` 不重试，取消令牌也会终止等待
- 是否幂等由业务方法负责保证

### 多行为顺序

多个行为按特性声明顺序包裹执行：

```csharp
[Logging]
[Retry(MaxRetries = 2)]
[Cacheable(TtlSeconds = 120)]
public virtual Task<ProductDto> GetAsync(long id)
{
    // 业务逻辑
    throw new NotImplementedException();
}
```

执行顺序为 `Logging → Retry → Cacheable → 实际方法`

## EF Core 软删除过滤器

生成器会查找 DbContext 直接声明的 `DbSet<T>`。实体继承 `Repository.Bases.CD` 时，会为该实体生成：

```csharp
modelBuilder.Entity<TEntity>().HasQueryFilter(entity => entity.DeleteTime == null);
```

`DatabaseContext.OnModelCreating` 已统一调用：

```csharp
modelBuilder.ApplySoftDeleteFilters(this);
```

新增软删除实体时，继承 `CD` 并把实体加入对应 DbContext 的 `DbSet<T>` 即可，不要再重复写全局过滤器。确实需要查询已删除数据时使用 EF Core 的 `IgnoreQueryFilters()`

## EF Core JSON 列映射

对实体中的复杂对象或 `List<T>` 属性标记 `[JsonColumn]`：

```csharp
public class Product : CD
{
    [JsonColumn]
    public ProductSnapshot Snapshot { get; set; } = new();
}
```

生成器会为对应 DbContext 输出 `ComplexProperty` 或 `ComplexCollection` 的 JSON 映射。`DatabaseContext.OnModelCreating` 已统一调用：

```csharp
modelBuilder.ApplyJsonColumns(this);
```

当前根属性仅支持复杂类型或 `List<T>`，不支持把 `Dictionary<TKey, TValue>` 作为根属性，也不支持静态属性、索引器、无 getter 属性和循环嵌套。违反约束时会产生 `JsonColumn001` 至 `JsonColumn007` 编译诊断

## EF Core PostgreSQL 分区表

生成器会发现 DbSet 实体上的 `[PartitionTable]`，并为对应 DbContext 输出：

```csharp
modelBuilder.ApplyPartitionTables(this);
```

生成配置把分区策略、实际列名、周期数量和周期单位写入 EF Core Annotation，随后由 Repository 中的模型校验、Migration SQL 生成器和运行维护服务共同使用

实体通过两个必填参数声明周期，例如 `[PartitionTable(1, PartitionUnit.Month)]`。当前支持 `Hour`、`Day`、`Month`、`Year`，时间边界固定按照 UTC+8 计算

当前只支持雪花 `long Id` 的 PostgreSQL `RANGE` 分区。完整声明、迁移和维护规则见 [PostgreSQL 分区表](PostgreSqlPartitionTable.md)

## 新增能力时的检查清单

- 新服务是否可以直接使用 `[RegisterService]`
- 宿主是否已经调用 `BatchRegisterServices()`
- 新后台服务是否已经由 `BatchRegisterBackgroundServices()` 覆盖
- 代理类是否同时标记 `[AutoProxy]` 和 `[RegisterService]`
- 被拦截方法是否可代理，并且调用方是否通过 DI 获取服务
- 缓存和并发行为所需的 `IDistributedCache`、`IDistributedLock` 是否已经注册
- 新的 `CD` 实体或 `[JsonColumn]` 属性是否已加入正确 DbContext
- 新分区实体是否满足主键和所有唯一约束包含雪花 ID 分区键
- 构建后是否检查了生成代码和编译诊断
