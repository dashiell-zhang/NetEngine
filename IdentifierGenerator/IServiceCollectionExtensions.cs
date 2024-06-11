using IdentifierGenerator.Models;
using IdentifierGenerator.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace IdentifierGenerator
{

    public static class ServiceCollectionExtensions
    {


        public static void AddIdentifierGenerator(this IServiceCollection services, Action<IdSetting>? action = null)
        {
            var idSetting = new IdSetting();

            if (action != null)
            {
                services.Configure(action);
                action(idSetting);
            }

            services.AddSingleton<IdService>();

            if (idSetting.DataCenterId == null || idSetting.MachineId == null)
            {
                services.AddHostedService<RefreshSignTask>();
            }
        }

    }
}