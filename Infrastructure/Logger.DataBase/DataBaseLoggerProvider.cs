using Logger.DataBase.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace Logger.DataBase;

/// <summary>
/// 数据库日志 Provider
/// </summary>
public class DataBaseLoggerProvider(IOptionsMonitor<LoggerSetting> config, DataBaseLogWriter logWriter) : ILoggerProvider, ISupportExternalScope
{

    private readonly LoggerSetting loggerConfiguration = config.CurrentValue;

    private readonly ConcurrentDictionary<string, DataBaseLogger> loggers = new();

    private IExternalScopeProvider? scopeProvider;


    /// <summary>
    /// 获取指定类别的日志记录器
    /// </summary>
    public ILogger CreateLogger(string categoryName)
    {

        return loggers.GetOrAdd(categoryName, key => new DataBaseLogger(key, loggerConfiguration, logWriter, scopeProvider));

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
