using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Repository.Tool
{
    internal class Program
    {
        static void Main(string[] args)
        {
            IHost host = Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                    {
                        services.AddDbContext<Database.DatabaseContext>(options =>
                        {
                            options.UseNpgsql("Host=127.0.0.1;Database=webcore;Username=postgres;Password=123456", x => x.MigrationsAssembly("Repository.Tool"));
                        });
                    }).Build();

            host.Run();
        }
    }
}