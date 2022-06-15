using FileStorage;
using FileStorage.AliCloud;
using FileStorage.AliCloud.Models;
using System;

namespace Microsoft.Extensions.DependencyInjection
{

    public static class ServiceCollectionExtensions
    {

        public static void AddAliCloudStorage(this IServiceCollection services, Action<StorageSetting> action)
        {
            StorageSetting storageSetting = new();
            action(storageSetting);
            services.AddSingleton<IFileStorage>(new AliCloudStorage(storageSetting.Endpoint, storageSetting.AccessKeyId, storageSetting.AccessKeySecret, storageSetting.BucketName));
        }
    }
}