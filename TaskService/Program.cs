using Common;
using DistributedLock.Redis;
using Logger.DataBase;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using StackExchange.Redis;

namespace TaskService
{
    class Program
    {
        static void Main(string[] args)
        {
            ThreadPool.SetMinThreads(128, 1);

            EnvironmentHelper.ChangeDirectory(args);

            IHost host = Host.CreateDefaultBuilder(args).UseWindowsService()
                .ConfigureServices((hostContext, services) =>
                {

                    services.AddDbContextPool<Repository.Database.DatabaseContext>(options =>
                    {
                        var connectionString = hostContext.Configuration.GetConnectionString("dbConnection");

                        NpgsqlDataSourceBuilder dataSourceBuilder = new(connectionString);

                        options.UseNpgsql(dataSourceBuilder.Build());
                  
                    }, 30);


                    services.BatchRegisterServices();

                    #region 注册短信服务

                    //services.AddTencentCloudSMS(options =>
                    //{
                    //    var settings = hostContext.Configuration.GetRequiredSection("TencentCloudSMS").Get<SMS.TencentCloud.Models.SMSSetting>()!;
                    //    options.AppId = settings.AppId;
                    //    options.SecretId = settings.SecretId;
                    //    options.SecretKey = settings.SecretKey;
                    //});


                    //services.AddAliCloudSMS(options =>
                    //{
                    //    var settings = hostContext.Configuration.GetRequiredSection("AliCloudSMS").Get<SMS.AliCloud.Models.SMSSetting>()!;
                    //    options.AccessKeyId = settings.AccessKeyId;
                    //    options.AccessKeySecret = settings.AccessKeySecret;
                    //});
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
                    //});


                    //services.AddAliCloudStorage(options =>
                    //{
                    //    var settings = hostContext.Configuration.GetRequiredSection("AliCloudFileStorage").Get<FileStorage.AliCloud.Models.FileStorageSetting>()!;
                    //    options.Endpoint = settings.Endpoint;
                    //    options.AccessKeyId = settings.AccessKeyId;
                    //    options.AccessKeySecret = settings.AccessKeySecret;
                    //    options.BucketName = settings.BucketName;
                    //});

                    #endregion


                    //注册雪花ID算法
                    services.AddSingleton(new IDHelper(0, 0));


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

                }).ConfigureLogging((hostContext, builder) =>
                {
                    //注册数据库日志服务
                    builder.AddDataBaseLogger(options => { });

                    //注册本地文件日志服务
                    //builder.AddLocalFileLogger(options => { });
                })
                .Build();

            host.Start();
#if DEBUG

            var queueMethodList = Libraries.QueueTask.QueueTaskBuilder.queueMethodList;

            var scheduleMethodList = Libraries.ScheduleTask.ScheduleTaskBuilder.scheduleMethodList;

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
                string actionStatus = item.IsEnable ? "已启动" : "";

                if (item.IsEnable)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                }

                Console.WriteLine($"[{indexNo}] " + "定时任务：" + item.Method.Name + " " + actionStatus);
                actionList.Add(indexNo, ("定时任务", item.Method.Name));
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
                                var actionInfo = scheduleMethodList.Where(t => t.Method.Name == startActionName.Item2).First();
                                actionInfo.IsEnable = true;
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
