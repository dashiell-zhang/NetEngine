using global::QueueTask.Database;
using global::QueueTask.Database.Models;
using Microsoft.EntityFrameworkCore;
using Repository.Database;
using Repository.Database.Generated;
using Repository.Core.Bases;
using Repository.Core.Extensions;

namespace Repository;

/// <summary>
/// 数据库写入上下文
/// </summary>
public class DatabaseContext : RepositoryDbContext
{

    /// <summary>
    /// 本业务模型在 public 中使用的迁移历史表
    /// </summary>
    public const string MigrationHistoryTable = "__EFMigrationsHistory_Business";


    /// <summary>
    /// 创建数据库写入上下文
    /// </summary>
    public DatabaseContext(DbContextOptions<DatabaseContext> options) : base(options)
    {

    }


    /// <summary>
    /// 创建派生数据库上下文
    /// </summary>
    protected DatabaseContext(DbContextOptions options) : base(options)
    {

    }


    #region 表实体声明

    public DbSet<AppSetting> AppSetting { get; set; }

    public DbSet<Article> Article { get; set; }

    public DbSet<Category> Category { get; set; }

    public DbSet<DataUpdateLog> DataUpdateLog { get; set; }

    public DbSet<Function> Function { get; set; }

    public DbSet<FunctionAuthorize> FunctionAuthorize { get; set; }

    public DbSet<FunctionRoute> FunctionRoute { get; set; }

    public DbSet<Link> Link { get; set; }
    public DbSet<LlmApp> LlmApp { get; set; }

    public DbSet<LlmConversation> LlmConversation { get; set; }

    public DbSet<LlmModel> LlmModel { get; set; }

    public DbSet<Order> Order { get; set; }

    public DbSet<OrderDetail> OrderDetail { get; set; }

    public DbSet<Product> Product { get; set; }

    public DbSet<global::QueueTask.Database.Models.QueueTask> QueueTask { get; set; }

    public DbSet<RegionArea> RegionArea { get; set; }

    public DbSet<RegionCity> RegionCity { get; set; }

    public DbSet<RegionProvince> RegionProvince { get; set; }

    public DbSet<RegionTown> RegionTown { get; set; }

    public DbSet<Role> Role { get; set; }

    public DbSet<StoredFile> StoredFile { get; set; }

    public DbSet<TaskSetting> TaskSetting { get; set; }

    public DbSet<User> User { get; set; }

    public DbSet<UserBindExternal> UserBindExternal { get; set; }

    public DbSet<UserInfo> UserInfo { get; set; }

    public DbSet<UserRole> UserRole { get; set; }

    public DbSet<UserToken> UserToken { get; set; }

    #endregion


    /// <summary>
    /// 配置业务实体、过滤器和分区元数据
    /// </summary>
    /// <param name="modelBuilder">EF Core 模型构建器</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {

        modelBuilder.ConfigureQueueTasks();
        modelBuilder.ApplySoftDeleteFilters(this);

        modelBuilder.ApplyJsonColumns(this);


        modelBuilder.ApplyModelDocumentation(this);

        modelBuilder.ApplyPartitionTables(this);

    }

}
