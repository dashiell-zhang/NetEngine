using DistributedLock;
using DistributedLock.Redis;
using DistributedLock.Redis.Models;
using System;

namespace Microsoft.Extensions.DependencyInjection
{

    public static class ServiceCollectionExtensions
    {

        public static void AddRedisLock(this IServiceCollection services, Action<RedisSetting> action)
        {
            services.Configure(action);
            services.AddSingleton<IDistributedLock, RedisLock>();

        }
    }
}