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
                            options.UseSqlServer("Data Source=127.0.0.1;Initial Catalog=webcore;User ID=sa;Password=123456;Max Pool Size=100;Encrypt=True", x => x.MigrationsAssembly("Repository.Tool"));
                        });
                    }).Build();

            host.Run();
        }
    }
}