using Microsoft.Extensions.DependencyInjection;
using SMS.AliCloud.Models;

namespace SMS.AliCloud
{

    public static class ServiceCollectionExtensions
    {

        public static void AddAliCloudSMS(this IServiceCollection services, Action<SMSSetting> action)
        {
            services.Configure(action);

            services.AddTransient<ISMS, AliCloudSMS>();

        }
    }
}