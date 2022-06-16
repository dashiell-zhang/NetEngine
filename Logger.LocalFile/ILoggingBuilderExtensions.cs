using Logger.LocalFile;
using Logger.LocalFile.Models;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Microsoft.Extensions.Logging
{

    public static class ILoggingBuilderExtensions
    {

        public static void AddLocalFileLogger(this ILoggingBuilder builder, Action<LoggerConfiguration> action)
        {
            builder.Services.Configure(action);
            builder.Services.AddSingleton<ILoggerProvider, LocalFileLoggerProvider>();
        }
    }
}