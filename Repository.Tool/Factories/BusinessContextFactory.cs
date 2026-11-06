using Repository;
using Repository.Tool.Configuration;
using Microsoft.EntityFrameworkCore.Design;

namespace Repository.Tool.Factories;

/// <summary>
/// 在工具中离线创建 Business 写模型
/// </summary>
public sealed class BusinessContextFactory : IDesignTimeDbContextFactory<DatabaseContext>
{

    /// <summary>
    /// 创建使用对应迁移历史表的上下文，不连接数据库
    /// </summary>
    public DatabaseContext CreateDbContext(string[] args)
    {

        return new DatabaseContext(ToolDatabaseOptions.Build<DatabaseContext>("Business", DatabaseContext.MigrationHistoryTable, true, args));

    }

}
