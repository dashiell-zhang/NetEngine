using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;

namespace Cms.Libraries
{
    public static class HttpContext
    {
        private static IServiceProvider ServiceProvider { get; set; }

        private static IHostingEnvironment HostingEnvironment { get; set; }


        public static void Add(IServiceCollection services)
        {
            services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        }


        public static void Initialize(IApplicationBuilder app, IHostingEnvironment env)
        {
            ServiceProvider = app.ApplicationServices;
            HostingEnvironment = env;
        }


        public static Microsoft.AspNetCore.Http.HttpContext Current()
        {
            object factory = ServiceProvider.GetService(typeof(Microsoft.AspNetCore.Http.IHttpContextAccessor));
            Microsoft.AspNetCore.Http.HttpContext context = ((IHttpContextAccessor)factory).HttpContext;
            return context;
        }


        /// <summary>
        /// 获取Url信息
        /// </summary>
        /// <returns></returns>
        public static string GetUrl()
        {
            return $"{HttpContext.Current().Request.Scheme}://{HttpContext.Current().Request.Host.Host}{HttpContext.Current().Request.Path}{HttpContext.Current().Request.QueryString}";
        }
    }
}
