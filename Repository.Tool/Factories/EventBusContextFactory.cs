using EventBus.Database.Persistence;
using Repository.Tool.Configuration;
using Microsoft.EntityFrameworkCore.Design;

namespace Repository.Tool.Factories;

/// <summary>
/// 在工具中离线创建 EventBus 写模型
/// </summary>
public sealed class EventBusContextFactory : IDesignTimeDbContextFactory<EventBusDatabaseContext>
{

    /// <summary>
    /// 创建使用对应迁移历史表的上下文，不连接数据库
    /// </summary>
    public EventBusDatabaseContext CreateDbContext(string[] args)
    {

        return new EventBusDatabaseContext(ToolDatabaseOptions.Build<EventBusDatabaseContext>("EventBus", EventBusDatabaseContext.MigrationHistoryTable, false, args));

    }

}
