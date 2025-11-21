using Common;
using DistributedLock.Redis;
using FileStorage.AliCloud;
using IdentifierGenerator;
using Logger.DataBase;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Npgsql;
using Repository.Interceptors;
using SMS.AliCloud;
using StackExchange.Redis;
using System.Reflection;
using TaskService.Core;
using TaskService.Core.QueueTask;
using TaskService.Core.ScheduleTask;
using NetEngine.Generated;

namespace TaskService
{
    class Program
    {
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
                    builder.AddDataBaseLogger(options => { });

                    //注册本地文件日志服务
                    //builder.AddLocalFileLogger(options => { });
                })
                .ConfigureServices((hostContext, services) =>
                {

                    var connectionString = hostContext.Configuration.GetConnectionString("dbConnection");
                    NpgsqlDataSourceBuilder dataSourceBuilder = new(connectionString);

                    NpgsqlConnectionStringBuilder connectionStringBuilder = new(connectionString);
                    int maxPoolSize = connectionStringBuilder.MaxPoolSize;

                    services.AddSingleton<QueryCountInterceptor>();

                    services.AddDbContextPool<Repository.Database.DatabaseContext>((serviceProvider, options) =>
                    {
                        options.UseNpgsql(dataSourceBuilder.Build());
                        options.AddInterceptors(new PostgresPatchInterceptor());
                        options.AddInterceptors(serviceProvider.GetRequiredService<QueryCountInterceptor>());

                    }, maxPoolSize);

                    services.AddPooledDbContextFactory<Repository.Database.DatabaseContext>(options => { }, maxPoolSize);

                    services.BatchRegisterServices();


                    //注册所有 TaskBase的子类
                    Assembly.GetExecutingAssembly().GetTypes().Where(t => typeof(TaskBase).IsAssignableFrom(t) && t.IsClass && !t.IsAbstract && t.IsPublic).ToList().ForEach(t =>
                    {
                        services.AddScoped(t);
                    });


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
                        options.Configuration = hostContext.Configuration.GetConnectionString("redisConnection");
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
                    services.AddIdentifierGenerator();


                    //注册 Redis 驱动
                    services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(hostContext.Configuration.GetConnectionString("redisConnection")!));


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
}
