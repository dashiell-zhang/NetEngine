using global::QueueTask.Database;
using Repository.Core.Extensions;
using Common;
using DistributedLock.Redis;
using EventBus;
using EventBus.Database;
using EventBus.Redis;
using FileStorage.AliCloud;
using IdentifierGenerator;
using Logger.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using NetEngine.Generated;
using Npgsql;
using Repository;
using Repository.Core.Partitioning;
using SMS.AliCloud;
using StackExchange.Redis;
using System.Reflection;
using TaskService.Core;
using TaskService.Core.QueueTask;
using TaskService.Core.ScheduleTask;

namespace TaskService;

/// <summary>
/// 队列任务和定时任务宿主入口
/// </summary>
class Program
{
    /// <summary>
    /// 启动队列任务和定时任务宿主
    /// </summary>
    /// <param name="args">命令行参数</param>
    static void Main(string[] args)
    {

        ThreadPool.GetMinThreads(out int minWorkerThreads, out int minCompletionPortThreads);
        ThreadPool.SetMinThreads(minWorkerThreads * 8, minCompletionPortThreads);

        EnvironmentHelper.ChangeDirectory(args);

        List<Type>? initSingletonServiceTypes = [];

        IHost host = Host.CreateDefaultBuilder(args).UseWindowsService()
            .ConfigureLogging((hostContext, builder) =>
            {
                //注册数据库日志服务
                builder.AddDatabaseLogger(hostContext.Configuration.GetConnectionString("logConnection") ?? hostContext.Configuration.GetConnectionString("dbConnection")!, hostContext.Configuration["DatabaseIdentity:Log"] ?? "NetEngine.Log", options => hostContext.Configuration.GetSection("DatabaseLogger").Bind(options), partitionLockKey: "NetEngine.Log");

                //注册本地文件日志服务
                //builder.AddLocalFileLogger(options => { });
            })
            .ConfigureServices((hostContext, services) =>
            {

                var connectionString = hostContext.Configuration.GetConnectionString("dbConnection");
                // 启动时读取一次写库分区目录，池化上下文和工厂共用只读快照
                var partitionSnapshot = PartitionTableSnapshot.Load(connectionString!);
                services.AddPostgreSqlContext<DatabaseContext>(connectionString!, snapshot: partitionSnapshot, migrationHistoryTable: DatabaseContext.MigrationHistoryTable);
                services.AddQueueTaskDatabase<DatabaseContext>();
                var readConnectionString = hostContext.Configuration.GetConnectionString("dbReadConnection");
                services.AddPostgreSqlContext<ReadDatabaseContext>(string.IsNullOrWhiteSpace(readConnectionString) ? connectionString! : readConnectionString, readOnly: true, snapshot: partitionSnapshot, migrationHistoryTable: DatabaseContext.MigrationHistoryTable);

                services.BatchRegisterServices();
                services.BatchRegisterBackgroundServices();
                services.AddPartitionMaintenance<DatabaseContext>(hostContext.Configuration["DatabaseIdentity:Business"] ?? "NetEngine.Business");


                services.AddTaskWorker<DatabaseContext>(Assembly.GetExecutingAssembly().GetTypes().Where(type => type.IsSubclassOf(typeof(TaskBase)) && !type.IsAbstract && type.IsPublic));

                #region 注册短信服务

                //services.AddTencentCloudSMS(options =>
                //{
                //    var settings = hostContext.Configuration.GetRequiredSection("TencentCloudSMS").Get<SMS.TencentCloud.Models.SMSSetting>()!;
                //    options.AppId = settings.AppId;
                //    options.SecretId = settings.SecretId;
                //    options.SecretKey = settings.SecretKey;
                //});


                services.AddAliCloudSMS(options =>
                {
                    var settings = hostContext.Configuration.GetRequiredSection("AliCloudSMS").Get<SMS.AliCloud.Models.SMSSetting>()!;
                    options.AccessKeyId = settings.AccessKeyId;
                    options.AccessKeySecret = settings.AccessKeySecret;
                });
                #endregion

                #region 注册文件服务
                //services.AddTencentCloudStorage(options =>
                //{
                //    var settings = hostContext.Configuration.GetRequiredSection("TencentCloudFileStorage").Get<FileStorage.TencentCloud.Models.FileStorageSetting>()!;
                //    options.AppId = settings.AppId;
                //    options.Region = settings.Region;
                //    options.SecretId = settings.SecretId;
                //    options.SecretKey = settings.SecretKey;
                //    options.BucketName = settings.BucketName;
                //    options.Url = hostContext.Configuration.GetValue<string>("FileServerUrl")!;
                //});

                services.AddAliCloudStorage(options =>
                {
                    var settings = hostContext.Configuration.GetRequiredSection("AliCloudFileStorage").Get<FileStorage.AliCloud.Models.FileStorageSetting>()!;
                    options.Region = settings.Region;
                    options.UseInternalEndpoint = settings.UseInternalEndpoint;
                    options.AccessKeyId = settings.AccessKeyId;
                    options.AccessKeySecret = settings.AccessKeySecret;
                    options.BucketName = settings.BucketName;
                    options.Url = hostContext.Configuration.GetValue<string>("FileServerUrl")!;
                });
                #endregion


                //注册分布式锁 Redis模式
                services.AddRedisLock(options =>
                {
                    options.Configuration = hostContext.Configuration.GetConnectionString("redisConnection")!;
                    options.InstanceName = "lock";
                });


                //注册缓存服务 Redis模式
                services.AddStackExchangeRedisCache(options =>
                {
                    options.Configuration = hostContext.Configuration.GetConnectionString("redisConnection") ?? throw new InvalidOperationException("缺少业务 Redis 配置");
                    options.InstanceName = "cache";
                });


                //注册混合缓存服务
                services.AddHybridCache(options =>
                {
                    options.MaximumPayloadBytes = 1024 * 1024 * 4;
                    options.MaximumKeyLength = 1024;
                    options.DefaultEntryOptions = new HybridCacheEntryOptions
                    {
                        Expiration = TimeSpan.FromSeconds(300),
                        LocalCacheExpiration = TimeSpan.FromSeconds(60)
                    };
                }).AddSerializerFactory<HybridCacheJsonSerializerFactory>(); ;


                //注册Id生成器
                services.AddRedisLock("NetEngine.Log", options =>
                {
                    options.Configuration = hostContext.Configuration.GetConnectionString("logRedisConnection") ?? hostContext.Configuration.GetConnectionString("idRedisConnection") ?? hostContext.Configuration.GetConnectionString("redisConnection")!;
                    options.InstanceName = "NetEngine.Log.Maintenance";
                });
                services.AddIdentifierGenerator(options =>
                {
                    options.RedisConfiguration = hostContext.Configuration.GetConnectionString("idRedisConnection") ?? hostContext.Configuration.GetConnectionString("redisConnection")!;
                });



                // 注册各进程独立消费的广播事件，默认后端为 Redis Streams
                var eventBusSection = hostContext.Configuration.GetRequiredSection("EventBus");
                switch (eventBusSection.GetValue<EventBusProvider>("Provider"))
                {
                    case EventBusProvider.Redis:
                        services.AddRedisEventBus(hostContext.Configuration.GetConnectionString("eventBusRedisConnection") ?? hostContext.Configuration.GetConnectionString("redisConnection")!, options => eventBusSection.Bind(options));
                        break;
                    case EventBusProvider.Database:
                        services.AddDatabaseEventBus(hostContext.Configuration.GetConnectionString("eventBusConnection") ?? connectionString!, options => eventBusSection.Bind(options));
                        break;
                    default:
                        throw new InvalidOperationException("不支持的 EventBus.Provider");
                }


                #region 注册HttpClient

                services.AddHttpClient("", options =>
                {
                    options.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
                }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                {
                    AllowAutoRedirect = false,
                    AutomaticDecompression = System.Net.DecompressionMethods.All,
                    UseCookies = false
                });

                #endregion

                initSingletonServiceTypes = [.. services.Where(t => t.Lifetime == ServiceLifetime.Singleton && t.ServiceType.ContainsGenericParameters == false).Select(t => t.ServiceType)];
            })
            .Build();

        host.Start();

        //初始化所有不包含开放泛型的单例服务
        initSingletonServiceTypes.ForEach(t => host.Services.GetService(t));
        initSingletonServiceTypes = null;

#if DEBUG

        try
        {
            host.Services.WaitForTaskInitializationAsync(CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("InitTaskBackgroundService 初始化失败：" + ex);
            return;
        }

        var queueMethodList = QueueTaskBuilder.queueMethodList;

        var scheduleMethodList = ScheduleTaskBuilder.scheduleMethodList;

    StartActionTag:

        int indexNo = 1;
        Console.WriteLine();

        Dictionary<int, (string, string)> actionList = [];

        foreach (var item in queueMethodList)
        {
            string actionStatus = item.Value.IsEnable ? "已启动" : "";

            if (item.Value.IsEnable)
            {
                Console.ForegroundColor = ConsoleColor.Green;
            }

            Console.WriteLine($"[{indexNo}] " + "队列任务：" + item.Key + " " + actionStatus);

            actionList.Add(indexNo, ("队列任务", item.Key));
            indexNo++;

            Console.ResetColor();
        }

        foreach (var item in scheduleMethodList)
        {
            string actionStatus = item.Value.IsEnable ? "已启动" : "";

            if (item.Value.IsEnable)
            {
                Console.ForegroundColor = ConsoleColor.Green;
            }

            Console.WriteLine($"[{indexNo}] " + "定时任务：" + item.Key + " " + actionStatus);
            actionList.Add(indexNo, ("定时任务", item.Key));
            indexNo++;

            Console.ResetColor();
        }

        Console.WriteLine();
        Console.WriteLine("请选择要启用的服务，输入序号回车即可（支持空格分割一次输入多个序号）");

        var startIndexNoStr = Console.ReadLine();

        if (startIndexNoStr != null)
        {
            try
            {
                foreach (var startIndexNo in startIndexNoStr.Split(" "))
                {
                    var startActionName = actionList.GetValueOrDefault(int.Parse(startIndexNo));

                    if (startActionName != default)
                    {

                        if (startActionName.Item1 == "队列任务")
                        {
                            var actionInfo = queueMethodList.Where(t => t.Key == startActionName.Item2).First();
                            actionInfo.Value.IsEnable = true;
                        }
                        else
                        {
                            var actionInfo = scheduleMethodList.Where(t => t.Key == startActionName.Item2).First();
                            actionInfo.Value.IsEnable = true;
                        }
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("无效的服务序号：" + startIndexNo);
                        Console.ResetColor();
                    }
                }
            }
            catch
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("无效的服务序号：" + startIndexNoStr);
                Console.ResetColor();
            }

            goto StartActionTag;
        }

#endif
        host.WaitForShutdown();

    }

}
