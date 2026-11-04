# 进程广播事件

进程广播用于让每个订阅实例分别处理事件，例如清理自己的 HybridCache、重新加载配置或刷新本地规则。它与由 Worker 竞争领取的 QueueTask 独立，多个实例可以同时处理同一条广播

公共能力位于 Infrastructure/EventBus，Redis Streams 后端位于 Infrastructure/EventBus.Redis，数据库后端、组件实体和映射位于 Infrastructure/EventBus.Database。数据库后端内部使用 EF Core，并复用已有主库上下文工厂；基础层不反向引用业务 Repository，应用服务只需引用公共 EventBus 能力

## 行为约定

| 行为 | 约定 |
|---|---|
| 消费范围 | 每个进程执行自己注册的相关监听方法，发布进程也参与接收 |
| 启动起点 | 在托管服务启动前建立后端的已提交位置，跳过此前事件 |
| 断线恢复 | 同一存活进程使用原内存游标继续读取 |
| 进程重启 | 重新建立起点，不恢复旧进程的消费进度 |
| 历史保留 | 默认一小时，后台每分钟物理清理历史 |
| 发布成功 | 事件已保存到所选后端，不表示所有监听函数已经完成 |
| 发布失败 | 异常向调用方传播，不返回伪成功 |
| 重复执行 | 允许发生，监听业务应保持幂等 |

事件只包含通知所需数据。推荐广播“数据已经变化，请读取当前状态”，不依赖完整历史事件还原业务状态

广播是异步传播，不能保证数据库提交后所有进程立即看到相同状态。发短信、生成文件等单次后台业务继续使用 [QueueTask](TaskService.md)

## 定义事件

共享事件 DTO 放在 Application.Model，例如：

~~~csharp
namespace Application.Model.Events;

/// <summary>
/// 产品信息已变更事件
/// </summary>
public sealed record ProductChangedEvent(long ProductId);


/// <summary>
/// 应用配置已变更事件
/// </summary>
public sealed record AppSettingChangedEvent(string Key);
~~~

事件必须是非泛型具体类。发布使用明确的具体类型，不支持接口、object 或隐式多态事件。事件的完整类型名作为传输名称，不包含程序集版本，互通宿主需要引用一致的事件契约

## 声明监听方法

应用服务引用 EventBus，沿用 RegisterService 自动注册。一个服务可以有多个 EventSubscribe 方法，同一事件也可以被多个方法监听，不要求实现事件处理接口

~~~csharp
using Application.Model.Events;
using EventBus;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using SourceGenerator.Runtime.Attributes;

/// <summary>
/// 处理当前进程的缓存变化通知
/// </summary>
[RegisterService(Lifetime = ServiceLifetime.Scoped)]
public class CacheEventService(HybridCache cache)
{

    /// <summary>
    /// 产品变化后清理产品缓存
    /// </summary>
    [EventSubscribe]
    public async Task OnProductChangedAsync(ProductChangedEvent message, CancellationToken cancellationToken)
    {

        await cache.RemoveAsync($"product:{message.ProductId}", cancellationToken);

    }


    /// <summary>
    /// 配置变化后清理配置缓存
    /// </summary>
    [EventSubscribe]
    public async ValueTask OnAppSettingChangedAsync(AppSettingChangedEvent message)
    {

        await cache.RemoveAsync($"app-setting:{message.Key}");

    }

}
~~~

示例缓存键需要与实际读取端一致。仓库也提供表达式缓存键扩展，不能假定所有缓存都使用这些示例键。HybridCache.RemoveAsync 同时影响当前实例和共享二级缓存，它不是仅删除本地缓存的接口

监听方法要求：

- 公开的非泛型实例方法，接收一个事件参数及可选的第二个 CancellationToken 参数
- 返回 Task 或 ValueTask，不支持 void、async void、Task<T> 或 ValueTask<T>
- 所在服务已在后端注册前加入 DI，普通服务优先使用 RegisterService 和 BatchRegisterServices
- 不支持 Keyed Service、开放泛型服务和无法从服务契约识别实现类型的工厂注册监听

