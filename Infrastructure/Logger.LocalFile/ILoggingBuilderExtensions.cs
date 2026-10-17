using Logger.LocalFile.Models;
using Logger.LocalFile.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Logger.LocalFile;

/// <summary>
/// 注册本地文件日志及其后台写入和清理服务
/// </summary>
public static class ILoggingBuilderExtensions
{

    /// <summary>
    /// 注册有界日志缓冲并在启动时校验容量、间隔和保留天数
    /// </summary>
    public static void AddLocalFileLogger(this ILoggingBuilder builder, Action<LoggerSetting> action)
    {

        builder.Services.AddOptions<LoggerSetting>().Configure(action)
            .Validate(options => options.QueueCapacity > 0 && options.ImportantQueueCapacity > 0, "日志队列容量必须大于零")
            .Validate(options => options.MaxBufferedBytes > 0 && options.ImportantMaxBufferedBytes > 0, "日志缓冲字节上限必须大于零")
            .Validate(options => options.BatchSize > 0, "日志批量大小必须大于零")
            .Validate(options => options.FlushInterval.TotalMilliseconds >= 1 && options.FlushInterval.TotalMilliseconds <= uint.MaxValue - 1, "日志落盘间隔必须介于 1 毫秒与 4294967294 毫秒之间")
            .Validate(options => options.SaveDays == -1 || options.SaveDays > 0, "日志保留天数必须为正数或 -1")
            .ValidateOnStart();

        builder.Services.AddSingleton<LocalFileLogWriter>();
        builder.Services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<LocalFileLogWriter>());
        builder.Services.AddSingleton<ILoggerProvider, LocalFileLoggerProvider>();
        builder.Services.AddHostedService<LogClearTask>();

    }

}
