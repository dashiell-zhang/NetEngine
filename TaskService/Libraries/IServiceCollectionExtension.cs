using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TaskService.Libraries
{
    public static class IServiceCollectionExtension
    {

        public static void AddCustomServices(this IServiceCollection services)
        {
            services.RegisterBackgroundService();
        }



        /// <summary>
        /// 注册后台服务
        /// </summary>
        /// <param name="services"></param>
        /// <param name="serviceLifetime"></param>
        private static void RegisterBackgroundService(this IServiceCollection services)
        {
            List<Type> types = AppDomain.CurrentDomain.GetAssemblies().SelectMany(t => t.GetTypes()).Where(t => typeof(BackgroundService).IsAssignableFrom(t) && t.IsClass && !t.IsAbstract).ToList();

            foreach (var type in types)
            {
                services.AddSingleton(typeof(IHostedService), type);
            }
        }


    }
}
