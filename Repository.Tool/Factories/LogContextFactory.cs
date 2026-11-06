using Logger.Database.Persistence;
using Repository.Tool.Configuration;
using Microsoft.EntityFrameworkCore.Design;

namespace Repository.Tool.Factories;

/// <summary>
/// 在工具中离线创建 Log 写模型
/// </summary>
public sealed class LogContextFactory : IDesignTimeDbContextFactory<LogDatabaseContext>
{

    /// <summary>
    /// 创建使用对应迁移历史表的上下文，不连接数据库
    /// </summary>
    public LogDatabaseContext CreateDbContext(string[] args)
    {

        return new LogDatabaseContext(ToolDatabaseOptions.Build<LogDatabaseContext>("Log", LogDatabaseContext.MigrationHistoryTable, true, args));

    }

}
