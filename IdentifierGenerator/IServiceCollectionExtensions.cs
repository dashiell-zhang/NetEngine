using IdentifierGenerator.Models;
using Microsoft.Extensions.DependencyInjection;

namespace IdentifierGenerator
{

    public static class ServiceCollectionExtensions
    {

        public static void AddIdentifierGenerator(this IServiceCollection services, Action<IdSetting> action)
        {
            services.Configure(action);
            services.AddSingleton<IdService>();
        }
    }
}