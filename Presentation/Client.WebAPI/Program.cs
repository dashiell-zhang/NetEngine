using global::QueueTask.Database;
using Repository.Core;
using Logger.Database.Persistence;
using EventBus.Database.Persistence;
using WebAPI.Core.Libraries.HealthCheck;
using Repository.Core.Extensions;
using Client.WebAPI.Libraries.HttpHandler;
using Common;
using DistributedLock.Redis;
using EventBus;
using EventBus.Database;
using EventBus.Redis;
using IdentifierGenerator;
using LLM;
using Logger.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using NetEngine.Generated;
using Npgsql;
using Repository;
using Repository.Core.Partitioning;
using StackExchange.Redis;
using System.Security.Cryptography.X509Certificates;
using WebAPI.Core.Extensions;

namespace Client.WebAPI;

/// <summary>
/// 配置客户端 API 宿主及数据库启动快照
/// </summary>
public class Program
{
    /// <summary>
    /// 初始化基础设施并启动客户端 API
    /// </summary>
    public static void Main(string[] args)
    {

        ThreadPool.GetMinThreads(out int minWorkerThreads, out int minCompletionPortThreads);
        ThreadPool.SetMinThreads(minWorkerThreads * 8, minCompletionPortThreads);

        EnvironmentHelper.ChangeDirectory(args);

        var builder = WebApplication.CreateBuilder(args);

        builder.SetKestrelConfig();

        builder.Host.UseWindowsService();


        var connectionString = builder.Configuration.GetConnectionString("dbConnection");
        // 启动时读取一次写库分区目录，池化上下文和工厂共用只读快照
        var partitionSnapshot = PartitionTableSnapshot.Load(connectionString!);
        builder.Services.AddPostgreSqlContext<DatabaseContext>(connectionString!, snapshot: partitionSnapshot, migrationHistoryTable: DatabaseContext.MigrationHistoryTable);
        builder.Services.AddQueueTaskDatabase<DatabaseContext>();
        var readConnectionString = builder.Configuration.GetConnectionString("dbReadConnection");
        builder.Services.AddPostgreSqlContext<ReadDatabaseContext>(string.IsNullOrWhiteSpace(readConnectionString) ? connectionString! : readConnectionString, readOnly: true, snapshot: partitionSnapshot, migrationHistoryTable: DatabaseContext.MigrationHistoryTable);


        builder.AddCommonServices();
        builder.Services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck<DatabaseContext>>("Business.Write")
            .AddCheck<DatabaseHealthCheck<ReadDatabaseContext>>("Business.Read")
            .AddCheck<DatabaseHealthCheck<LogDatabaseContext>>("Log.Write")
            .AddCheck<CacheHealthCheck>("Business.Cache");

        builder.Services.BatchRegisterServices();
        builder.Services.BatchRegisterBackgroundServices();
        builder.Services.AddPartitionMaintenance<DatabaseContext>(builder.Configuration["DatabaseIdentity:Business"] ?? "NetEngine.Business");


        builder.Services.AddLlmClientFactory();

        //注册Id生成器
        builder.Services.AddRedisLock("NetEngine.Log", options =>
        {
            options.Configuration = builder.Configuration.GetConnectionString("logRedisConnection") ?? builder.Configuration.GetConnectionString("idRedisConnection") ?? builder.Configuration.GetConnectionString("redisConnection")!;
            options.InstanceName = "NetEngine.Log.Maintenance";
        });
        builder.Services.AddIdentifierGenerator(options =>
        {
            options.RedisConfiguration = builder.Configuration.GetConnectionString("idRedisConnection") ?? builder.Configuration.GetConnectionString("redisConnection")!;
        });


        //注册分布式锁 Redis模式
        builder.Services.AddRedisLock(options =>
        {
            options.Configuration = builder.Configuration.GetConnectionString("redisConnection")!;
            options.InstanceName = "lock";
        });


        //注册缓存服务 Redis模式
        builder.Services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = builder.Configuration.GetConnectionString("redisConnection") ?? throw new InvalidOperationException("缺少业务 Redis 配置");
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
        }).AddSerializerFactory<HybridCacheJsonSerializerFactory>();



        // 注册各进程独立消费的广播事件，默认后端为 Redis Streams
        var eventBusSection = builder.Configuration.GetRequiredSection("EventBus");
        switch (eventBusSection.GetValue<EventBusProvider>("Provider"))
        {
            case EventBusProvider.Redis:
                builder.Services.AddHealthChecks().AddCheck<RedisEventHealthCheck>("EventBus");
                builder.Services.AddRedisEventBus(builder.Configuration.GetConnectionString("eventBusRedisConnection") ?? builder.Configuration.GetConnectionString("redisConnection")!, options => eventBusSection.Bind(options));
                break;
            case EventBusProvider.Database:
                builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck<EventBusDatabaseContext>>("EventBus");
                builder.Services.AddDatabaseEventBus(builder.Configuration.GetConnectionString("eventBusConnection") ?? connectionString!, options => eventBusSection.Bind(options));
                break;
            default:
                throw new InvalidOperationException("不支持的 EventBus.Provider");
        }


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
            using X509Certificate2 certificate = X509CertificateLoader.LoadPkcs12FromFile(sslPath, "证书密码", X509KeyStorageFlags.MachineKeySet);
            handler.ClientCertificates.Add(certificate);
            return handler;
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
        var logConnection = builder.Configuration.GetConnectionString("logConnection") ?? connectionString!;
        builder.Logging.AddDatabaseLogger(logConnection, builder.Configuration["DatabaseIdentity:Log"] ?? "NetEngine.Log", options => builder.Configuration.GetSection("DatabaseLogger").Bind(options), partitionLockKey: "NetEngine.Log");

        //注册本地文件日志服务
        //builder.Logging.AddLocalFileLogger(options => { });

        #endregion


        var app = builder.Build();

        app.UseCommonMiddleware();

        app.MapControllers();

        app.MapHealthChecks("/healthz");

        app.Start();

        app.InitSingletonService(builder.Services);

        app.ShowDocUrl();

        app.WaitForShutdown();

    }


}
