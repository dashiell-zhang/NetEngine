using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AdminApi.Attributes;

namespace AdminApi.Libraries
{
    public static class IServiceCollectionExtension
    {

        public static void AddCustomServices(this IServiceCollection services)
        {
            services.RegisterServiceByAttribute(ServiceLifetime.Singleton);
            services.RegisterServiceByAttribute(ServiceLifetime.Scoped);
            services.RegisterServiceByAttribute(ServiceLifetime.Transient);
        }



        /// <summary>
        /// 通过 
        /// </summary>
        /// <param name="services"></param>
        /// <param name="serviceLifetime"></param>
        private static void RegisterServiceByAttribute(this IServiceCollection services, ServiceLifetime serviceLifetime)
        {
            List<Type> types = AppDomain.CurrentDomain.GetAssemblies().SelectMany(t => t.GetTypes()).Where(t => t.GetCustomAttributes(typeof(ServiceAttribute), false).Length > 0 && t.GetCustomAttribute<ServiceAttribute>()?.Lifetime == serviceLifetime && t.IsClass && !t.IsAbstract).ToList();

            foreach (var type in types)
            {

                Type? typeInterface = type.GetInterfaces().FirstOrDefault();

                if (typeInterface == null)
                {
                    //服务非继承自接口的直接注入
                    switch (serviceLifetime)
                    {
                        case ServiceLifetime.Singleton: services.AddSingleton(type); break;
                        case ServiceLifetime.Scoped: services.AddScoped(type); break;
                        case ServiceLifetime.Transient: services.AddTransient(type); break;
                    }
                }
                else
                {
                    //服务继承自接口的和接口一起注入
                    switch (serviceLifetime)
                    {
                        case ServiceLifetime.Singleton: services.AddSingleton(typeInterface, type); break;
                        case ServiceLifetime.Scoped: services.AddScoped(typeInterface, type); break;
                        case ServiceLifetime.Transient: services.AddTransient(typeInterface, type); break;
                    }
                }

            }
        }


    }
}
