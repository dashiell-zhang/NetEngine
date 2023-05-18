using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Common;

namespace AdminAPI.Libraries.HealthCheck
{
    public class CacheHealthCheck : IHealthCheck
    {
        private readonly IDistributedCache distributedCache;

        public CacheHealthCheck(IDistributedCache distributedCache)
        {
            this.distributedCache = distributedCache;
        }


        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {

            var isHealthy = distributedCache.Set("cacheHealthCheck", "", TimeSpan.FromSeconds(10));

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
