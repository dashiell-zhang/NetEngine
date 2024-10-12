using FileStorage.AliCloud.Models;
using Microsoft.Extensions.DependencyInjection;

namespace FileStorage.AliCloud
{

    public static class ServiceCollectionExtensions
    {

        public static void AddAliCloudStorage(this IServiceCollection services, Action<FileStorageSetting> action)
        {
            services.Configure(action);
            services.AddTransient<IFileStorage, AliCloudStorage>();
        }
    }
}