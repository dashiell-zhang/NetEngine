using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Logger.LocalFile;

/// <summary>
/// 本地文件日志 Provider
/// </summary>
public class LocalFileLoggerProvider(LocalFileLogWriter logWriter) : ILoggerProvider, ISupportExternalScope
{

    private readonly ConcurrentDictionary<string, LocalFileLogger> loggers = new();

    private IExternalScopeProvider? scopeProvider;


    public ILogger CreateLogger(string categoryName)
    {
        return loggers.GetOrAdd(categoryName, key => new LocalFileLogger(key, logWriter, scopeProvider));
    }


    /// <summary>
    /// 设置外部 Scope 提供器
    /// </summary>
    public void SetScopeProvider(IExternalScopeProvider scopeProvider)
    {
        this.scopeProvider = scopeProvider;
    }


    public void Dispose()
    {
        loggers.Clear();
        GC.SuppressFinalize(this);
    }

}