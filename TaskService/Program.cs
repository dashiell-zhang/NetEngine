using Common;
using DistributedLock.Redis;
using Logger.DataBase;
using Microsoft.EntityFrameworkCore;

namespace TaskService
{
    class Program
    {
        static void Main(string[] args)
        {
            EnvironmentHelper.ChangeDirectory(args);

            IHost host = Host.CreateDefaultBuilder(args).UseWindowsService()
                .ConfigureServices((hostContext, services) =>
                {

                    services.AddDbContextPool<Repository.Database.DatabaseContext>(options =>
                    {
                        options.UseNpgsql(hostContext.Configuration.GetConnectionString("dbConnection"));
                    }, 30);


                    services.BatchRegisterServices();

                    #region 注册短信服务

                    //services.AddTencentCloudSMS(options =>
                    //{
                    //    var settings = hostContext.Configuration.GetSection("TencentCloudSMS").Get<SMS.TencentCloud.Models.SMSSetting>();
                    //    options.AppId = settings.AppId;
                    //    options.SecretId = settings.SecretId;
                    //    options.SecretKey = settings.SecretKey;
                    //});


                    //services.AddAliCloudSMS(options =>
                    //{
                    //    var settings = hostContext.Configuration.GetSection("AliCloudSMS").Get<SMS.AliCloud.Models.SMSSetting>();
                    //    options.AccessKeyId = settings.AccessKeyId;
                    //    options.AccessKeySecret = settings.AccessKeySecret;
                    //});
                    #endregion

                    #region 注册文件服务


                    //services.AddTencentCloudStorage(options =>
                    //{
                    //    var settings = hostContext.Configuration.GetSection("TencentCloudFileStorage").Get<FileStorage.TencentCloud.Models.FileStorageSetting>();
                    //    options.AppId = settings.AppId;
                    //    options.Region = settings.Region;
                    //    options.SecretId = settings.SecretId;
                    //    options.SecretKey = settings.SecretKey;
                    //    options.BucketName = settings.BucketName;
                    //});


                    //services.AddAliCloudStorage(options =>
                    //{
                    //    var settings = hostContext.Configuration.GetSection("AliCloudFileStorage").Get<FileStorage.AliCloud.Models.FileStorageSetting>();
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
                        options.Configuration = hostContext.Configuration.GetConnectionString("redisConnection");
                        options.InstanceName = "lock";
                    });


                    //注册缓存服务 Redis模式
                    services.AddStackExchangeRedisCache(options =>
                    {
                        options.Configuration = hostContext.Configuration.GetConnectionString("redisConnection");
                        options.InstanceName = "cache";
                    });


                    #region 注册HttpClient

                    services.AddHttpClient("", options =>
                    {
                        options.DefaultRequestVersion = new("2.0");
                    }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                    {
                        AllowAutoRedirect = false
                    });


                    services.AddHttpClient("SkipSsl", options =>
                    {
                        options.DefaultRequestVersion = new("2.0");
                    }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                    {
                        AllowAutoRedirect = false,
                        ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => { return true; }
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
