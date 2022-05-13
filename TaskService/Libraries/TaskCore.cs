using Common;
using Common.RedisLock.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace TaskService.Libraries
{
    public class TaskCore : BackgroundService
    {

        public readonly IDistributedLockProvider distLock;
        public readonly IDistributedSemaphoreProvider distSemaphoreLock;
        public readonly IDistributedReaderWriterLockProvider distReaderWriterLock;
        public readonly SnowflakeHelper snowflakeHelper;

        public TaskCore()
        {
            distLock = Program.ServiceProvider.GetRequiredService<IDistributedLockProvider>();
            distSemaphoreLock = Program.ServiceProvider.GetRequiredService<IDistributedSemaphoreProvider>();
            distReaderWriterLock = Program.ServiceProvider.GetRequiredService<IDistributedReaderWriterLockProvider>();
            snowflakeHelper = Program.ServiceProvider.GetRequiredService<SnowflakeHelper>();
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            throw new NotImplementedException();
        }
    }
}
