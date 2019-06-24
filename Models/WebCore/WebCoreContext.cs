using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Models.WebCore
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

        public virtual DbSet<TArticle> TArticle { get; set; }
        public virtual DbSet<TCategory> TCategory { get; set; }
        public virtual DbSet<TChannel> TChannel { get; set; }
        public virtual DbSet<TFile> TFile { get; set; }
        public virtual DbSet<TUserSys> TUserSys { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. See http://go.microsoft.com/fwlink/?LinkId=723263 for guidance on storing connection strings.
                optionsBuilder.UseMySQL("server=cloud.blackbaby.net;userid=webcore;pwd=webcore@321;port=3306;database=webcore;sslmode=none;");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasAnnotation("ProductVersion", "2.2.4-servicing-10062");

            modelBuilder.Entity<TArticle>(entity =>
            {
                entity.ToTable("t_article", "webcore");

                entity.HasIndex(e => e.Categoryid);

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .HasColumnType("int(11)");

                entity.Property(e => e.Abstract)
                    .IsRequired()
                    .HasColumnName("abstract")
                    .HasMaxLength(1000)
                    .IsUnicode(false);

                entity.Property(e => e.Categoryid)
                    .HasColumnName("categoryid")
                    .HasColumnType("int(11)");

                entity.Property(e => e.Clickcount)
                    .HasColumnName("clickcount")
                    .HasColumnType("int(11)");

                entity.Property(e => e.Commentstate)
                    .HasColumnName("commentstate")
                    .HasColumnType("bit(1)");

                entity.Property(e => e.Content)
                    .IsRequired()
                    .HasColumnName("content")
                    .IsUnicode(false);

                entity.Property(e => e.Cover)
                    .HasColumnName("cover")
                    .HasMaxLength(100)
                    .IsUnicode(false);

                entity.Property(e => e.Createtime).HasColumnName("createtime");

                entity.Property(e => e.Openstate)
                    .HasColumnName("openstate")
                    .HasColumnType("bit(1)");

                entity.Property(e => e.Recommendstate)
                    .HasColumnName("recommendstate")
                    .HasColumnType("bit(1)");

                entity.Property(e => e.Seod)
                    .HasColumnName("seod")
                    .HasMaxLength(500)
                    .IsUnicode(false);

                entity.Property(e => e.Seok)
                    .HasColumnName("seok")
                    .HasMaxLength(500)
                    .IsUnicode(false);

                entity.Property(e => e.Seot)
                    .HasColumnName("seot")
                    .HasMaxLength(500)
                    .IsUnicode(false);

                entity.Property(e => e.Sort)
                    .HasColumnName("sort")
                    .HasColumnType("int(11)");

                entity.Property(e => e.Title)
                    .IsRequired()
                    .HasColumnName("title")
                    .HasMaxLength(200)
                    .IsUnicode(false);

                entity.Property(e => e.Updatetime).HasColumnName("updatetime");

                entity.HasOne(d => d.Category)
                    .WithMany(p => p.TArticle)
                    .HasForeignKey(d => d.Categoryid)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK__t_article__categ__4F7CD00D");
            });

            modelBuilder.Entity<TCategory>(entity =>
            {
                entity.ToTable("t_category", "webcore");

                entity.HasIndex(e => e.Channelid);

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .HasColumnType("int(11)");

                entity.Property(e => e.Channelid)
                    .HasColumnName("channelid")
                    .HasColumnType("int(11)");

                entity.Property(e => e.Createtime).HasColumnName("createtime");

                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasColumnName("name")
                    .HasMaxLength(500)
                    .IsUnicode(false);

                entity.Property(e => e.Parentid)
                    .HasColumnName("parentid")
                    .HasColumnType("int(11)");

                entity.Property(e => e.Remark)
                    .HasColumnName("remark")
                    .IsUnicode(false);

                entity.Property(e => e.Seod)
                    .HasColumnName("seod")
                    .HasMaxLength(500)
                    .IsUnicode(false);

                entity.Property(e => e.Seok)
                    .HasColumnName("seok")
                    .HasMaxLength(500)
                    .IsUnicode(false);

                entity.Property(e => e.Seot)
                    .HasColumnName("seot")
                    .HasMaxLength(500)
                    .IsUnicode(false);

                entity.Property(e => e.Sort)
                    .HasColumnName("sort")
                    .HasColumnType("int(11)");

                entity.Property(e => e.Updatetime).HasColumnName("updatetime");

                entity.Property(e => e.Urlrole)
                    .HasColumnName("urlrole")
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.HasOne(d => d.Channel)
                    .WithMany(p => p.TCategory)
                    .HasForeignKey(d => d.Channelid)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK__t_categor__chann__5070F446");
            });

            modelBuilder.Entity<TChannel>(entity =>
            {
                entity.ToTable("t_channel", "webcore");

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .HasColumnType("int(11)");

                entity.Property(e => e.Createtime).HasColumnName("createtime");

                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasColumnName("name")
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.Remark)
                    .HasColumnName("remark")
                    .HasMaxLength(100)
                    .IsUnicode(false);

                entity.Property(e => e.Seod)
                    .HasColumnName("seod")
                    .HasMaxLength(500)
                    .IsUnicode(false);

                entity.Property(e => e.Seok)
                    .HasColumnName("seok")
                    .HasMaxLength(500)
                    .IsUnicode(false);

                entity.Property(e => e.Seot)
                    .HasColumnName("seot")
                    .HasMaxLength(500)
                    .IsUnicode(false);

                entity.Property(e => e.Sort)
                    .HasColumnName("sort")
                    .HasColumnType("int(11)");

                entity.Property(e => e.Updatetime).HasColumnName("updatetime");

                entity.Property(e => e.Urlrole)
                    .HasColumnName("urlrole")
                    .HasMaxLength(50)
                    .IsUnicode(false);
            });

            modelBuilder.Entity<TFile>(entity =>
            {
                entity.ToTable("t_file", "webcore");

                entity.HasIndex(e => e.Createuserid)
                    .HasName("createuserid");

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .HasMaxLength(64)
                    .IsUnicode(false)
                    .ValueGeneratedNever();

                entity.Property(e => e.Createtime).HasColumnName("createtime");

                entity.Property(e => e.Createuserid)
                    .IsRequired()
                    .HasColumnName("createuserid")
                    .HasMaxLength(64)
                    .IsUnicode(false);

                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasColumnName("name")
                    .HasMaxLength(255)
                    .IsUnicode(false);

                entity.Property(e => e.Path)
                    .IsRequired()
                    .HasColumnName("path")
                    .HasMaxLength(255)
                    .IsUnicode(false);
            });

            modelBuilder.Entity<TUserSys>(entity =>
            {
                entity.ToTable("t_user_sys", "webcore");

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .HasColumnType("int(11)");

                entity.Property(e => e.Createtime).HasColumnName("createtime");

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

                entity.Property(e => e.Updatetime).HasColumnName("updatetime");
            });
        }
    }
}
