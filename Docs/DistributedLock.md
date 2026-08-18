# 分布式锁

仓库通过统一的 `IDistributedLock` 抽象提供 Redis 锁和进程内锁。业务代码依赖抽象，不直接依赖具体实现

## 如何选择

| 实现 | 注册方式 | 适用场景 |
|---|---|---|
| Redis 锁 | `AddRedisLock(...)` | WebAPI、TaskService、多进程或多机器部署 |
| 内存锁 | `AddInMemoryLock()` | 单进程客户端或明确不需要跨进程互斥的场景 |

内存锁只在当前进程内生效，不能阻止另一进程或另一台机器执行相同操作。服务端部署默认应使用 Redis 锁

相关项目：

- `Infrastructure/DistributedLock`：抽象和锁句柄
- `Infrastructure/DistributedLock.Redis`：Redis 实现
- `Infrastructure/DistributedLock.InMemory`：进程内实现

## 注册

服务端宿主使用 Redis：

```csharp
services.AddRedisLock(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("redisConnection")!;
    options.InstanceName = "lock";
});
```

`Configuration` 使用 StackExchange.Redis 连接字符串，`InstanceName` 是 Redis 物理键的统一前缀

仅需进程内互斥时：

```csharp
services.AddInMemoryLock();
```

同一个宿主只注册一种 `IDistributedLock` 实现

## 等待获取锁

`LockAsync` 会等待锁可用，超过等待时长后抛出 `TimeoutException`：

```csharp
public class OrderService(IDistributedLock distributedLock)
{
    public async Task ProcessAsync(long orderId, CancellationToken cancellationToken)
    {
        await using (await distributedLock.LockAsync(
            $"order:{orderId}",
            TimeSpan.FromSeconds(30),
            cancellationToken: cancellationToken))
        {
            // 需要互斥执行的业务逻辑
        }
    }
}
```

`expiry` 同时表示单次租约时长和最长等待时长。未传或传入 `default` 时使用 1 分钟

## 尝试获取锁

`TryLockAsync` 只尝试一次，锁被占用时返回 `null`：

```csharp
await using var lockHandle = await distributedLock.TryLockAsync(
    $"order:{orderId}",
    TimeSpan.FromSeconds(30),
    cancellationToken: cancellationToken);

if (lockHandle is null)
{
    return;
}

// 成功持有锁后的业务逻辑
```

适合“已有实例执行时直接跳过或返回提示”的场景

## 并发信号量

`semaphore` 表示同一个逻辑键允许同时持有锁的数量，默认值为 `1`：

```csharp
await using var lockHandle = await distributedLock.TryLockAsync(
    "image:convert",
    TimeSpan.FromMinutes(2),
    semaphore: 4,
    cancellationToken: cancellationToken);
```

所有使用同一逻辑键的调用方应使用相同的 `semaphore`，否则并发语义会变得难以判断

## 长任务续期

如果业务执行时间可能超过初始租约，需要在租约到期前调用 `RenewAsync`：

```csharp
var renewed = await distributedLock.RenewAsync(
    lockHandle,
    TimeSpan.FromMinutes(2),
    cancellationToken);
```

返回 `false` 表示句柄已释放、锁所有权已经丢失，或该句柄不属于当前锁实现。长任务通常需要后台循环续期，并在业务结束时停止续期循环

`ConcurrencyLimit` 代理行为和 TaskService 队列执行器已经包含自动续期逻辑，使用这些能力时不需要再手写续期

## 参数和释放规则

- `key` 不能为空或空白
- `expiry` 必须大于零；`default` 会转换为 1 分钟
- `semaphore` 必须大于零
- 等待和续期都支持 `CancellationToken`
- 锁句柄实现了 `IDisposable` 和 `IAsyncDisposable`，异步代码优先使用 `await using`
- 不要缓存或复用已经释放的锁句柄
- 释放失败会记录日志，但不应覆盖已经得到的业务结果

Redis 实现不会直接使用原始业务键，而是基于逻辑键摘要和信号量槽位生成物理键。业务代码仍应使用稳定、可读且带业务命名空间的逻辑键，例如 `order:123` 或 `user:sync:456`

## 与 AutoProxy 的关系

方法级并发控制可以使用 `[ConcurrencyLimit]`，底层同样依赖 `IDistributedLock`：

```csharp
[ConcurrencyLimit(IsUseParameter = true, IsBlock = true, ExpirySeconds = 60)]
public virtual Task ProcessAsync(long orderId, CancellationToken cancellationToken)
{
    // 业务逻辑
    return Task.CompletedTask;
}
```

简单的方法级限制优先使用该特性；需要多个操作共享同一锁、动态控制键或自行处理未获取锁结果时，直接注入 `IDistributedLock`

AutoProxy 的完整用法见 [源码生成器](SourceGenerator.md#autoproxy-方法代理)
