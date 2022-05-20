using Common;
using Common.DistributedLock;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace TaskService.Libraries
{
    public class TaskCore : BackgroundService
    {

        public readonly IDistributedLock distLock;
        public readonly SnowflakeHelper snowflakeHelper;

        public TaskCore()
        {
            distLock = Program.ServiceProvider.GetRequiredService<IDistributedLock>();
            snowflakeHelper = Program.ServiceProvider.GetRequiredService<SnowflakeHelper>();
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            throw new NotImplementedException();
        }
    }
}
