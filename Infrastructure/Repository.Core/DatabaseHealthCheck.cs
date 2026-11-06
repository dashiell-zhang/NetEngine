using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Repository.Core;

/// <summary>
/// 检查选定数据库连接，不查询业务表或修改数据库结构
/// </summary>
public sealed class DatabaseHealthCheck<TContext>(IDbContextFactory<TContext> factory) : IHealthCheck where TContext : DbContext
{

    /// <summary>
    /// 使用独立上下文检查当前目标的连接可用性
    /// </summary>
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {

        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("数据库连接不可用");

    }

}
