using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyModel;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Common
{
    public static class IServiceCollectionExtension
    {

        public static void BatchRegisterServices(this IServiceCollection services)
        {
            services.RegisterServiceByAttribute(ServiceLifetime.Singleton);
            services.RegisterServiceByAttribute(ServiceLifetime.Scoped);
            services.RegisterServiceByAttribute(ServiceLifetime.Transient);

            services.RegisterBackgroundService();
        }



        /// <summary>
        /// 通过 ServiceAttribute 批量注册服务
        /// </summary>
        /// <param name="services"></param>
        /// <param name="serviceLifetime"></param>
        private static void RegisterServiceByAttribute(this IServiceCollection services, ServiceLifetime serviceLifetime)
        {
            List<Assembly> assemblies = new();

            var allNames = DependencyContext.Default.RuntimeLibraries.Select(o => o.Name).ToList();

            foreach (var name in allNames)
            {
                try
                {
                    assemblies.Add(Assembly.Load(new AssemblyName(name)));
                }
                catch
                {
                }
            }

            if (assemblies != null)
            {


                List<Type> types = assemblies.SelectMany(t => t.GetTypes()).Where(t => t.GetCustomAttributes(typeof(ServiceAttribute), false).Length > 0 && t.GetCustomAttribute<ServiceAttribute>()?.Lifetime == serviceLifetime && t.IsClass && !t.IsAbstract).ToList();

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




        /// <summary>
        /// 注册后台服务
        /// </summary>
        /// <param name="services"></param>
        /// <param name="serviceLifetime"></param>
        private static void RegisterBackgroundService(this IServiceCollection services)
        {
            var assemblies = Assembly.GetEntryAssembly()?.GetReferencedAssemblies().Select(t => Assembly.Load(t)).ToArray();

            if (assemblies != null)
            {
                List<Type> types = assemblies.SelectMany(t => t.GetTypes()).Where(t => typeof(BackgroundService).IsAssignableFrom(t) && t.IsClass && !t.IsAbstract).ToList();

                foreach (var type in types)
                {
                    services.AddSingleton(typeof(IHostedService), type);
                }
            }
        }


    }



    [AttributeUsage(AttributeTargets.Class)]
    public class ServiceAttribute : Attribute
    {
        public ServiceLifetime Lifetime { get; set; } = ServiceLifetime.Transient;

    }
}
