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

        public DbSet<TUser> TUser { get; set; }

        public DbSet<TUserToken> TUserToken { get; set; }

        public DbSet<TFile> TFile { get; set; }

        public DbSet<TFileGroup> TFileGroup { get; set; }

        public DbSet<TFileGroupFile> TFileGroupFile { get; set; }

        public DbSet<TOrder> TOrder { get; set; }

        public DbSet<TProduct> TProduct { get; set; }

        public DbSet<TWeiXinKey> TWeiXinKey { get; set; }

        public DbSet<TUserBindWeixin> TUserBindWeixin { get; set; }

        public DbSet<TCount> TCount { get; set; }


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
