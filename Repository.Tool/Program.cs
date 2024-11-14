using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Repository.Database;
using Repository.Tool.Tasks;

namespace Repository.Tool
{

    internal class Program
    {
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
                        });
                        services.AddHostedService<SyncJsonIndexTask>();
                    }).Build();

            host.Run();
        }
    }
}