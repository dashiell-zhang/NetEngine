using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Repository.Database;

namespace Repository.HealthCheck
{
    public class DatabaseHealthCheck(DatabaseContext db) : IHealthCheck
    {
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                var isHealthy = await db.TUser.Select(it => new { it.Id }).FirstOrDefaultAsync();

                return HealthCheckResult.Healthy("A healthy result.");
            }
            catch
            {
                return new HealthCheckResult(context.Registration.FailureStatus, "An unhealthy result.");
            }
        }
    }
}
