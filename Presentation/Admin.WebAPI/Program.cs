using Common;
using DistributedLock.Redis;
using IdentifierGenerator;
using Logger.DataBase;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Npgsql;
using Repository.Interceptors;
using StackExchange.Redis;
using WebAPI.Core.Extensions;
using NetEngine.Generated;

namespace Admin.WebAPI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            ThreadPool.GetMinThreads(out int minWorkerThreads, out int minCompletionPortThreads);
            ThreadPool.SetMinThreads(minWorkerThreads * 8, minCompletionPortThreads);

            EnvironmentHelper.ChangeDirectory(args);

            var builder = WebApplication.CreateBuilder(args);

            builder.SetKestrelConfig();

            builder.Host.UseWindowsService();

            var connectionString = builder.Configuration.GetConnectionString("dbConnection");
            NpgsqlDataSourceBuilder dataSourceBuilder = new(connectionString);

            NpgsqlConnectionStringBuilder connectionStringBuilder = new(connectionString);
            int maxPoolSize = connectionStringBuilder.MaxPoolSize;

            builder.Services.AddSingleton<QueryCountInterceptor>();

            builder.Services.AddDbContextPool<Repository.Database.DatabaseContext>((serviceProvider, options) =>
            {
                options.UseNpgsql(dataSourceBuilder.Build());
                options.AddInterceptors(new PostgresPatchInterceptor());
                options.AddInterceptors(serviceProvider.GetRequiredService<QueryCountInterceptor>());

            }, maxPoolSize);

            builder.Services.AddPooledDbContextFactory<Repository.Database.DatabaseContext>(options => { }, maxPoolSize);


            builder.AddCommonServices();

            builder.Services.BatchRegisterServices();
            builder.Services.BatchRegisterBackgroundServices();


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


            //注册混合缓存服务
            builder.Services.AddHybridCache(options =>
            {
                options.MaximumPayloadBytes = 1024 * 1024 * 4;
                options.MaximumKeyLength = 1024;
                options.DefaultEntryOptions = new HybridCacheEntryOptions
                {
                    Expiration = TimeSpan.FromSeconds(300),
                    LocalCacheExpiration = TimeSpan.FromSeconds(60)
                };
            }).AddSerializerFactory<HybridCacheJsonSerializerFactory>(); ;


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

            #region 注册文件服务
            //builder.Services.AddTencentCloudStorage(options =>
            //{
            //    var settings = builder.Configuration.GetRequiredSection("TencentCloudFileStorage").Get<FileStorage.TencentCloud.Models.FileStorageSetting>()!;
            //    options.AppId = settings.AppId;
            //    options.Region = settings.Region;
            //    options.SecretId = settings.SecretId;
            //    options.SecretKey = settings.SecretKey;
            //    options.BucketName = settings.BucketName;
            //    options.Url = builder.Configuration.GetValue<string>("FileServerUrl")!;
            //});

            //builder.Services.AddAliCloudStorage(options =>
            //{
            //    var settings = builder.Configuration.GetRequiredSection("AliCloudFileStorage").Get<FileStorage.AliCloud.Models.FileStorageSetting>()!;
            //    options.Region = settings.Region;
            //    options.UseInternalEndpoint = settings.UseInternalEndpoint;
            //    options.AccessKeyId = settings.AccessKeyId;
            //    options.AccessKeySecret = settings.AccessKeySecret;
            //    options.BucketName = settings.BucketName;
            //    options.Url = builder.Configuration.GetValue<string>("FileServerUrl")!;
            //});
            #endregion

            #region 注册日志服务

            //注册数据库日志服务
            builder.Logging.AddDataBaseLogger(options => { });

            //注册本地文件日志服务
            //builder.Logging.AddLocalFileLogger(options => { });

            #endregion


            var app = builder.Build();

            app.UseCommonMiddleware();

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

            app.InitSingletonService(builder.Services);

            app.ShowDocUrl();

            app.WaitForShutdown();

        }


    }
}
