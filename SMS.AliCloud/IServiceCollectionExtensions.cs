using SMS;
using SMS.AliCloud;
using SMS.AliCloud.Models;

namespace Microsoft.Extensions.DependencyInjection
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