using Common;
using DistributedLock.Redis;
using Logger.DataBase;
using Microsoft.EntityFrameworkCore;
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
                        options.UseNpgsql(hostContext.Configuration.GetConnectionString("dbConnection")!);
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
                        AutomaticDecompression = System.Net.DecompressionMethods.All
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

            host.Run();

        }



    }

}
