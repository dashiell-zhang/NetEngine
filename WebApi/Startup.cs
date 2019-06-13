using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.Swagger;
using WebApi.Actions;
using WebApi.Filters;
using PublicMethods = Methods;

namespace WebApi
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {

            //注册HttpContext
            PublicMethods.Http.HttpContext.Add(services);

            //注册全局过滤器
            services.AddMvc(config => config.Filters.Add(new GlobalFiler()));


            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_2);


            //注册跨域信息
            services.AddCors(options => options.AddPolicy("cors", policy => policy.AllowAnyHeader().AllowAnyMethod().AllowCredentials().AllowAnyOrigin()));


            services.AddMvc().AddJsonOptions(options =>
            {
                //设置 Json 默认时间格式
                options.SerializerSettings.DateFormatString = "yyyy-MM-dd hh:mm:ss";
            });

            //注册配置文件信息
            PublicMethods.Start.StartConfiguration.Add(Configuration);


            //注册Swagger生成器，定义一个和多个Swagger 文档
            services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new Info { Title = "WebApi", Version = "v1" });
                var basePath = AppContext.BaseDirectory;
                var xmlPath = Path.Combine(basePath, "WebApi.xml");
                options.IncludeXmlComments(xmlPath, true);
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {

            //注册全局异常处理机制
            app.UseExceptionHandler(builder => builder.Run(async context => await GlobalError.ErrorEvent(context)));

            if (env.IsDevelopment())
            {
                //默认错误输出页面
                app.UseDeveloperExceptionPage();
            }
            else
            {
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            //注册HttpContext
            PublicMethods.Http.HttpContext.Initialize(app, env);


            //注册跨域信息
            app.UseCors("cors");

            app.UseHttpsRedirection();
            app.UseMvc();


            //注册HostingEnvironment
            PublicMethods.Start.StartHostingEnvironment.Add(env);


            //启用中间件服务生成Swagger作为JSON端点
            app.UseSwagger();

            //启用中间件服务对swagger-ui，指定Swagger JSON端点
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/swagger/v1/swagger.json", "WebApi V1");
            });
        }
    }
}
