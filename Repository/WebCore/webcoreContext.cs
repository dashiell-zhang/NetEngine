using Microsoft.EntityFrameworkCore;

namespace Repository.WebCore
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

        public DbSet<TAlipayKey> TAlipayKey { get; set; }

        public DbSet<TCount> TCount { get; set; }

        public DbSet<TFile> TFile { get; set; }

        public DbSet<TFileGroup> TFileGroup { get; set; }

        public DbSet<TFileGroupFile> TFileGroupFile { get; set; }

        public DbSet<TGuidToInt> TGuidToInt { get; set; }

        public DbSet<TLog> TLog { get; set; }

        public DbSet<TOrder> TOrder { get; set; }

        public DbSet<TProduct> TProduct { get; set; }

        public DbSet<TProductImg> TProductImg { get; set; }

        public DbSet<TProductImgBaiduAi> TProductImgBaiduAi { get; set; }

        public DbSet<TUser> TUser { get; set; }

        public DbSet<TUserBindAlipay> TUserBindAlipay { get; set; }

        public DbSet<TUserBindWeixin> TUserBindWeixin { get; set; }

        public DbSet<TUserToken> TUserToken { get; set; }

        public DbSet<TWeiXinKey> TWeiXinKey { get; set; }




        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlServer("Data Source=cloud.blackbaby.net;Initial Catalog=webcore;User ID=webcore;Password=webcore@321");

                ///开启全局懒加载
                optionsBuilder.UseLazyLoadingProxies();

                //optionsBuilder.UseMySQL("server=127.0.0.1;userid=webcore;pwd=webcore@321;database=webcore;");
            }
        }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            foreach (var entity in modelBuilder.Model.GetEntityTypes())
            {
                modelBuilder.Entity(entity.Name, builder =>
                {

                    //设置生成数据库时的表名为小写格式并添加前缀 t_
                    string tablename = "t_" + entity.ClrType.Name.ToLower().Substring(1);
                    builder.ToTable(tablename);


                    //循环转换数据库表字段名全部为小写
                    foreach (var property in entity.GetProperties())
                    {
                        builder.Property(property.Name).HasColumnName(property.Name.ToLower());
                    }
                });
            }
        }
    }
}
