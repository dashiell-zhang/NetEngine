using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using System.IO;

namespace Methods.Http
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



        /// <summary>
        /// 获取Body信息
        /// </summary>
        /// <returns></returns>
        public static string GetBody()
        {
            try
            {
                Current().Request.Body.Seek(0, 0);

                var requestReader = new StreamReader(Current().Request.Body);

                var requestContent = requestReader.ReadToEnd();

                return requestContent;
            }
            catch
            {
                return "";
            }
        }
    }
}
