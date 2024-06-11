using Microsoft.Extensions.Diagnostics.HealthChecks;
using Repository.Database;

namespace Repository.HealthCheck
{
    public class DatabaseHealthCheck(DatabaseContext db) : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            var isHealthy = db.TUser.Select(it => new { it.Id }).FirstOrDefault() != null;

            if (isHealthy)
            {
                return Task.FromResult(
                    HealthCheckResult.Healthy("A healthy result."));
            }

            return Task.FromResult(
                new HealthCheckResult(
                    context.Registration.FailureStatus, "An unhealthy result."));
        }
    }
}
