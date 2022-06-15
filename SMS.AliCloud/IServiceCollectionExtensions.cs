using SMS;
using SMS.AliCloud;
using SMS.AliCloud.Models;
using System;

namespace Microsoft.Extensions.DependencyInjection
{

    public static class ServiceCollectionExtensions
    {

        public static void AddAliCloudSMS(this IServiceCollection services, Action<SMSSetting> action)
        {
            SMSSetting setting = new();
            action(setting);

            services.AddSingleton<ISMS>(new AliCloudSMS(setting.AccessKeyId, setting.AccessKeySecret));

        }
    }
}