using Common;
using Common.DistributedLock;
using Common.FileStorage;
using Common.SMS;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Net.Http;
using TaskService.Models.AppSetting;

namespace TaskService
{
    class Program
    {
        static void Main(string[] args)
        {
            EnvironmentHelper.ChangeDirectory(args);

            using IHost host = CreateHostBuilder(args).Build();

            host.Run();
        }


        private static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {

                    services.AddDbContextPool<Repository.Database.DatabaseContext>(options =>
                    {
                        options.UseSqlServer(hostContext.Configuration.GetConnectionString("dbConnection"), o => o.MigrationsHistoryTable("__efmigrationshistory"));
                    }, 100);


                    services.BatchRegisterServices();


                    //注册腾讯云短信服务
                    //var tencentCloudSMSSetting = hostContext.Configuration.GetSection("TencentCloudSMS").Get<TencentCloudSMSSetting>();
                    //services.AddSingleton<ISMS>(new TencentCloudSMS(tencentCloudSMSSetting.AppId, tencentCloudSMSSetting.SecretId, tencentCloudSMSSetting.SecretKey));


                    //注册阿里云短信服务
                    //var aliCloudSMSSetting = hostContext.Configuration.GetSection("AliCloudSMS").Get<AliCloudSMSSetting>();
                    //services.AddSingleton<ISMS>(new AliCloudSMS(aliCloudSMSSetting.AccessKeyId, aliCloudSMSSetting.AccessKeySecret));



                    //注册腾讯云COS文件服务
                    //var tencentCloudFileStorageSetting = hostContext.Configuration.GetSection("TencentCloudFileStorage").Get<TencentCloudFileStorageSetting>();
                    //services.AddSingleton<IFileStorage>(new TencentCloudFileStorage(tencentCloudFileStorageSetting.AppId, tencentCloudFileStorageSetting.Region, tencentCloudFileStorageSetting.SecretId, tencentCloudFileStorageSetting.SecretKey, tencentCloudFileStorageSetting.BucketName));


                    //注册阿里云OSS文件服务
                    //var aliCloudFileStorageSetting = hostContext.Configuration.GetSection("AliCloudFileStorage").Get<AliCloudFileStorageSetting>();
                    //services.AddSingleton<IFileStorage>(new AliCloudFileStorage(aliCloudFileStorageSetting.Endpoint, aliCloudFileStorageSetting.AccessKeyId, aliCloudFileStorageSetting.AccessKeySecret, aliCloudFileStorageSetting.BucketName));



                    //注册雪花ID算法示例
                    services.AddSingleton(new SnowflakeHelper(0, 0));


                    //注册分布式锁 Redis模式
                    //services.AddSingleton<IDistributedLock, RedisLock>();

                    //注册分布式锁 数据库模式
                    services.AddScoped<IDistributedLock, DataBaseLock>();


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


                });



    }

}
