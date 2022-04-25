using Common.RedisLock;
using Common.RedisLock.Core;
using Hangfire;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;
using System;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using TaskAdmin.Filters;
using TaskAdmin.Libraries;

namespace TaskAdmin
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Common.EnvironmentHelper.ChangeDirectory(args);

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
            Repository.Database.DatabaseContext.ConnectionString = builder.Configuration.GetConnectionString("dbConnection");
            builder.Services.AddDbContextPool<Repository.Database.DatabaseContext>(options => { }, 100);


            builder.Services.AddSingleton<IDistributedLockProvider>(new RedisDistributedSynchronizationProvider(ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("redisConnection")).GetDatabase()));
            builder.Services.AddSingleton<IDistributedSemaphoreProvider>(new RedisDistributedSynchronizationProvider(ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("redisConnection")).GetDatabase()));
            builder.Services.AddSingleton<IDistributedReaderWriterLockProvider>(new RedisDistributedSynchronizationProvider(ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("redisConnection")).GetDatabase()));

            builder.Services.AddHsts(options =>
            {
                options.MaxAge = TimeSpan.FromDays(365);
            });

            builder.Services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                //options.KnownProxies.Add(IPAddress.Parse("10.0.0.100"));
            });



            builder.Services.AddControllersWithViews();



            //注册 HangFire(Memory)
            builder.Services.AddHangfire(configuration => configuration.UseInMemoryStorage());


            //注册 HangFire(Redis)
            //builder.Services.AddHangfire(options => options.UseRedisStorage(builder.Configuration.GetConnectionString("dbConnection")));


            //注册 HangFire(SqlServer)
            //builder.Services.AddHangfire(options => options
            //    .UseSqlServerStorage(builder.Configuration.GetConnectionString("dbConnection"), new SqlServerStorageOptions
            //    {
            //        SchemaName = "hangfire"
            //    }));


            //注册 HangFire(PostgreSQL)
            //builder.Services.AddHangfire(options => options
            //    .UsePostgreSqlStorage(builder.Configuration.GetConnectionString("dbConnection"), new PostgreSqlStorageOptions
            //    {
            //        SchemaName = "hangfire"
            //    }));


            //注册 HangFire(MySql)
            //builder.Services.AddHangfire(options => options
            //    .UseStorage(new MySqlStorage(builder.Configuration.GetConnectionString("dbConnection") + "Allow User Variables=True", new MySqlStorageOptions
            //    {
            //        TablesPrefix = "hangfire_"
            //    })));



            // 注册 HangFire 服务
            builder.Services.AddHangfireServer(options => options.SchedulePollingInterval = TimeSpan.FromSeconds(3));



            //身份认证模块配置
            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            }).AddCookie(options =>
            {
                options.LoginPath = new PathString("/User/Login");
                options.AccessDeniedPath = new PathString("/User/Login");
                options.ExpireTimeSpan = TimeSpan.FromHours(20);
            });

            //身份权限校验模块配置
            //builder.Services.AddAuthorization(options =>
            //{
            //    options.DefaultPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().RequireAssertion(context => IdentityVerification.Authorization(context)).Build();
            //});


            //注册HttpContext
            builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();


            //注册全局过滤器
            builder.Services.AddMvc(config => config.Filters.Add(new GlobalFilter()));


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
                    var errors = actionContext.ModelState.Where(e => e.Value!.Errors.Count > 0).Select(e => e.Value!.Errors.First().ErrorMessage).ToList();

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
                options.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/95.0.4638.69 Safari/537.36");
                options.DefaultRequestHeaders.Add("Accept-Language", "zh-CN,zh;q=0.9");
            }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = false
            });


            builder.Services.AddHttpClient("SkipSsl", options =>
            {
                options.DefaultRequestVersion = new Version("2.0");
                options.DefaultRequestHeaders.Add("Accept", "*/*");
                options.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/95.0.4638.69 Safari/537.36");
                options.DefaultRequestHeaders.Add("Accept-Language", "zh-CN,zh;q=0.9");
            }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = false,
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => { return true; }
            });


            builder.Services.AddHttpClient("xxx.com", options =>
            {
                options.DefaultRequestVersion = new Version("2.0");
                options.DefaultRequestHeaders.Add("Accept", "*/*");
                options.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/95.0.4638.69 Safari/537.36");
                options.DefaultRequestHeaders.Add("Accept-Language", "zh-CN,zh;q=0.9");
            }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = false,
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) =>
                {
                    return string.Equals(cert?.Thumbprint, "xxxxxx", StringComparison.OrdinalIgnoreCase);
                }
            });

            var app = builder.Build();

            ServiceProvider = app.Services;

            app.UseForwardedHeaders();


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


            app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

            Tasks.Main.Run();

            app.Run();

        }


        public static IServiceProvider ServiceProvider { get; set; }
    }
}
