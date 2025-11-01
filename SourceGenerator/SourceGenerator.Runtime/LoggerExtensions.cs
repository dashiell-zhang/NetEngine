using Microsoft.Extensions.Logging;

namespace SourceGenerator.Runtime;

public static class LoggerExtensions
{
    public static void Info(this ILogger logger, string message)
        => logger.LogInformation(message);
}

