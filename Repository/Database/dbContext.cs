using Microsoft.EntityFrameworkCore;
using Repository.Interceptors;
using System.Linq;

namespace Repository.Database
{
    public partial class dbContext : DbContext
    {
        public dbContext()
        {
        }

        public dbContext(DbContextOptions<dbContext> options)
            : base(options)
        {
        }

        public DbSet<TAlipayKey> TAlipayKey { get; set; }

        public DbSet<TArticle> TArticle { get; set; }

        public DbSet<TCategory> TCategory { get; set; }

        public DbSet<TChannel> TChannel { get; set; }

        public DbSet<TCount> TCount { get; set; }

        public DbSet<TDictionary> TDictionary { get; set; }

        public DbSet<TFile> TFile { get; set; }

        public DbSet<TFileGroup> TFileGroup { get; set; }

        public DbSet<TFileGroupFile> TFileGroupFile { get; set; }

        public DbSet<TGuidToInt> TGuidToInt { get; set; }

        public DbSet<TLink> TLink { get; set; }

        public DbSet<TLog> TLog { get; set; }

        public DbSet<TOrder> TOrder { get; set; }

        public DbSet<TProduct> TProduct { get; set; }

        public DbSet<TProductImg> TProductImg { get; set; }

        public DbSet<TProductImgBaiduAi> TProductImgBaiduAi { get; set; }

        public DbSet<TRegionArea> TRegionArea { get; set; }

        public DbSet<TRegionCity> TRegionCity { get; set; }

        public DbSet<TRegionProvince> TRegionProvince { get; set; }

        public DbSet<TRole> TRole { get; set; }

        public DbSet<TSign> TSign { get; set; }

        public DbSet<TUser> TUser { get; set; }

        public DbSet<TUserBindAlipay> TUserBindAlipay { get; set; }

        public DbSet<TUserBindWeixin> TUserBindWeixin { get; set; }

        public DbSet<TUserInfo> TUserInfo { get; set; }

        public DbSet<TUserToken> TUserToken { get; set; }

        public DbSet<TWebInfo> TWebInfo { get; set; }

        public DbSet<TWeiXinKey> TWeiXinKey { get; set; }



        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {

            if (!optionsBuilder.IsConfigured)
            {

                optionsBuilder.UseSqlServer("Data Source=localhost;Initial Catalog=webcore;User ID=sa;Password=zhangxiaodong", o => o.MigrationsHistoryTable("__efmigrationshistory"));

                optionsBuilder.UseMySQL("server=127.0.0.1;userid=root;pwd=zhangxiaodong;database=ceshi;", o => o.MigrationsHistoryTable("__efmigrationshistory"));

                optionsBuilder.UseSqlite("Data Source=../Repository/database.db", o => o.MigrationsHistoryTable("__efmigrationshistory"));


                //开启调试拦截器
                optionsBuilder.AddInterceptors(new DeBugInterceptor());


                //开启数据分表拦截器
                //optionsBuilder.AddInterceptors(new SubTableInterceptor());


                //开启全局懒加载
                //optionsBuilder.UseLazyLoadingProxies();
            }
        }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {

            //循环关闭所有表的级联删除功能
            foreach (var foreignKey in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
            {
                foreignKey.DeleteBehavior = DeleteBehavior.Restrict;
            }


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
