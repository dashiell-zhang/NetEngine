using Hangfire;
using Medallion.Threading;
using Medallion.Threading.SqlServer;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using TaskAdmin.Filters;
using TaskAdmin.Libraries;
using TaskAdmin.Subscribes;

namespace TaskAdmin
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Common.EnvironmentHelper.ChangeDirectory(args);
            Common.EnvironmentHelper.InitTestServer();

            var builder = WebApplication.CreateBuilder(args);

            //启用 Kestrel Https 并绑定证书
            //builder.WebHost.UseKestrel(options =>
            //{
            //    options.ConfigureHttpsDefaults(options =>
            //    {
            //        options.ServerCertificate = new System.Security.Cryptography.X509Certificates.X509Certificate2(Path.Combine(AppContext.BaseDirectory, "xxxx.pfx"), "123456");
            //    });
            //});
            //builder.WebHost.UseUrls("https://*");

            // Add services to the container.

            //为各数据库注入连接字符串
            Repository.Database.dbContext.ConnectionString = builder.Configuration.GetConnectionString("dbConnection");
            builder.Services.AddDbContextPool<Repository.Database.dbContext>(options => { }, 100);


            builder.Services.AddSingleton<IDistributedLockProvider>(new SqlDistributedSynchronizationProvider(builder.Configuration.GetConnectionString("dbConnection")));
            builder.Services.AddSingleton<IDistributedSemaphoreProvider>(new SqlDistributedSynchronizationProvider(builder.Configuration.GetConnectionString("dbConnection")));
            builder.Services.AddSingleton<IDistributedUpgradeableReaderWriterLockProvider>(new SqlDistributedSynchronizationProvider(builder.Configuration.GetConnectionString("dbConnection")));


            builder.Services.AddResponseCompression();

            builder.Services.AddSingleton<DemoSubscribe>();
            builder.Services.AddCap(options =>
            {

                //使用 Redis 传输消息
                options.UseRedis(builder.Configuration.GetConnectionString("redisConnection"));

                //var rabbitMQSetting = builder.Configuration.GetSection("RabbitMQSetting").Get<RabbitMQSetting>();

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


            builder.Services.AddHsts(options =>
            {
                options.MaxAge = TimeSpan.FromDays(365);
            });


            //注册 HangFire(Memory)
            builder.Services.AddHangfire(configuration => configuration.UseInMemoryStorage());


            //注册 HangFire(Redis)
            //builder.Services.AddHangfire(options => options.UseRedisStorage(builder.Configuration.GetConnectionString("hangfireConnection")));


            //注册 HangFire(SqlServer)
            //builder.Services.AddHangfire(options => options
            //    .UseSqlServerStorage(builder.Configuration.GetConnectionString("hangfireConnection"), new SqlServerStorageOptions
            //    {
            //        SchemaName = "hangfire"
            //    }));


            //注册 HangFire(PostgreSQL)
            //builder.Services.AddHangfire(options => options
            //    .UsePostgreSqlStorage(builder.Configuration.GetConnectionString("hangfireConnection"), new PostgreSqlStorageOptions
            //    {
            //        SchemaName = "hangfire"
            //    }));


            //注册 HangFire(MySql)
            //builder.Services.AddHangfire(options => options
            //    .UseStorage(new MySqlStorage(builder.Configuration.GetConnectionString("hangfireConnection") + "Allow User Variables=True", new MySqlStorageOptions
            //    {
            //        TablesPrefix = "hangfire_"
            //    })));



            // 注册 HangFire 服务
            builder.Services.AddHangfireServer(options => options.SchedulePollingInterval = TimeSpan.FromSeconds(3));



            builder.Services.AddControllersWithViews();


            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            }).AddCookie(options =>
            {
                options.LoginPath = new PathString("/User/Login/");
                options.AccessDeniedPath = new PathString("/User/Login/");
                options.ExpireTimeSpan = TimeSpan.FromHours(20);
            });


            //builder.Services.AddAuthorization(options =>
            //{
            //    options.DefaultPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().RequireAssertion(context => IdentityVerification.Authorization(context)).Build();
            //});


            //注册HttpContext
            builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();


            //注册全局过滤器
            builder.Services.AddMvc(config => config.Filters.Add(new GlobalFilter()));



            //托管Session到Redis中
            if (Convert.ToBoolean(builder.Configuration["SessionToRedis"]))
            {
                builder.Services.AddDistributedRedisCache(options =>
                {
                    options.Configuration = builder.Configuration.GetConnectionString("redisConnection");
                });
            }


            //注册Session
            builder.Services.AddSession(options =>
            {
                //设置Session过期时间
                options.IdleTimeout = TimeSpan.FromHours(3);
            });


            //解决中文被编码
            builder.Services.AddSingleton(HtmlEncoder.Create(UnicodeRanges.All));


            //注册统一模型验证
            builder.Services.Configure<ApiBehaviorOptions>(options =>
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
            builder.Services.AddSingleton(new Common.SnowflakeHelper(0, 0));


            //注册缓存服务 内存模式
            builder.Services.AddDistributedMemoryCache();


            //注册缓存服务 SqlServer模式
            //builder.Services.AddDistributedSqlServerCache(options =>
            //{
            //    options.ConnectionString = builder.Configuration.GetConnectionString("dbConnection");
            //    options.SchemaName = "dbo";
            //    options.TableName = "t_cache";
            //});


            //注册缓存服务 Redis模式
            //builder.Services.AddStackExchangeRedisCache(options =>
            //{
            //    options.Configuration = builder.Configuration.GetConnectionString("redisConnection");
            //    options.InstanceName = "cache";
            //});

            builder.Services.AddHttpClient("", options =>
            {
                options.DefaultRequestVersion = new Version("2.0");
                options.DefaultRequestHeaders.Add("Accept", "*/*");
                options.DefaultRequestHeaders.Add("UserAgent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/93.0.4577.82 Safari/537.36");
                options.DefaultRequestHeaders.Add("Accept-Language", "zh-CN,zh;q=0.9");
            }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = false
            });


            builder.Services.AddHttpClient("SkipSsl", options =>
            {
                options.DefaultRequestVersion = new Version("2.0");
                options.DefaultRequestHeaders.Add("Accept", "*/*");
                options.DefaultRequestHeaders.Add("UserAgent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/93.0.4577.82 Safari/537.36");
                options.DefaultRequestHeaders.Add("Accept-Language", "zh-CN,zh;q=0.9");
            }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = false,
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => { return true; }
            });

            var app = builder.Build();

            ServiceProvider = app.Services;

            app.UseResponseCompression();

            //设置本地化信息，可实现 固定 Hangfire 管理面板为中文显示
            app.UseRequestLocalization(new RequestLocalizationOptions
            {
                DefaultRequestCulture = new RequestCulture("zh-CN"),
                SupportedCultures = new[]
                {
                    new CultureInfo("zh-CN")
                },
                SupportedUICultures = new[]
                {
                    new CultureInfo("zh-CN")
                }
            });



            //开启倒带模式运行多次读取HttpContext.Body中的内容
            app.Use(async (context, next) =>
            {
                context.Request.EnableBuffering();
                await next.Invoke();
            });


            if (app.Environment.IsDevelopment())
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


            //注册Session
            app.UseSession();


            app.UseRouting();


            app.UseAuthentication();
            app.UseAuthorization();


            app.UseHangfireDashboard("/hangfire", new DashboardOptions
            {
                Authorization = new[] { new DashboardAuthorizationFilter() },
                DisplayStorageConnectionString = false
            });


            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
            });

            Tasks.Main.Run();


            app.Run();

        }




        public static IServiceProvider ServiceProvider { get; set; }
    }
}
