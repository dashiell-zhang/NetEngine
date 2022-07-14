using FileStorage;
using FileStorage.AliCloud;
using FileStorage.AliCloud.Models;

namespace Microsoft.Extensions.DependencyInjection
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