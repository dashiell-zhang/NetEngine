using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyModel;
using Microsoft.Extensions.Hosting;
using System.Reflection;

namespace Common
{
    public static class IServiceCollectionExtension
    {

        public static void BatchRegisterServices(this IServiceCollection services)
        {
            var allAssembly = GetAllAssembly();

            services.RegisterServiceByAttribute(ServiceLifetime.Singleton, allAssembly);
            services.RegisterServiceByAttribute(ServiceLifetime.Scoped, allAssembly);
            services.RegisterServiceByAttribute(ServiceLifetime.Transient, allAssembly);

            services.RegisterBackgroundService(allAssembly);
        }



        /// <summary>
        /// 通过 ServiceAttribute 批量注册服务
        /// </summary>
        /// <param name="services"></param>
        /// <param name="serviceLifetime"></param>
        private static void RegisterServiceByAttribute(this IServiceCollection services, ServiceLifetime serviceLifetime, List<Assembly> allAssembly)
        {

            List<Type> types = allAssembly.SelectMany(t => t.GetTypes()).Where(t => t.GetCustomAttributes(typeof(ServiceAttribute), false).Length > 0 && t.GetCustomAttribute<ServiceAttribute>()?.Lifetime == serviceLifetime && t.IsClass && !t.IsAbstract).ToList();

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




        /// <summary>
        /// 注册后台服务
        /// </summary>
        /// <param name="services"></param>
        /// <param name="serviceLifetime"></param>
        private static void RegisterBackgroundService(this IServiceCollection services, List<Assembly> allAssembly)
        {

            List<Type> types = allAssembly.SelectMany(t => t.GetTypes()).Where(t => typeof(BackgroundService).IsAssignableFrom(t) && t.IsClass && !t.IsAbstract).ToList();

            foreach (var type in types)
            {
                services.AddSingleton(typeof(IHostedService), type);
            }
        }



        /// <summary>
        /// 获取全部 Assembly
        /// </summary>
        /// <remarks>注意：单文件发布模式下会存在缺失</remarks>
        /// <returns></returns>
        private static List<Assembly> GetAllAssembly()
        {
            var allAssemblies = AppDomain.CurrentDomain.GetAssemblies().ToList();

            HashSet<string> loadedAssemblies = new();

            foreach (var item in allAssemblies)
            {
                loadedAssemblies.Add(item.GetName().Name!);
            }

            Queue<Assembly> assembliesToCheck = new();
            assembliesToCheck.Enqueue(Assembly.GetEntryAssembly()!);

            while (assembliesToCheck.Any())
            {
                var assemblyToCheck = assembliesToCheck.Dequeue();
                foreach (var reference in assemblyToCheck!.GetReferencedAssemblies())
                {
                    if (!loadedAssemblies.Contains(reference.Name!))
                    {
                        var assembly = Assembly.Load(reference);

                        assembliesToCheck.Enqueue(assembly);

                        loadedAssemblies.Add(reference.Name!);

                        allAssemblies.Add(assembly);
                    }
                }
            }

            var runtimeLibraryNameList = DependencyContext.Default?.RuntimeLibraries.Select(o => o.Name).ToList();
            if (runtimeLibraryNameList != null)
            {
                foreach (var runtimeLibraryName in runtimeLibraryNameList)
                {
                    try
                    {
                        if (!loadedAssemblies.Contains(runtimeLibraryName))
                        {
                            var assembly = Assembly.Load(runtimeLibraryName);

                            loadedAssemblies.Add(runtimeLibraryName);

                            allAssemblies.Add(assembly);
                        }
                    }
                    catch
                    {
                    }
                }
            }

            return allAssemblies;
        }

    }



    [AttributeUsage(AttributeTargets.Class)]
    public class ServiceAttribute : Attribute
    {
        public ServiceLifetime Lifetime { get; set; } = ServiceLifetime.Transient;

    }
}
