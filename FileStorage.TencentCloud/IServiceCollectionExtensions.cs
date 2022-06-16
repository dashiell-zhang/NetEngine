using FileStorage;
using FileStorage.TencentCloud;
using FileStorage.TencentCloud.Models;
using System;

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