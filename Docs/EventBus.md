# EventBus：跨进程广播通知

EventBus 将事件广播给所有注册了对应监听的运行实例，包括发布方自身，适合缓存失效、配置重载和内存规则更新。支持 Redis 和数据库两种后端，业务侧写法相同

只需一个 Worker 执行的工作使用 [QueueTask](TaskService.md)；当前请求需要处理结果时直接调用应用服务。EventBus 不提供业务与消息的原子提交，也不保证进程重启后补收旧事件

## 快速接入

以下以商品修改后清理各实例缓存为例，示例类型需自行添加

### 1. 确认配置

`Admin.WebAPI`、`Client.WebAPI`、`TaskService` 已注册 EventBus，`Application.Service` 已引用公共事件组件，无需重复注册。在参与广播的宿主配置中使用：

```json
{
  "EventBus": {
    "Provider": "Redis",
    "Scope": "NetEngine:Production",
    "Retention": "01:00:00"
  }
}
```

互通实例必须使用相同的 `Provider`、`Scope` 和存储目标。Redis 后端要求 Redis 6.2+，复用 `ConnectionStrings:redisConnection`，需连接同一 Redis 数据库。开发配置使用 `NetEngine:Development`，不同环境应使用不同 Scope

### 2. 定义事件

在 `Application/Application.Model/Events` 中定义共享事件，只携带定位变化所需的数据：

```csharp
namespace Application.Model.Events;

/// <summary>
/// 商品信息已变更
/// </summary>
public sealed record ProductChangedEvent(long ProductId);
```

### 3. 声明监听

在 `Application.Service` 中添加监听服务，内置宿主会通过 `BatchRegisterServices()` 自动注册：

```csharp
using Application.Model.Events;
using EventBus;
using Microsoft.Extensions.DependencyInjection;
using SourceGenerator.Runtime.Attributes;
using System.Collections.Concurrent;

namespace Application.Service;

/// <summary>
/// 保存当前进程的商品名称缓存并监听变更
/// </summary>
[RegisterService(Lifetime = ServiceLifetime.Singleton)]
public class ProductCacheService
{

    /// <summary>
    /// 当前进程缓存的商品名称，以商品 ID 为键
    /// </summary>
    public ConcurrentDictionary<long, string> ProductNames { get; } = new();


    /// <summary>
    /// 清理商品缓存，让后续查询重新加载数据
    /// </summary>
    [EventSubscribe]
    public Task OnProductChangedAsync(ProductChangedEvent message)
    {

        ProductNames.TryRemove(message.ProductId, out _);
        return Task.CompletedTask;

    }

}
```

业务读写缓存时注入同一个 `ProductCacheService`，通过 `ProductNames` 访问字典。服务注册为 Singleton，每个进程各持有一份字典；收到广播后删除对应商品，后续读取时重新加载

### 4. 发布事件

在业务应用服务中注入 `IEventPublisher eventPublisher`，保存成功后发布：

```csharp
using Application.Model.Events;
using EventBus;

// 完成原有业务校验和实体修改后保存
await db.SaveChangesAsync(cancellationToken);
await eventPublisher.PublishAsync(new ProductChangedEvent(productId), cancellationToken);
```

`db` 为业务写入上下文，`productId` 为已修改商品的 ID。有外层显式事务时，在 `CommitAsync` 成功后发布

`PublishAsync` 成功仅表示事件已保存到后端，不代表监听已完成。发布不加入业务事务，失败时业务数据可能已经提交；异常或取消也不代表事件一定未写入

## 事件与监听约定

