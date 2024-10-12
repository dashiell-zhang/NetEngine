using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Logger.LocalFile
{
    public class LocalFileLoggerProvider : ILoggerProvider
    {


        private readonly ConcurrentDictionary<string, LocalFileLogger> loggers = new();




        public ILogger CreateLogger(string categoryName)
        {
            return loggers.GetOrAdd(categoryName, new LocalFileLogger(categoryName));
        }

        public void Dispose()
        {
            loggers.Clear();
            GC.SuppressFinalize(this);
        }
    }
}
