using Logger.DataBase.Models;
using Logger.DataBase.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Logger.DataBase;

/// <summary>
/// 注册数据库日志及其后台持久化服务
/// </summary>
public static class ILoggingBuilderExtensions
{

    /// <summary>
    /// 注册数据库日志并在启动时校验缓冲配置
    /// </summary>
    public static void AddDataBaseLogger(this ILoggingBuilder builder, Action<LoggerSetting> action)
    {

        builder.Services.AddOptions<LoggerSetting>().Configure(action)
            .Validate(options => options.QueueCapacity > 0 && options.ImportantQueueCapacity > 0, "日志队列容量必须大于零")
            .Validate(options => options.MaxBufferedBytes > 0 && options.ImportantMaxBufferedBytes > 0, "日志缓冲字节上限必须大于零")
            .Validate(options => options.BatchSize > 0, "日志批量大小必须大于零")
            .Validate(options => options.FlushInterval.TotalMilliseconds >= 1 && options.FlushInterval.TotalMilliseconds <= uint.MaxValue - 1, "日志落盘间隔必须介于 1 毫秒与 4294967294 毫秒之间")
            .ValidateOnStart();

        builder.Services.AddSingleton<DataBaseLogWriter>();
        builder.Services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<DataBaseLogWriter>());
        builder.Services.AddSingleton<ILoggerProvider, DataBaseLoggerProvider>();
        builder.Services.AddHostedService<LogClearTask>();
        builder.Services.AddHostedService<LogSaveTask>();

    }

}
