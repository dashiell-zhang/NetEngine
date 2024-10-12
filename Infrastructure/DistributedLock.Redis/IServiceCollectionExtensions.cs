using DistributedLock.Redis.Models;
using Microsoft.Extensions.DependencyInjection;

namespace DistributedLock.Redis
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