监听服务可注入 Scoped 依赖；每次方法调用使用独立异步作用域，完成后释放。通过接口注册服务时，属于该接口或其继承接口的监听方法会通过接口调用，保留代理行为。方法不必为了监听声明 virtual；使用 AutoProxy 时继续遵守 [代理规则](SourceGenerator.md)

三个宿主引用并注册同一处理服务后，每个进程都会调用自己的方法。不要从监听函数无条件再次发布相同事件，避免广播循环

## 发布事件

在应用服务中注入 IEventPublisher：

~~~csharp
await eventPublisher.PublishAsync(new ProductChangedEvent(productId), cancellationToken);
~~~

先提交业务数据，再发布广播。没有外层显式事务时，可以在 SaveChangesAsync 成功后发布；有显式事务时，在 CommitAsync 成功后发布

发布使用后端自己的保存操作，EF 后端也不会自动加入当前业务 DbContext 的事务。发布失败时业务可能已经提交，调用方会收到异常。本能力不提供 Outbox、原子业务事务发布、自动回退或自动双写

## 宿主接入与后端切换

Admin.WebAPI、Client.WebAPI、TaskService 已按相同方式接入，配置位于各自的 appsettings.json 和 appsettings.Development.json：

~~~json
{
  "EventBus": {
    "Provider": "Redis",
    "Scope": "NetEngine:Production",
    "Retention": "01:00:00"
  }
}
~~~

开发配置使用 NetEngine:Development。需要互通的宿主必须选择相同 Provider、相同 Scope 及相同存储目标，不按宿主名称隔离频道。不同环境使用不同 Scope

Provider 可选择 Redis 或 Database，默认 Redis。切换配置后重启宿主，发布端与监听函数不需要修改。不同后端之间不自动转发事件

独立宿主接入片段如下，先完成业务服务注册及后端依赖注册：

~~~csharp
using EventBus;
using EventBus.Database;
using EventBus.Redis;
using Repository;

builder.Services.BatchRegisterServices();

// Redis 使用已有 IConnectionMultiplexer
builder.Services.AddRedisEventBus(options =>
{
    options.Scope = "NetEngine:Production";
    options.Retention = TimeSpan.FromHours(1);
});

// 使用数据库后端时将上面的 AddRedisEventBus 替换为以下注册
// builder.Services.AddDatabaseEventBus<DatabaseContext>(options =>
// {
//     options.Scope = "NetEngine:Production";
//     options.Retention = TimeSpan.FromHours(1);
// });
~~~

同一宿主只能注册一个后端。调用对应的扩展方法即可完成广播运行服务的注册，无需额外调用 AddHostedService

| 选项 | 默认值 | 用途 |
|---|---|---|
| Provider | Redis | 三个内置宿主选择后端 |
| Scope | NetEngine | 广播范围，内置宿主通过配置覆盖 |
| Retention | 01:00:00 | 历史保留和失败重试的时间边界 |
| PollInterval | 00:00:01 | 无新事件或读取失败后的等待时间 |
| RetryInterval | 00:00:01 | 当前事件失败方法的重试间隔 |
| CleanupInterval | 00:01:00 | 历史清理间隔 |
| BatchSize | 100 | 每批读取条数，范围为 1 到 1000 |

## 启动、重连与关闭

框架在 IHostedLifecycleService.StartingAsync 中建立起点，等所有托管服务的 StartAsync 完成后开始消费。建立起点之后、初始化期间发布的事件会在初始化完成后处理。建立起点失败时会记录错误并重试，宿主等待后端恢复后继续启动

需要初始化本地业务状态时，应在托管服务的 StartAsync 阶段完成并等待初始化结束；不要将初始化放入脱离启动阶段的后台任务，也不要在 StartAsync 中等待事件监听函数执行完成。所有 StartedAsync 回调之间不承诺业务初始化顺序，因此不要在该阶段再初始化监听依赖的状态

读取失败不会清空游标，连接恢复后继续读取。正常关闭时取消监听令牌并等待后台任务；直接释放宿主或启动失败后释放宿主时也会请求取消后台循环。监听函数应配合取消，不自行启动脱离框架的后台任务

如果两次成功读取的间隔超过 Retention，会记录可能存在历史缺口的警告；不提供超长离线完整恢复

## 监听异常和消费顺序

