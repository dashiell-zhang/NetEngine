using Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;

namespace TaskService
{
    class Program
    {
        static void Main(string[] args)
        {
            //EnvironmentHelper.ChangeDirectory(args);

            IHost host = Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {

                    services.AddDbContextPool<Repository.Database.DatabaseContext>(options =>
                    {
                        options.UseSqlServer(hostContext.Configuration.GetConnectionString("dbConnection"));
                    }, 100);


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
                    services.AddSingleton(new SnowflakeHelper(0, 0));

                    #region 注册分布式锁

                    //注册分布式锁 Redis模式
                    //services.AddRedisLock(options =>
                    //{
                    //    options.Configuration = hostContext.Configuration.GetConnectionString("redisConnection");
                    //    options.InstanceName = "lock";
                    //});


                    //注册分布式锁 数据库模式
                    services.AddDataBaseLock();

                    #endregion

                    #region 注册缓存服务

                    //注册缓存服务 内存模式
                    services.AddDistributedMemoryCache();


                    //注册缓存服务 SqlServer模式
                    //services.AddDistributedSqlServerCache(options =>
                    //{
                    //    options.ConnectionString = hostContext.Configuration.GetConnectionString("dbConnection");
                    //    options.SchemaName = "dbo";
                    //    options.TableName = "t_cache";
                    //});


                    //注册缓存服务 Redis模式
                    //services.AddStackExchangeRedisCache(options =>
                    //{
                    //    options.Configuration = hostContext.Configuration.GetConnectionString("redisConnection");
                    //    options.InstanceName = "cache";
                    //});

                    #endregion

                    #region 注册HttpClient

                    services.AddHttpClient("", options =>
                    {
                        options.DefaultRequestVersion = new Version("2.0");
                    }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                    {
                        AllowAutoRedirect = false
                    });


                    services.AddHttpClient("SkipSsl", options =>
                    {
                        options.DefaultRequestVersion = new Version("2.0");
                    }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                    {
                        AllowAutoRedirect = false,
                        ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => { return true; }
                    });

                    #endregion

                }).ConfigureLogging((hostContext, builder) =>
                {
                    //注册数据库日志服务
                    builder.AddDataBaseLogger(options =>
                    {
                        options.DataBaseConnection = hostContext.Configuration.GetConnectionString("dbConnection");
                    });

                    //注册本地文件日志服务
                    builder.AddLocalFileLogger(options => { });
                })
                .Build();

            host.Run();

        }



    }

}
