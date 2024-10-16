using Common;
using DistributedLock.Redis;
using IdentifierGenerator;
using Logger.DataBase;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;
using Repository.HealthCheck;
using Repository.Interceptors;
using StackExchange.Redis;
using System.Security.Cryptography.X509Certificates;
using WebAPI.Core.Extensions;
using WebAPI.Core.Libraries.HealthCheck;

namespace Admin.WebAPI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            ThreadPool.SetMinThreads(128, 1);

            EnvironmentHelper.ChangeDirectory(args);

            var builder = WebApplication.CreateBuilder(args);

            builder.WebHost.UseKestrel((context, options) =>
            {
                options.ConfigureHttpsDefaults(options =>
                {
                    var certConf = context.Configuration.GetSection("Kestrel:Certificates:Default");

                    if (certConf.Value != null)
                    {
                        X509Certificate2Collection x509Certificate2s = new();

                        var sslPath = certConf.GetValue<string>("Path")!;

                        if (sslPath.EndsWith("pfx", StringComparison.OrdinalIgnoreCase))
                        {
                            string password = certConf.GetValue<string>("Password")!;

                            x509Certificate2s.Import(sslPath, password);
                            options.ServerCertificateChain = x509Certificate2s;
                        }
                        else if (sslPath.EndsWith("pem", StringComparison.OrdinalIgnoreCase) || sslPath.EndsWith("crt", StringComparison.OrdinalIgnoreCase))
                        {
                            x509Certificate2s.ImportFromPemFile(sslPath);
                            options.ServerCertificateChain = x509Certificate2s;

                        }
                    }

                });
            });

            builder.Host.UseWindowsService();

            var connectionString = builder.Configuration.GetConnectionString("dbConnection");
            NpgsqlDataSourceBuilder dataSourceBuilder = new(connectionString);

            builder.Services.AddDbContextPool<Repository.Database.DatabaseContext>(options =>
            {
                options.UseNpgsql(dataSourceBuilder.Build());
                options.AddInterceptors(new PostgresPatchInterceptor());
            }, 30);

            builder.Services.AddPooledDbContextFactory<Repository.Database.DatabaseContext>(options =>
            {
                options.UseNpgsql(dataSourceBuilder.Build());
                options.AddInterceptors(new PostgresPatchInterceptor());
            }, 30);

            builder.Services.AddCommonServices(builder.Configuration);

            //#region 注册 Json 序列化配置

            //builder.Services.AddControllers().AddJsonOptions(options =>
            //{
            //    options.JsonSerializerOptions.Converters.Add(new Common.JsonConverter.LongConverter());
            //});

            //#endregion



            //注册Id生成器
            builder.Services.AddIdentifierGenerator();


            //注册分布式锁 Redis模式
            builder.Services.AddRedisLock(options =>
            {
                options.Configuration = builder.Configuration.GetConnectionString("redisConnection")!;
                options.InstanceName = "lock";
            });


            //注册缓存服务 Redis模式
            builder.Services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = builder.Configuration.GetConnectionString("redisConnection");
                options.InstanceName = "cache";
            });


            //注册 Redis 驱动
            builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("redisConnection")!));


            #region 注册HttpClient

            builder.Services.AddHttpClient("", options =>
            {
                options.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
            }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = false,
                AutomaticDecompression = System.Net.DecompressionMethods.All,
                UseCookies = false
            });

            #endregion

            builder.Services.BatchRegisterServices();

            #region 注册文件服务
            //builder.Services.AddTencentCloudStorage(options =>
            //{
            //    var settings = builder.Configuration.GetRequiredSection("TencentCloudFileStorage").Get<FileStorage.TencentCloud.Models.FileStorageSetting>()!;
            //    options.AppId = settings.AppId;
            //    options.Region = settings.Region;
            //    options.SecretId = settings.SecretId;
            //    options.SecretKey = settings.SecretKey;
            //    options.BucketName = settings.BucketName;
            //    options.URL = builder.Configuration.GetValue<string>("FileServerURL")!;
            //});

            //builder.Services.AddAliCloudStorage(options =>
            //{
            //    var settings = builder.Configuration.GetRequiredSection("AliCloudFileStorage").Get<FileStorage.AliCloud.Models.FileStorageSetting>()!;
            //    options.Endpoint = settings.Endpoint;
            //    options.AccessKeyId = settings.AccessKeyId;
            //    options.AccessKeySecret = settings.AccessKeySecret;
            //    options.BucketName = settings.BucketName;
            //    options.URL = builder.Configuration.GetValue<string>("FileServerURL")!;
            //});
            #endregion

            #region 注册日志服务

            //注册数据库日志服务
            builder.Logging.AddDataBaseLogger(options => { });

            //注册本地文件日志服务
            //builder.Logging.AddLocalFileLogger(options => { });

            #endregion

            #region 注册健康检测服务
            builder.Services.AddHealthChecks()
                .AddCheck<CacheHealthCheck>("CacheHealthCheck")
                .AddCheck<DatabaseHealthCheck>("DatabaseHealthCheck");


            builder.Services.Configure<HealthCheckPublisherOptions>(options =>
            {
                options.Delay = TimeSpan.FromSeconds(10);
                options.Period = TimeSpan.FromSeconds(60);
            });

            builder.Services.AddSingleton<IHealthCheckPublisher, HealthCheckPublisher>();
            #endregion


            var app = builder.Build();

            app.UseCommonMiddleware(app.Environment);

            app.UseStaticFiles();


            //注入 adminapp 项目
            //app.MapWhen(ctx => ctx.Request.Path.Value.ToLower().Contains("/admin"), adminapp =>
            //{
            //    adminapp.UseStaticFiles("/admin");
            //    adminapp.UseBlazorFrameworkFiles("/admin");

            //    adminapp.UseEndpoints(endpoints =>
            //    {
            //        endpoints.MapFallbackToFile("/admin/{*path:nonfile}", "admin/index.html");
            //    });
            //});

            app.MapControllers();

            app.MapHealthChecks("/healthz");


            app.Start();

            //初始化所有不包含开放泛型的单例服务
            builder.Services.Where(t => t.Lifetime == ServiceLifetime.Singleton && t.ServiceType.ContainsGenericParameters == false).Select(t => t.ServiceType).ToList().ForEach(t => app.Services.GetService(t));
#if DEBUG
            string url = app.Urls.First().Replace("http://[::]", "http://127.0.0.1");
            Console.WriteLine(Environment.NewLine + "Swagger Doc: " + url + "/swagger/" + Environment.NewLine);
#endif
            app.WaitForShutdown();

        }


    }
}
