using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Repository.Core.Partitioning;

namespace Repository.Core.Extensions;

/// <summary>
/// 按具体上下文统一配置 PostgreSQL 连接和上下文工厂
/// </summary>
public static class PostgreSqlServiceCollectionExtensions
{

    /// <summary>
    /// 注册固定连接的上下文池，读连接保留现有选项并追加默认只读事务
    /// </summary>
    public static IServiceCollection AddPostgreSqlContext<TContext>(this IServiceCollection services, string connectionString, bool readOnly = false, PartitionTableSnapshot? snapshot = null, Action<DbContextOptionsBuilder>? configure = null, string? migrationHistoryTable = null) where TContext : DbContext
    {

        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        if (services.Any(descriptor => descriptor.ServiceType == typeof(IDbContextFactory<TContext>)))
        {
            throw new InvalidOperationException($"上下文 {typeof(TContext).Name} 已注册工厂");
        }

        var connection = new NpgsqlConnectionStringBuilder(connectionString);
        if (readOnly)
        {
            const string readOnlyOption = "-c default_transaction_read_only=on";
            connection.Options = string.IsNullOrWhiteSpace(connection.Options) ? readOnlyOption : $"{connection.Options} {readOnlyOption}";
        }

        services.AddPooledDbContextFactory<TContext>(options =>
        {
            options.UseNpgsql(connection.ConnectionString, postgres =>
            {
                if (migrationHistoryTable is not null)
                {
                    postgres.MigrationsHistoryTable(migrationHistoryTable, "public");
                }
            });
            if (readOnly)
            {
                options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
            }
            if (snapshot is not null)
            {
                options.UsePostgreSqlPartitioning(snapshot);
            }
            configure?.Invoke(options);
        }, connection.MaxPoolSize);
        return services;

    }

}

