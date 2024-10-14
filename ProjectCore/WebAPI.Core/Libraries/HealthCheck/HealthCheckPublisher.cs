using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace WebAPI.Core.Libraries.HealthCheck
{
    public class HealthCheckPublisher : IHealthCheckPublisher
    {
        public Task PublishAsync(HealthReport report, CancellationToken cancellationToken)
        {
            if (report.Status == HealthStatus.Healthy)
            {
            }
            else
            {
            }

            return Task.CompletedTask;
        }
    }
}
