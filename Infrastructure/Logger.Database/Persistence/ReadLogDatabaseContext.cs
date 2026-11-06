using Microsoft.EntityFrameworkCore;

namespace Logger.Database.Persistence;

/// <summary>
/// 使用独立读连接查询日志并复用写入模型
/// </summary>
public sealed class ReadLogDatabaseContext(DbContextOptions<ReadLogDatabaseContext> options) : LogDatabaseContext(options)
{

}
