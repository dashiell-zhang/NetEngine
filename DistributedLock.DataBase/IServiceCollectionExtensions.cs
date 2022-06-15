using DistributedLock;
using DistributedLock.DataBase;

namespace Microsoft.Extensions.DependencyInjection
{

    public static class ServiceCollectionExtensions
    {

        public static void AddDataBaseLock(this IServiceCollection services)
        {

            services.AddScoped<IDistributedLock, DataBaseLock>();

        }
    }
}