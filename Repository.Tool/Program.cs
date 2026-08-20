using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NetEngine.Generated;
using Npgsql;
using Repository.Migrations;

namespace Repository.Tool;

/// <summary>
/// EF Core 数据库工具宿主入口
/// </summary>
internal class Program
{
    /// <summary>
    /// 启动 EF Core 数据库工具宿主
    /// </summary>
    /// <param name="args">命令行参数</param>
    static void Main(string[] args)
    {
        IHost host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((hostContext, services) =>
                {
                    services.AddDbContext<DatabaseContext>(options =>
                    {
                        var connectionString = "Host=127.0.0.1;Database=webcore;Username=postgres;Password=123456";

                        NpgsqlDataSourceBuilder dataSourceBuilder = new(connectionString);

                        options.UseNpgsql(dataSourceBuilder.Build(), x => x.MigrationsAssembly("Repository.Tool"));
                        options.UseNetEnginePostgreSqlMigrations();
                    });

                    services.BatchRegisterBackgroundServices();

                }).Build();

        host.Run();
    }
}
