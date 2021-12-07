using Medallion.Threading;
using Medallion.Threading.Redis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System;
using System.Net.Http;
using TaskService.Filters;
using TaskService.Subscribes;

namespace TaskService
{
    class Program
    {
        static void Main(string[] args)
        {
            Common.EnvironmentHelper.ChangeDirectory(args);
            Common.EnvironmentHelper.InitTestServer();

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
                    services.AddDbContextPool<Repository.Database.DatabaseContext>(options => { }, 30);

                    services.AddSingleton<IDistributedLockProvider>(new RedisDistributedSynchronizationProvider(ConnectionMultiplexer.Connect(hostContext.Configuration.GetConnectionString("redisConnection")).GetDatabase()));
                    services.AddSingleton<IDistributedSemaphoreProvider>(new RedisDistributedSynchronizationProvider(ConnectionMultiplexer.Connect(hostContext.Configuration.GetConnectionString("redisConnection")).GetDatabase()));
                    services.AddSingleton<IDistributedReaderWriterLockProvider>(new RedisDistributedSynchronizationProvider(ConnectionMultiplexer.Connect(hostContext.Configuration.GetConnectionString("redisConnection")).GetDatabase()));

                    services.AddLogging(options => options.AddConsole());

                    services.AddSingleton<DemoSubscribe>();
                    services.AddCap(options =>
                    {

                        //使用 Redis 传输消息
                        options.UseRedis(hostContext.Configuration.GetConnectionString("redisConnection"));

                        //var rabbitMQSetting = hostContext.Configuration.GetSection("RabbitMQSetting").Get<RabbitMQSetting>();

                        //使用 RabbitMQ 传输消息
                        //options.UseRabbitMQ(options =>
                        //{
                        //    options.HostName = rabbitMQSetting.HostName;
                        //    options.UserName = rabbitMQSetting.UserName;
                        //    options.Password = rabbitMQSetting.PassWord;
                        //    options.VirtualHost = rabbitMQSetting.VirtualHost;
                        //    options.Port = rabbitMQSetting.Port;
                        //    options.ConnectionFactoryOptions = options =>
                        //    {
                        //        options.Ssl = new RabbitMQ.Client.SslOption { Enabled = rabbitMQSetting.Ssl.Enabled, ServerName = rabbitMQSetting.Ssl.ServerName };
                        //    };
                        //});


                        //使用 ef 搭配 db 存储执行情况
                        options.UseEntityFramework<Repository.Database.DatabaseContext>();

                        options.DefaultGroupName = "default";   //默认组名称
                        options.GroupNamePrefix = null; //全局组名称前缀
                        options.TopicNamePrefix = null; //Topic 统一前缀
                        options.Version = "v1";
                        options.FailedRetryInterval = 60;   //失败时重试间隔
                        options.ConsumerThreadCount = 1;    //消费者线程并行处理消息的线程数，当这个值大于1时，将不能保证消息执行的顺序
                        options.FailedRetryCount = 10;  //失败时重试的最大次数
                        options.FailedThresholdCallback = null; //重试阈值的失败回调
                        options.SucceedMessageExpiredAfter = 24 * 3600; //成功消息的过期时间（秒）
                    }).AddSubscribeFilter<CapSubscribeFilter>();


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

                });


        public static IServiceProvider ServiceProvider { get; set; }

    }

}
