using Microsoft.Extensions.Diagnostics.HealthChecks;
using Repository.Database;

namespace Repository.HealthCheck
{
    public class DatabaseHealthCheck(DatabaseContext db) : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                var isHealthy = db.TUser.Select(it => new { it.Id }).FirstOrDefault();

                return Task.FromResult(HealthCheckResult.Healthy("A healthy result."));
            }
            catch
            {
                return Task.FromResult(new HealthCheckResult(context.Registration.FailureStatus, "An unhealthy result."));
            }
        }
    }
}
