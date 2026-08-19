using Microsoft.EntityFrameworkCore;

namespace Repository;

/// <summary>
/// 数据库读取上下文
/// </summary>
public class ReadDatabaseContext(DbContextOptions<ReadDatabaseContext> options) : DatabaseContext(options)
{

}
