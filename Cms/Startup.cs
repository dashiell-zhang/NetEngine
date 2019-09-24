using Cms.Filters;
using Cms.Libraries;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Text.Encodings.Web;
using System.Text.Unicode;

namespace Cms
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

            services.AddControllersWithViews();


            //注册HttpContext
            Methods.Http.HttpContext.Add(services);

            //注册全局过滤器
            services.AddMvc(config => config.Filters.Add(new GlobalFiler()));

            //注册跨域信息
            services.AddCors(option =>
            {
                option.AddPolicy("cors", policy =>
                {
                    policy.SetIsOriginAllowed(origin => true)
                       .AllowAnyHeader()
                       .AllowAnyMethod()
                       .AllowCredentials();
                });
            });

            //注册配置文件信息
            Methods.Start.StartConfiguration.Add(Configuration);

            //注册Session
            services.AddSession();


            //调整Json操作类库为 NewtonsoftJson ,并设置默认时间格式，需要安装 Microsoft.AspNetCore.Mvc.NewtonsoftJson
            services.AddControllers().AddNewtonsoftJson(options =>
            {
                options.SerializerSettings.DateFormatString = "yyyy-MM-dd HH:mm:ss";
            });


            //解决中文被编码
            services.AddSingleton(HtmlEncoder.Create(UnicodeRanges.All));
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
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
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }


            //强制重定向到Https
            app.UseHttpsRedirection();


            app.UseStaticFiles();


            //注册HttpContext
            Methods.Http.HttpContext.Initialize(app, env);

            //注册跨域信息
            app.UseCors("cors");
            //注册Session
            app.UseSession();


            //注册HostingEnvironment
            Methods.Start.StartHostingEnvironment.Add(env);


            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "areas",
                    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
            });


        }
    }
}
