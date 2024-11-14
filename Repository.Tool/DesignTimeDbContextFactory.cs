using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;
using Repository.Database;

namespace Repository.Tool
{
    internal class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<DatabaseContext>
    {
        public DatabaseContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<DatabaseContext>();

            var connectionString = "Host=127.0.0.1;Database=webcore;Username=postgres;Password=123456";

            NpgsqlDataSourceBuilder dataSourceBuilder = new(connectionString);

            optionsBuilder.UseNpgsql(dataSourceBuilder.Build(), x => x.MigrationsAssembly("Repository.Tool"));

            return new DatabaseContext(optionsBuilder.Options);
        }
    }
}