同一实例按后端记录顺序处理事件，每条事件的相关方法依次调用。即使前一个方法失败，本轮仍尝试该事件的其他方法

当前事件中已经成功的方法会在内存中保留进度，只重试失败的方法。所有相关方法成功后推进游标，再处理下一事件；因此失败事件会阻塞当前实例后续事件，不影响其他进程

失败方法每隔 RetryInterval 重试，直到成功、进程退出或事件超过 Retention。超过保留范围时记录警告并跳过尚未完成的方法，继续后续事件。未知事件类型没有本地监听方法时跳过，不根据远端类型名称加载任意类型

重复执行仍可能发生，例如处理已产生副作用但随后抛出异常，因此不能把进程内进度当成严格只执行一次保证

## Redis Streams 后端

复用宿主现有 IConnectionMultiplexer，使用 eventbus:<Scope> 作为事件流键，各进程独立维护内存读取位置，不使用竞争消费组

需要 Redis 6.2 或更高版本。事件流按 Retention 清理历史记录，Redis 连接由宿主管理

服务端持久化、容灾和时间配置由 Redis 部署负责。客户端取消等待或连接中断可能发生在实际写入已经完成之后，此时发布结果不确定，调用方不能假定失败就绝对没有保存

## 数据库后端

数据库后端、事件实体和映射集中在 Infrastructure/EventBus.Database，复用宿主已有的 IDbContextFactory<TContext>，不会创建独立上下文或另一套数据库配置。Repository 只需引用该类库，并在已有 OnModelCreating 中调用一次 ConfigureEventBus

内置 DatabaseContext 已接入映射。其他上下文接入时，在原有模型配置中增加：

~~~csharp
using EventBus.Database;
using Microsoft.EntityFrameworkCore;

protected override void OnModelCreating(ModelBuilder modelBuilder)
{

    modelBuilder.ConfigureEventBus();
    // 继续执行当前上下文已有的业务映射

}
~~~

宿主先按原有方式注册主库上下文工厂，再选择该类型：

~~~csharp
using EventBus.Database;
using Repository;

builder.Services.AddDatabaseEventBus<DatabaseContext>(options =>
{
    options.Scope = "NetEngine:Production";
    options.Retention = TimeSpan.FromHours(1);
});
~~~

上下文工厂原有的 Provider、拦截器、模型定制、上下文池及 PostgreSQL 分区支持会继续生效。复用分区支持不表示事件表自动分区；事件表是否声明分区需要另行按结构和需求判断

内置三个宿主选择 Database 时使用 DatabaseContext 的主库工厂，不使用 ReadDatabaseContext 读取事件。启动位置和补收查询需要及时看到已提交事件，不能使用允许复制延迟的只读副本

数据库提供程序沿用主库上下文工厂的配置，无需为广播组件单独配置连接

需要的表：

| 表 | 用途 |
|---|---|
| EventBusStream | 每个 Scope 的已提交位置 |
| EventBusMessage | Scope、提交位置、事件标识、类型名、JSON 参数和发布时间 |

清理会物理删除过期事件，保留范围状态和最新位置

事件和范围位置在同一事务中提交，按提交顺序读取。发布遇到并发冲突时会自动重试，重试失败后向调用方抛出异常

启用 Database 前，需要通过已有 Repository.Tool 迁移链路创建这两张表。宿主启动不会自动建表；缺少表时会记录错误并等待结构准备完成

## 扩展其他后端

实现 IEventTransport，并调用 AddEventBus<TTransport> 接入公共运行逻辑。后端负责独立保存事件、建立已提交起点、按可靠位置读取以及物理清理历史

EventEnvelope 表达事件标识、名称和 JSON 参数，EventRecord 补充后端位置与保存时间。位置使用不透明字符串，由对应后端解释，业务无需理解 Stream ID 或数据库序号

## 参考资料

- [Redis Streams 官方文档](https://redis.io/docs/latest/develop/data-types/streams/)
- [Redis XTRIM 时间裁剪](https://redis.io/docs/latest/commands/xtrim/)
- [EF Core 并发令牌](https://learn.microsoft.com/en-us/ef/core/saving/concurrency)
- [EF Core 事务](https://learn.microsoft.com/en-us/ef/core/saving/transactions)
