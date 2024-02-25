using Common;
using IdentifierGenerator.Models;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;


namespace IdentifierGenerator.Tasks
{

    internal class RefreshSignTask(IDistributedCache distributedCache, IdService idService, IOptionsMonitor<IdSetting> config) : BackgroundService
    {

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            idService.GetId();

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
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
