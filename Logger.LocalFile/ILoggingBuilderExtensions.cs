using Logger.LocalFile.Models;
using Logger.LocalFile.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Logger.LocalFile
{

    public static class ILoggingBuilderExtensions
    {

        public static void AddLocalFileLogger(this ILoggingBuilder builder, Action<LoggerSetting> action)
        {
            builder.Services.Configure(action);
            builder.Services.AddSingleton<ILoggerProvider, LocalFileLoggerProvider>();
            builder.Services.AddHostedService<LogClearTask>();
        }
    }
}