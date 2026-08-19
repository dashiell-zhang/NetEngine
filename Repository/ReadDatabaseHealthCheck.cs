using Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Repository;

/// <summary>
/// 检查数据库读取连接是否可用
/// </summary>
public class ReadDatabaseHealthCheck(ReadDatabaseContext db, ILogger<ReadDatabaseHealthCheck> logger) : IHealthCheck
{

    /// <summary>
    /// 执行数据库读取连接健康检查
    /// </summary>
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {

        try
        {
            var isHealthy = await db.User.Select(it => new { it.Id }).FirstOrDefaultAsync(cancellationToken);

            return HealthCheckResult.Healthy("A healthy result.");
        }
        catch (Exception ex)
        {

            var errorLog = new
            {
                ex.Source,
                ex.Message,
                ex.StackTrace,
                InnerSource = ex.InnerException?.Source,
                InnerMessage = ex.InnerException?.Message,
                InnerStackTrace = ex.InnerException?.StackTrace,
            };

            logger.LogError(JsonHelper.ObjectToJson(errorLog));
            return new HealthCheckResult(context.Registration.FailureStatus, "An unhealthy result.");
        }

    }

}
