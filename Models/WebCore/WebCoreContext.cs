using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Models.WebCore
{
    public partial class WebCoreContext : DbContext
    {
        public WebCoreContext()
        {
        }

        public WebCoreContext(DbContextOptions<WebCoreContext> options)
            : base(options)
        {
        }

        public virtual DbSet<TArticle> TArticle { get; set; }
        public virtual DbSet<TCategory> TCategory { get; set; }
        public virtual DbSet<TChannel> TChannel { get; set; }
        public virtual DbSet<TUserSys> TUserSys { get; set; }

        // Unable to generate entity type for table 'dbo.Cms_Link'. Please see the warning messages.
        // Unable to generate entity type for table 'dbo.Cms_Web'. Please see the warning messages.
        // Unable to generate entity type for table 'dbo.Love'. Please see the warning messages.

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. See http://go.microsoft.com/fwlink/?LinkId=723263 for guidance on storing connection strings.
                optionsBuilder.UseSqlServer("Data Source=cloud.blackbaby.net;Initial Catalog=WebCore;User ID=WebCore;Password=webcore@321");

                //启用懒加载模块
                optionsBuilder.UseLazyLoadingProxies();
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasAnnotation("ProductVersion", "2.2.3-servicing-35854");

            modelBuilder.Entity<TArticle>(entity =>
            {
                entity.ToTable("t_article");

                entity.Property(e => e.Id).HasColumnName("id");

                entity.Property(e => e.Abstract)
                    .IsRequired()
                    .HasColumnName("abstract")
                    .HasMaxLength(1000);

                entity.Property(e => e.Categoryid).HasColumnName("categoryid");

                entity.Property(e => e.Clickcount).HasColumnName("clickcount");

                entity.Property(e => e.Commentstate).HasColumnName("commentstate");

                entity.Property(e => e.Content)
                    .IsRequired()
                    .HasColumnName("content");

                entity.Property(e => e.Cover)
                    .HasColumnName("cover")
                    .HasMaxLength(100);

                entity.Property(e => e.Createtime)
                    .HasColumnName("createtime")
                    .HasColumnType("datetime");

                entity.Property(e => e.Openstate).HasColumnName("openstate");

                entity.Property(e => e.Recommendstate).HasColumnName("recommendstate");

                entity.Property(e => e.Seod)
                    .HasColumnName("seod")
                    .HasMaxLength(500);

                entity.Property(e => e.Seok)
                    .HasColumnName("seok")
                    .HasMaxLength(500);

                entity.Property(e => e.Seot)
                    .HasColumnName("seot")
                    .HasMaxLength(500);

                entity.Property(e => e.Sort).HasColumnName("sort");

                entity.Property(e => e.Title)
                    .IsRequired()
                    .HasColumnName("title")
                    .HasMaxLength(200);

                entity.Property(e => e.Updatetime)
                    .HasColumnName("updatetime")
                    .HasColumnType("datetime");

                entity.HasOne(d => d.Category)
                    .WithMany(p => p.TArticle)
                    .HasForeignKey(d => d.Categoryid)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK__t_article__categ__4F7CD00D");
            });

            modelBuilder.Entity<TCategory>(entity =>
            {
                entity.ToTable("t_category");

                entity.Property(e => e.Id).HasColumnName("id");

                entity.Property(e => e.Channelid).HasColumnName("channelid");

                entity.Property(e => e.Createtime)
                    .HasColumnName("createtime")
                    .HasColumnType("datetime");

                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasColumnName("name")
                    .HasMaxLength(500);

                entity.Property(e => e.Parentid).HasColumnName("parentid");

                entity.Property(e => e.Remark).HasColumnName("remark");

                entity.Property(e => e.Seod)
                    .HasColumnName("seod")
                    .HasMaxLength(500);

                entity.Property(e => e.Seok)
                    .HasColumnName("seok")
                    .HasMaxLength(500);

                entity.Property(e => e.Seot)
                    .HasColumnName("seot")
                    .HasMaxLength(500);

                entity.Property(e => e.Sort).HasColumnName("sort");

                entity.Property(e => e.Updatetime)
                    .HasColumnName("updatetime")
                    .HasColumnType("datetime");

                entity.Property(e => e.Urlrole)
                    .HasColumnName("urlrole")
                    .HasMaxLength(50);

                entity.HasOne(d => d.Channel)
                    .WithMany(p => p.TCategory)
                    .HasForeignKey(d => d.Channelid)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK__t_categor__chann__5070F446");
            });

            modelBuilder.Entity<TChannel>(entity =>
            {
                entity.ToTable("t_channel");

                entity.Property(e => e.Id).HasColumnName("id");

                entity.Property(e => e.Createtime)
                    .HasColumnName("createtime")
                    .HasColumnType("datetime");

                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasColumnName("name")
                    .HasMaxLength(50);

                entity.Property(e => e.Remark)
                    .HasColumnName("remark")
                    .HasMaxLength(100);

                entity.Property(e => e.Seod)
                    .HasColumnName("seod")
                    .HasMaxLength(500);

                entity.Property(e => e.Seok)
                    .HasColumnName("seok")
                    .HasMaxLength(500);

                entity.Property(e => e.Seot)
                    .HasColumnName("seot")
                    .HasMaxLength(500);

                entity.Property(e => e.Sort).HasColumnName("sort");

                entity.Property(e => e.Updatetime)
                    .HasColumnName("updatetime")
                    .HasColumnType("datetime");

                entity.Property(e => e.Urlrole)
                    .HasColumnName("urlrole")
                    .HasMaxLength(50);
            });

            modelBuilder.Entity<TUserSys>(entity =>
            {
                entity.ToTable("t_user_sys");

                entity.Property(e => e.Id).HasColumnName("id");

                entity.Property(e => e.Createtime)
                    .HasColumnName("createtime")
                    .HasColumnType("datetime");

                entity.Property(e => e.Email)
                    .HasColumnName("email")
                    .HasMaxLength(100)
                    .IsUnicode(false);

                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasColumnName("name")
                    .HasMaxLength(255)
                    .IsUnicode(false);

                entity.Property(e => e.Nickname)
                    .HasColumnName("nickname")
                    .HasMaxLength(255)
                    .IsUnicode(false);

                entity.Property(e => e.Password)
                    .IsRequired()
                    .HasColumnName("password")
                    .HasMaxLength(255)
                    .IsUnicode(false);

                entity.Property(e => e.Phone)
                    .HasColumnName("phone")
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.Updatetime)
                    .HasColumnName("updatetime")
                    .HasColumnType("datetime");
            });
        }
    }
}
