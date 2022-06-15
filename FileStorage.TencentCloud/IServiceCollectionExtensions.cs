using FileStorage;
using FileStorage.TencentCloud;
using FileStorage.TencentCloud.Models;
using System;

namespace Microsoft.Extensions.DependencyInjection
{

    public static class ServiceCollectionExtensions
    {

        public static void AddTencentCloudStorage(this IServiceCollection services, Action<StorageSetting> action)
        {
            StorageSetting storageSetting = new();
            action(storageSetting);
            services.AddSingleton<IFileStorage>(new TencentCloudStorage(storageSetting.AppId, storageSetting.Region, storageSetting.SecretId, storageSetting.SecretKey, storageSetting.BucketName));
        }
    }
}