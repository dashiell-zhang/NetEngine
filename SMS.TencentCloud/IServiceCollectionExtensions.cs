using SMS;
using SMS.TencentCloud;
using SMS.TencentCloud.Models;

namespace Microsoft.Extensions.DependencyInjection
{

    public static class ServiceCollectionExtensions
    {

        public static void AddTencentCloudSMS(this IServiceCollection services, Action<SMSSetting> action)
        {
            services.Configure(action);

            services.AddTransient<ISMS, TencentCloudSMS>();
        }
    }
}