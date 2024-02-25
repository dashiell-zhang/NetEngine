using Common;
using IdentifierGenerator;
using IdentifierGenerator.Models;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;


namespace IdentifierGenerator.Tasks
{

    internal class RefreshSignTask(IServiceProvider serviceProvider, IdService idService, IOptionsMonitor<IdSetting> config) : BackgroundService
    {

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            idService.GetId();

            while (!stoppingToken.IsCancellationRequested && config.CurrentValue.IsAuto)
            {
                try
                {

                    using var scope = serviceProvider.CreateScope();
                    var distributedCache = scope.ServiceProvider.GetRequiredService<IDistributedCache>();

                    var key = "IdentifierGenerator-" + config.CurrentValue.DataCenterId + ":" + config.CurrentValue.MachineId;

                    distributedCache.Set(key, "", TimeSpan.FromDays(7));
                }
                catch
                {
                }

                await Task.Delay(1000 * 60 * 60 * 24, stoppingToken);
            }

        }

    }
}
