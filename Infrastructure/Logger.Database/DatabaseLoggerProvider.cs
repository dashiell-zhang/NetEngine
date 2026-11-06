using Logger.Database.Models;
using Logger.Database.Persistence;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace Logger.Database;

/// <summary>
/// 数据库日志 Provider
/// </summary>
internal class DatabaseLoggerProvider(IOptionsMonitor<LoggerSetting> config, DatabaseLogWriter logWriter) : ILoggerProvider, ISupportExternalScope
{

    private readonly LoggerSetting loggerConfiguration = config.CurrentValue;

    private readonly ConcurrentDictionary<string, DatabaseLogger> loggers = new();

    private IExternalScopeProvider? scopeProvider;


    /// <summary>
    /// 排除本组件分区维护诊断，其余类别继续遵循宿主的日志级别及过滤配置
    /// </summary>
    public ILogger CreateLogger(string categoryName)
    {

        if (categoryName.StartsWith("Repository.Core.Partitioning.", StringComparison.Ordinal)
            && categoryName.EndsWith("." + typeof(LogDatabaseContext).FullName, StringComparison.Ordinal))
        {
            return NullLogger.Instance;
        }
        return loggers.GetOrAdd(categoryName, key => new DatabaseLogger(key, loggerConfiguration, logWriter, scopeProvider));

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
