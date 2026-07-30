using Microsoft.EntityFrameworkCore;

namespace Repository;

/// <summary>
/// 数据库只读上下文
/// </summary>
public class ReadDatabaseContext(DbContextOptions<ReadDatabaseContext> options) : DatabaseContext(options)
{

    /// <summary>
    /// 禁止只读上下文同步保存数据
    /// </summary>
    public override int SaveChanges()
    {

        throw CreateReadOnlyException();

    }


    /// <summary>
    /// 禁止只读上下文同步保存数据
    /// </summary>
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {

        throw CreateReadOnlyException();

    }


    /// <summary>
    /// 禁止只读上下文异步保存数据
    /// </summary>
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {

        throw CreateReadOnlyException();

    }


    /// <summary>
    /// 禁止只读上下文异步保存数据
    /// </summary>
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {

        throw CreateReadOnlyException();

    }


    /// <summary>
    /// 创建只读上下文保存异常
    /// </summary>
    private static InvalidOperationException CreateReadOnlyException()
    {

        return new InvalidOperationException("ReadDatabaseContext 为只读上下文，不允许保存数据");

    }

}
