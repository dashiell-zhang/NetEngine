using Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Repository.HealthCheck;
public class DatabaseHealthCheck(DatabaseContext db, ILogger<DatabaseHealthCheck> logger) : IHealthCheck
{
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
