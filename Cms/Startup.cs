using Cms.Filters;
using Cms.Libraries;
using Cms.Libraries.Verify;
using Cms.Subscribes;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Linq;
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

            //为各数据库注入连接字符串
            Repository.Database.dbContext.ConnectionString = Configuration.GetConnectionString("dbConnection");
            services.AddDbContextPool<Repository.Database.dbContext>(options => { }, 100);


            services.AddSingleton<DemoSubscribe>();
            services.AddCap(options =>
            {

                //使用 Redis 传输消息
                options.UseRedis(Configuration.GetConnectionString("redisConnection"));

                //var rabbitMQSetting = Configuration.GetSection("RabbitMQSetting").Get<RabbitMQSetting>();

                ////使用 RabbitMQ 传输消息
                //options.UseRabbitMQ(options =>
                //{
                //    options.HostName = rabbitMQSetting.HostName;
                //    options.UserName = rabbitMQSetting.UserName;
                //    options.Password = rabbitMQSetting.PassWord;
                //    options.VirtualHost = rabbitMQSetting.VirtualHost;
                //    options.Port = rabbitMQSetting.Port;
                //    options.ConnectionFactoryOptions = options =>
                //    {
                //        options.Ssl = new RabbitMQ.Client.SslOption { Enabled = rabbitMQSetting.Ssl.Enabled, ServerName = rabbitMQSetting.Ssl.ServerName };
                //    };
                //});


                //使用 ef 搭配 db 存储执行情况
                options.UseEntityFramework<Repository.Database.dbContext>();

                options.UseDashboard();
                options.JsonSerializerOptions.Encoder = JavaScriptEncoder.Create(UnicodeRanges.All);

                options.DefaultGroupName = "default";   //默认组名称
                options.GroupNamePrefix = null; //全局组名称前缀
                options.TopicNamePrefix = null; //Topic 统一前缀
                options.Version = "v1";
                options.FailedRetryInterval = 60;   //失败时重试间隔
                options.ConsumerThreadCount = 1;    //消费者线程并行处理消息的线程数，当这个值大于1时，将不能保证消息执行的顺序
                options.FailedRetryCount = 10;  //失败时重试的最大次数
                options.FailedThresholdCallback = null; //重试阈值的失败回调
                options.SucceedMessageExpiredAfter = 24 * 3600; //成功消息的过期时间（秒）
            }).AddSubscribeFilter<CapSubscribeFilter>();


            services.Configure<FormOptions>(options =>
            {
                options.MultipartBodyLengthLimit = long.MaxValue;
            });

            services.Configure<KestrelServerOptions>(options =>
            {
                options.AllowSynchronousIO = true;
            });

            services.Configure<IISServerOptions>(options =>
            {
                options.AllowSynchronousIO = true;
            });

            services.AddHsts(options =>
            {
                options.MaxAge = TimeSpan.FromDays(365);
            });

            services.AddControllersWithViews();


            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            }).AddCookie(options =>
            {
                options.LoginPath = new PathString("/User/Login/");
                options.AccessDeniedPath = new PathString("/User/Login/");
                options.ExpireTimeSpan = TimeSpan.FromHours(20);
            });

            services.AddAuthorization(options =>
            {
                options.DefaultPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().RequireAssertion(context => IdentityVerification.Authorization(context)).Build();
            });


            //注册HttpContext
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();


            //注册全局过滤器
            services.AddMvc(config => config.Filters.Add(new GlobalFilter()));

            //注册跨域信息
            services.AddCors(options =>
            {
                options.AddPolicy("cors", policy =>
                {
                    policy.SetIsOriginAllowed(origin => true)
                       .AllowAnyHeader()
                       .AllowAnyMethod()
                       .AllowCredentials();
                });
            });



            //托管Session到Redis中
            if (Convert.ToBoolean(Configuration["SessionToRedis"]))
            {
                services.AddDistributedRedisCache(options =>
                {
                    options.Configuration = Configuration.GetConnectionString("redisConnection");
                });
            }


            //注册Session
            services.AddSession(options =>
            {
                //设置Session过期时间
                options.IdleTimeout = TimeSpan.FromHours(3);
            });



            services.AddControllers().AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new Common.Json.DateTimeConverter());
                options.JsonSerializerOptions.Converters.Add(new Common.Json.DateTimeNullConverter());
                options.JsonSerializerOptions.Converters.Add(new Common.Json.LongConverter());
            });


            //解决中文被编码
            services.AddSingleton(HtmlEncoder.Create(UnicodeRanges.All));


            //注册统一模型验证
            services.Configure<ApiBehaviorOptions>(options =>
            {
                options.InvalidModelStateResponseFactory = actionContext =>
                {

                    //获取验证失败的模型字段 
                    var errors = actionContext.ModelState.Where(e => e.Value.Errors.Count > 0).Select(e => e.Value.Errors.First().ErrorMessage).ToList();

                    var dataStr = string.Join(" | ", errors);

                    //设置返回内容
                    var result = new
                    {
                        errMsg = dataStr
                    };

                    return new BadRequestObjectResult(result);
                };
            });


            //注册雪花ID算法示例
            services.AddSingleton(new Common.SnowflakeHelper(0, 0));


        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {

            Program.ServiceProvider = app.ApplicationServices;


            //开启倒带模式运行多次读取HttpContext.Body中的内容
            app.Use(next => context =>
            {
                context.Request.EnableBuffering();
                return next(context);
            });


            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                //注册全局异常处理机制
                app.UseExceptionHandler(builder => builder.Run(async context => await GlobalError.ErrorEvent(context)));
            }


            app.UseHsts();


            //强制重定向到Https
            app.UseHttpsRedirection();


            app.UseStaticFiles();


            //注册跨域信息
            app.UseCors("cors");

            //注册Session
            app.UseSession();


            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();


            app.UseEndpoints(options =>
            {
                options.MapControllerRoute(
                    name: "areas",
                    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

                options.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
            });


        }
    }
}
