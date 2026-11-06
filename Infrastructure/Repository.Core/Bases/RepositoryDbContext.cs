using Microsoft.EntityFrameworkCore;

namespace Repository.Core.Bases;

/// <summary>
/// 为具体数据库上下文提供公共保存前处理，不声明实体或连接
/// </summary>
public abstract class RepositoryDbContext(DbContextOptions options) : DbContext(options)
{

    /// <summary>
    /// 为没有显式修改更新时间的实体补充当前时间
    /// </summary>
    public void PreprocessingChangeTracker()
    {

        foreach (var entry in ChangeTracker.Entries().Where(entry => entry.State == EntityState.Modified))
        {
            var updateTime = entry.Properties.FirstOrDefault(property => property.Metadata.Name == "UpdateTime");
            if (updateTime is not null && !updateTime.IsModified)
            {
                updateTime.CurrentValue = DateTimeOffset.UtcNow;
            }
        }

    }


    /// <summary>
    /// 处理更新时间后执行同步保存
    /// </summary>
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {

        PreprocessingChangeTracker();
        return base.SaveChanges(acceptAllChangesOnSuccess);

    }


    /// <summary>
    /// 处理更新时间后执行异步保存
    /// </summary>
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {

        PreprocessingChangeTracker();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);

    }

}
