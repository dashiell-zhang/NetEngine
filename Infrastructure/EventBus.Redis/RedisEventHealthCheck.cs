using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace EventBus.Redis;

/// <summary>
/// 检查广播实际绑定的 Redis 连接，不发布消息或改变消费位置
/// </summary>
public sealed class RedisEventHealthCheck(Func<IConnectionMultiplexer> getConnection) : IHealthCheck
{

    /// <summary>
    /// 对选定事件目标执行连接探测
    /// </summary>
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {

        cancellationToken.ThrowIfCancellationRequested();
        await getConnection().GetDatabase().PingAsync().WaitAsync(cancellationToken);
        return HealthCheckResult.Healthy();

    }

}
