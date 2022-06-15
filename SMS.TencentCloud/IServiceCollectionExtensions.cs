using SMS;
using SMS.TencentCloud;
using SMS.TencentCloud.Models;
using System;

namespace Microsoft.Extensions.DependencyInjection
{

    public static class ServiceCollectionExtensions
    {

        public static void AddTencentCloudSMS(this IServiceCollection services, Action<SMSSetting> action)
        {
            SMSSetting setting = new();
            action(setting);

            services.AddSingleton<ISMS>(new TencentCloudSMS(setting.AppId, setting.SecretId, setting.SecretKey));
        }
    }
}