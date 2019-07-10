using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Models.DataBases.WebCore
{
    public partial class webcoreContext : DbContext
    {
        public webcoreContext()
        {
        }

        public webcoreContext(DbContextOptions<webcoreContext> options)
            : base(options)
        {
        }

       
        public virtual DbSet<TFile> TFile { get; set; }
        public virtual DbSet<TUser> TUser { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseMySQL("server=cloud.blackbaby.net;userid=webcore;pwd=webcore@321;database=webcore;");
                //optionsBuilder.UseSqlServer("Data Source=cloud.blackbaby.net;Initial Catalog=webcore;User ID=webcore;Password=webcore@321");
            }
        }
    }
}
