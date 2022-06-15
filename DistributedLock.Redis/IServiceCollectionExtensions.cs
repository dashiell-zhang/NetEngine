using DistributedLock;
using DistributedLock.Redis;
using DistributedLock.Redis.Models;
using System;

namespace Microsoft.Extensions.DependencyInjection
{

    public static class ServiceCollectionExtensions
    {

        public static void AddRedisLock(this IServiceCollection services, Action<ActionOptions> setupAction)
        {
            ActionOptions actionOptions = new();
            setupAction(actionOptions);
            services.AddSingleton<IDistributedLock>(new RedisLock(actionOptions.RedisConnection));

        }
    }
}