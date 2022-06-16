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

                    //注册腾讯云短信服务
                    //var tencentCloudSMSSetting = hostContext.Configuration.GetSection("TencentCloudSMS").Get<TencentCloudSMSSetting>();
                    //services.AddSingleton<ISMS>(new TencentCloudSMS(tencentCloudSMSSetting.AppId, tencentCloudSMSSetting.SecretId, tencentCloudSMSSetting.SecretKey));


                    //注册阿里云短信服务
                    //var aliCloudSMSSetting = hostContext.Configuration.GetSection("AliCloudSMS").Get<AliCloudSMSSetting>();
                    //services.AddSingleton<ISMS>(new AliCloudSMS(aliCloudSMSSetting.AccessKeyId, aliCloudSMSSetting.AccessKeySecret));

                    #endregion

                    #region 注册文件服务


                    ////注册腾讯云COS文件服务
                    //services.AddTencentCloudStorage(options =>
                    //{
                    //    options = hostContext.Configuration.GetSection("TencentCloudFileStorage").Get<FileStorage.TencentCloud.Models.FileStorageSetting>();
                    //});


                    ////注册阿里云OSS文件服务
                    //services.AddAliCloudStorage(options =>
                    //{
                    //    options = hostContext.Configuration.GetSection("AliCloudFileStorage").Get<FileStorage.AliCloud.Models.FileStorageSetting>();
                    //});

                    #endregion


                    //注册雪花ID算法
                    services.AddSingleton(new SnowflakeHelper(0, 0));

                    #region 注册分布式锁

                    //注册分布式锁 Redis模式
                    //services.AddRedisLock(options =>
                    //{
                    //    options.RedisConnection = hostContext.Configuration.GetConnectionString("redisConnection");
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
