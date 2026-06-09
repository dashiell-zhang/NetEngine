using Logger.DataBase.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace Logger.DataBase;

/// <summary>
/// 数据库日志 Provider
/// </summary>
public class DataBaseLoggerProvider(IOptionsMonitor<LoggerSetting> config, IServiceProvider serviceProvider, DataBaseLogWriter logWriter) : ILoggerProvider, ISupportExternalScope
{

    private readonly LoggerSetting loggerConfiguration = config.CurrentValue;

    private readonly ConcurrentDictionary<string, DataBaseLogger> loggers = new();

    private IExternalScopeProvider? scopeProvider;


    public ILogger CreateLogger(string categoryName)
    {
        return loggers.GetOrAdd(categoryName, key => new DataBaseLogger(key, loggerConfiguration, serviceProvider, logWriter, scopeProvider));
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