- **事件类型**：使用非泛型具体类或 `record class`，以具体类型发布。事件以 JSON 传输，通过完整类型名匹配；互通宿主应引用一致契约，不支持按父类或接口订阅
- **监听签名**：公开、非泛型实例方法，接收一个事件参数及可选的第二个 `CancellationToken` 参数，返回 `Task` 或 `ValueTask`。无需为监听添加 `virtual`；使用代理时遵循 [AutoProxy 规则](SourceGenerator.md#autoproxy-方法代理)
- **服务注册**：监听服务必须在 `AddRedisEventBus` 或 `AddDatabaseEventBus` 之前加入 DI。同一服务类型有多个实现时，按具体类或不同的具体 Key 注册，参见 [服务注册规则](SourceGenerator.md#服务类型选择规则)
- **依赖与状态**：每次调用创建独立异步作用域，可注入 Scoped 依赖。跨请求的内存状态应放在共享服务中，不能只修改监听对象自身的字段
- **处理逻辑**：保持短小、幂等，传递取消令牌，不启动脱离监听生命周期的后台任务，也不要无条件再次发布相同事件

## 运行行为

| 情况 | 行为 |
|---|---|
| 正常广播 | 各进程独立处理；同一进程按后端记录顺序处理事件，每条事件的监听方法依次执行 |
| 监听失败 | 继续尝试当前事件的其他方法，仅重试失败方法；当前事件成功或到期后才处理后续事件 |
| 短暂断线 | 同一存活进程从原位置补收，超过历史保留时间的事件可能丢失 |
| 启动或重启 | 跳过启动前的事件，本地状态应从当前数据初始化 |
| 事件到期或宿主关闭 | 请求取消监听，处理代码需配合取消 |

`Retention` 从事件保存到后端时计算，同时限制历史保留和失败重试。广播异步传播，不保证各实例立即一致

宿主启动时需连接后端，后端不可用会等待恢复。监听在所有托管服务的 `StartAsync` 完成后开始，本地状态应在此之前完成初始化，且不要在 `StartAsync` 中等待监听结果

## 后端与配置

### 切换数据库后端

内置宿主将 `EventBus.Provider` 改为 `Database` 后重启即可切换，互通实例需一起切换。使用前通过 [Repository.Tool 迁移入口](../README.md#快速开始)准备 `EventBusStream` 和 `EventBusMessage` 两张表，宿主不会自动建表

内置 `DatabaseContext` 已调用 `modelBuilder.ConfigureEventBus()`。使用其他上下文时，引用 `Infrastructure/EventBus.Database` 并补充该映射。注册时使用主库 `IDbContextFactory<TContext>`，不能使用允许复制延迟的读库

数据库后端的广播、重启和重试规则与 Redis 相同；切换后也不会取消宿主其他能力对 Redis 的依赖

### 配置项

下表为 `EventBusOptions` 默认值；内置宿主配置会覆盖 `Scope` 等选项：

| 选项 | 默认值 | 用途 |
|---|---|---|
| `Provider` | `Redis` | 内置宿主选择 `Redis` 或 `Database` |
| `Scope` | `NetEngine` | 广播范围，非空且不超过 200 个字符 |
| `Retention` | `01:00:00` | 历史保留与重试期限，必须大于零 |
| `PollInterval` | `00:00:01` | 未读满一批或读取失败后的等待时间 |
| `RetryInterval` | `00:00:01` | 失败监听的重试间隔 |
| `CleanupInterval` | `00:01:00` | 过期事件的清理间隔 |
| `BatchSize` | `100` | 每批读取条数，范围 1～1000 |

三个间隔范围为 1～4294967294 毫秒。同一 Scope 的实例应保持相同 `Retention`，避免共享历史被提前清理

### 新宿主接入

应用服务引用 `Infrastructure/EventBus`，宿主引用所选后端项目。先注册业务服务和后端依赖，再注册一个后端；扩展方法已包含后台服务：

```csharp
using EventBus.Redis;

builder.Services.BatchRegisterServices();
// 此前需注册 IConnectionMultiplexer 和监听服务的依赖
builder.Services.AddRedisEventBus(options => options.Scope = "NetEngine:Production");
```

数据库后端先准备模型与表、注册主库上下文工厂，再将上面的 `AddRedisEventBus` 替换为：

```csharp
using EventBus.Database;
using Repository;

builder.Services.AddDatabaseEventBus<DatabaseContext>(options => options.Scope = "NetEngine:Production");
```

手动接入时由扩展方法选择后端；`Provider` 配置用于内置宿主的后端选择。扩展其他后端时，实现 [IEventTransport](../Infrastructure/EventBus/IEventTransport.cs) 并调用 `AddEventBus<TTransport>`
