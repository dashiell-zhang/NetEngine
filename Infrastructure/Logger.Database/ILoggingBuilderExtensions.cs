using Logger.Database.Persistence;
using Logger.Database.Query;
using Repository.Core.Extensions;
using Repository.Core.Partitioning;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Logger.Database.Models;
using Logger.Database.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Logger.Database;

/// <summary>
/// 注册数据库日志及其后台持久化服务
/// </summary>
public static class ILoggingBuilderExtensions
{

    /// <summary>
    /// 注册数据库日志并在启动时校验缓冲配置
    /// </summary>
    public static void AddDatabaseLogger(this ILoggingBuilder builder, string connectionString, string databaseIdentity, Action<LoggerSetting> action, object? partitionLockKey = null)
    {

        if (builder.Services.Any(descriptor => descriptor.ServiceType == typeof(DatabaseLogWriter)))
        {
            throw new InvalidOperationException("一个宿主只能注册一个日志写入目标");
        }

        connectionString = new NpgsqlConnectionStringBuilder(connectionString) { Enlist = false }.ConnectionString;
        var snapshot = PartitionTableSnapshot.Load(connectionString);
        builder.Services.AddPostgreSqlContext<LogDatabaseContext>(connectionString, snapshot: snapshot,
            configure: options => options.UseLoggerFactory(NullLoggerFactory.Instance), migrationHistoryTable: LogDatabaseContext.MigrationHistoryTable);
        builder.Services.AddPartitionMaintenance<LogDatabaseContext>(databaseIdentity, partitionLockKey);
        builder.Services.AddOptions<LoggerSetting>().Configure(action)
            .Validate(options => !string.IsNullOrWhiteSpace(options.Project), "日志 Project 不能为空")
            .Validate(options => options.SaveDays == -1 || options.SaveDays > 0, "日志保留天数必须为正数或 -1")
            .Validate(options => options.QueueCapacity > 0 && options.ImportantQueueCapacity > 0, "日志队列容量必须大于零")
            .Validate(options => options.MaxBufferedBytes > 0 && options.ImportantMaxBufferedBytes > 0, "日志缓冲字节上限必须大于零")
            .Validate(options => options.BatchSize > 0, "日志批量大小必须大于零")
            .Validate(options => options.FlushInterval.TotalMilliseconds >= 1 && options.FlushInterval.TotalMilliseconds <= uint.MaxValue - 1, "日志落盘间隔必须介于 1 毫秒与 4294967294 毫秒之间")
            .ValidateOnStart();

        builder.Services.AddSingleton<DatabaseLogWriter>();
        builder.Services.AddSingleton(provider => new LogSpoolDirectory(connectionString, databaseIdentity, provider.GetRequiredService<IOptions<LoggerSetting>>().Value.Project));
        builder.Services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<DatabaseLogWriter>());
        builder.Services.AddSingleton<ILoggerProvider, DatabaseLoggerProvider>();
        builder.Services.AddHostedService<LogClearTask>();
        builder.Services.AddHostedService<LogSaveTask>();

    }

    /// <summary>
    /// 仅注册日志查询，不启动写入、ID、文件消费者或分区维护
    /// </summary>
    public static IServiceCollection AddDatabaseLogQuery(this IServiceCollection services, string writeConnectionString, string? readConnectionString = null)
    {

        services.AddPostgreSqlContext<ReadLogDatabaseContext>(string.IsNullOrWhiteSpace(readConnectionString) ? writeConnectionString : readConnectionString,
            readOnly: true, configure: options => options.UseLoggerFactory(NullLoggerFactory.Instance), migrationHistoryTable: LogDatabaseContext.MigrationHistoryTable);
        services.AddScoped<LogQueryService>();
        return services;

    }

}
