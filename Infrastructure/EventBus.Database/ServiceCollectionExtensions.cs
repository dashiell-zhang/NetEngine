using EventBus.Database.Persistence;
using Npgsql;
using Repository.Core.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventBus.Database;

/// <summary>
/// 通过独立的 PostgreSQL 上下文接入数据库广播后端
/// </summary>
public static class ServiceCollectionExtensions
{

    /// <summary>
    /// 注册不加入业务环境事务的事件连接及单一广播后端
    /// </summary>
    public static IServiceCollection AddDatabaseEventBus(this IServiceCollection services, string connectionString, Action<EventBusOptions>? configure = null)
    {

        var connection = new NpgsqlConnectionStringBuilder(connectionString) { Enlist = false };
        services.AddPostgreSqlContext<EventBusDatabaseContext>(connection.ConnectionString, migrationHistoryTable: EventBusDatabaseContext.MigrationHistoryTable);
        return services.AddEventBus<DatabaseEventTransport>(configure);

    }

}
