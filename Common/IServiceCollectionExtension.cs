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

            List<Type> types = allAssembly.SelectMany(t => t.GetTypes()).Where(t => typeof(BackgroundService).IsAssignableFrom(t) && t.IsClass && !t.IsAbstract && t.IsPublic).ToList();

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
            List<Assembly> allAssemblies = [];

            var runtimeLibraryNameList = DependencyContext.Default?.RuntimeLibraries.SelectMany(t => t.RuntimeAssemblyGroups.SelectMany(r => r.AssetPaths.Select(a => new FileInfo(a)).Where(f => f.Extension == ".dll").Select(f => f.Name[..^4]))).Distinct().ToList();

            List<string> removeKeyList =
            [
                "Microsoft",
                "System",
                "NPOI"
            ];

            if (runtimeLibraryNameList != null)
            {
                foreach (var removeKey in removeKeyList)
                {
                    runtimeLibraryNameList.RemoveAll(t => t.StartsWith(removeKey));
                }

                foreach (var runtimeLibraryName in runtimeLibraryNameList)
                {
                    var assembly = Assembly.Load(runtimeLibraryName);
                    allAssemblies.Add(assembly);
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
