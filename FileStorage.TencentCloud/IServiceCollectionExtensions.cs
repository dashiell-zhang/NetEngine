using FileStorage;
using FileStorage.TencentCloud;
using FileStorage.TencentCloud.Models;

namespace Microsoft.Extensions.DependencyInjection
{

    public static class ServiceCollectionExtensions
    {

        public static void AddTencentCloudStorage(this IServiceCollection services, Action<FileStorageSetting> action)
        {
            services.Configure(action);
            services.AddTransient<IFileStorage, TencentCloudStorage>();
        }
    }
}