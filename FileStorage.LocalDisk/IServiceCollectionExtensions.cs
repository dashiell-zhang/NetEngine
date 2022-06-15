using FileStorage;
using FileStorage.LocalDisk;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.DependencyInjection
{

    public static class ServiceCollectionExtensions
    {

        public static void AddLocalDiskStorage(this IServiceCollection services)
        {
            services.AddTransient<IFileStorage, LocalDiskStorage>();
        }
    }
}