using FileStorage.TencentCloud.Models;
using Microsoft.Extensions.DependencyInjection;

namespace FileStorage.TencentCloud
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