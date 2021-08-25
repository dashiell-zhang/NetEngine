using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
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
                    var ss = services.BuildServiceProvider();

                    //为各数据库注入连接字符串
                    Repository.Database.dbContext.ConnectionString = hostContext.Configuration.GetConnectionString("dbConnection");
                    services.AddDbContextPool<Repository.Database.dbContext>(options => { }, 100);

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
                        options.UseEntityFramework<Repository.Database.dbContext>();

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
                });


        public static IServiceProvider ServiceProvider { get; set; }

    }

}
