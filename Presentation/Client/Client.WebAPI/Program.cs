using Client.WebAPI.Libraries.HttpHandler;
using Common;
using DistributedLock.Redis;
using IdentifierGenerator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;
using Repository.HealthCheck;
using Repository.Interceptors;
using SMS.AliCloud;
using StackExchange.Redis;
using System.Security.Cryptography.X509Certificates;
using WebAPI.Core.Extensions;
using WebAPI.Core.Libraries.HealthCheck;

namespace Client.WebAPI
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

            builder.Services.AddHttpClient("SkipSsl", options =>
            {
                options.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
            }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = false,
                AutomaticDecompression = System.Net.DecompressionMethods.All,
                UseCookies = false,
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => { return true; }
            });

            builder.Services.AddTransient<HttpSignHandler>();
            builder.Services.AddHttpClient("HttpSign", options =>
            {
                options.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
            }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = false,
                AutomaticDecompression = System.Net.DecompressionMethods.All,
                UseCookies = false
            }).AddHttpMessageHandler<HttpSignHandler>();


            builder.Services.AddHttpClient("CarryCert", options =>
            {
                options.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
            }).ConfigurePrimaryHttpMessageHandler(() =>
            {
                using HttpClientHandler handler = new()
                {
                    AllowAutoRedirect = false,
                    AutomaticDecompression = System.Net.DecompressionMethods.All,
                    UseCookies = false
                };
                var sslPath = Path.Combine(Directory.GetCurrentDirectory(), "ssl", "xxxx.p12");
                using X509Certificate2 certificate = new(sslPath, "证书密码", X509KeyStorageFlags.MachineKeySet);
                handler.ClientCertificates.Add(certificate);
                return handler;
            });

            #endregion


            #region 注册短信服务

            //builder.Services.AddTencentCloudSMS(options =>
            //{
            //    var settings = builder.Configuration.GetRequiredSection("TencentCloudSMS").Get<SMS.TencentCloud.Models.SMSSetting>()!;
            //    options.AppId = settings.AppId;
            //    options.SecretId = settings.SecretId;
            //    options.SecretKey = settings.SecretKey;
            //});


            builder.Services.AddAliCloudSMS(options =>
            {
                var settings = builder.Configuration.GetRequiredSection("AliCloudSMS").Get<SMS.AliCloud.Models.SMSSetting>()!;
                options.AccessKeyId = settings.AccessKeyId;
                options.AccessKeySecret = settings.AccessKeySecret;
            });

            #endregion

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
            //builder.Logging.AddDataBaseLogger(options => { });

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
