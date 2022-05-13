using Common.RedisLock;
using Common.RedisLock.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System;
using System.Net.Http;

namespace TaskService
{
    class Program
    {
        static void Main(string[] args)
        {
            Common.EnvironmentHelper.ChangeDirectory(args);

            using IHost host = CreateHostBuilder(args).Build();

            ServiceProvider = host.Services;

            host.Run();
        }


        private static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {

                    //为各数据库注入连接字符串
                    Repository.Database.DatabaseContext.ConnectionString = hostContext.Configuration.GetConnectionString("dbConnection");
                    services.AddDbContextPool<Repository.Database.DatabaseContext>(options => { }, 100);

                    services.AddSingleton<IDistributedLockProvider>(new RedisDistributedSynchronizationProvider(ConnectionMultiplexer.Connect(hostContext.Configuration.GetConnectionString("redisConnection")).GetDatabase()));
                    services.AddSingleton<IDistributedSemaphoreProvider>(new RedisDistributedSynchronizationProvider(ConnectionMultiplexer.Connect(hostContext.Configuration.GetConnectionString("redisConnection")).GetDatabase()));
                    services.AddSingleton<IDistributedReaderWriterLockProvider>(new RedisDistributedSynchronizationProvider(ConnectionMultiplexer.Connect(hostContext.Configuration.GetConnectionString("redisConnection")).GetDatabase()));


                    services.AddHostedService<Tasks.DemoTask>();


                    //注册雪花ID算法示例
                    services.AddSingleton(new Common.SnowflakeHelper(0, 0));


                    //注册缓存服务 内存模式
                    services.AddDistributedMemoryCache();


                    //注册缓存服务 SqlServer模式
                    //services.AddDistributedSqlServerCache(options =>
                    //{
                    //    options.ConnectionString = Configuration.GetConnectionString("dbConnection");
                    //    options.SchemaName = "dbo";
                    //    options.TableName = "t_cache";
                    //});


                    //注册缓存服务 Redis模式
                    //services.AddStackExchangeRedisCache(options =>
                    //{
                    //    options.Configuration = Configuration.GetConnectionString("redisConnection");
                    //    options.InstanceName = "cache";
                    //});

                    services.AddHttpClient("", options =>
                    {
                        options.DefaultRequestVersion = new Version("2.0");
                        options.DefaultRequestHeaders.Add("Accept", "*/*");
                        options.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/95.0.4638.69 Safari/537.36");
                        options.DefaultRequestHeaders.Add("Accept-Language", "zh-CN,zh;q=0.9");
                    }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                    {
                        AllowAutoRedirect = false
                    });


                    services.AddHttpClient("SkipSsl", options =>
                    {
                        options.DefaultRequestVersion = new Version("2.0");
                        options.DefaultRequestHeaders.Add("Accept", "*/*");
                        options.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/95.0.4638.69 Safari/537.36");
                        options.DefaultRequestHeaders.Add("Accept-Language", "zh-CN,zh;q=0.9");
                    }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                    {
                        AllowAutoRedirect = false,
                        ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => { return true; }
                    });


                    services.AddHttpClient("xxx.com", options =>
                    {
                        options.DefaultRequestVersion = new Version("2.0");
                        options.DefaultRequestHeaders.Add("Accept", "*/*");
                        options.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/95.0.4638.69 Safari/537.36");
                        options.DefaultRequestHeaders.Add("Accept-Language", "zh-CN,zh;q=0.9");
                    }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                    {
                        AllowAutoRedirect = false,
                        ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) =>
                        {
                            return string.Equals(cert?.Thumbprint, "xxxxxx", StringComparison.OrdinalIgnoreCase);
                        }
                    });

                });


        public static IServiceProvider ServiceProvider { get; set; }

    }

}
