using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Repository.Core.Partitioning;
using Repository.Tool.Factories;

namespace Repository.Tool.Configuration;

/// <summary>
/// 从工具 appsettings.json 创建写模型，不加载运行时快照或后台服务
/// </summary>
internal static class ToolDatabaseOptions
{

    /// <summary>
    /// 创建明确指定的业务、日志或事件上下文
    /// </summary>
    public static DbContext Create(string target)
    {

        return target.ToLowerInvariant() switch
        {
            "business" => new BusinessContextFactory().CreateDbContext([]),
            "log" => new LogContextFactory().CreateDbContext([]),
            "eventbus" => new EventBusContextFactory().CreateDbContext([]),
            _ => throw new ArgumentException("目标必须为 Business、Log 或 EventBus")
        };

    }


    /// <summary>
    /// 配置固定迁移程序集和目标历史表，离线建模不连接数据库
    /// </summary>
    public static DbContextOptions<TContext> Build<TContext>(string target, string historyTable, bool partitioned, string[] args) where TContext : DbContext
    {

        if (args.Length != 0)
        {

            throw new ArgumentException("设计时工厂不接受附加参数，请修改 Repository.Tool/appsettings.json 中对应的连接");

        }

        // 按工具程序集目录读取配置，不依赖控制台当前目录
        var toolDirectory = Path.GetDirectoryName(typeof(ToolDatabaseOptions).Assembly.Location)!;
        var configurationFile = Path.Combine(toolDirectory, "appsettings.json");
        var configurationBuilder = new ConfigurationBuilder();
        configurationBuilder.AddJsonFile(configurationFile, optional: false);

        using var configuration = (ConfigurationRoot)configurationBuilder.Build();
        var connectionString = configuration.GetConnectionString(target);
        if (string.IsNullOrWhiteSpace(connectionString))
        {

            throw new InvalidOperationException($"工具缺少 ConnectionStrings:{target}，请在 Repository.Tool/appsettings.json 中配置");

        }

        // 固定迁移所在程序集，并使用当前模型独立的迁移历史表
        var migrationAssembly = typeof(ToolDatabaseOptions).Assembly.GetName().Name;
        var optionsBuilder = new DbContextOptionsBuilder<TContext>();
        optionsBuilder.UseNpgsql(connectionString, postgres =>
        {

            postgres.MigrationsAssembly(migrationAssembly);
            postgres.MigrationsHistoryTable(historyTable, "public");

        });

        // 需要分区表的模型启用对应扩展，离线建模不加载运行时分区快照
        if (partitioned)
        {

            optionsBuilder.UsePostgreSqlPartitioning();

        }

        return optionsBuilder.Options;

    }

}
