using Microsoft.EntityFrameworkCore;
using Repository.Database.Generated;
using System.Reflection;
using System.Xml;

namespace Repository.Database;

public class DatabaseContext(DbContextOptions<DatabaseContext> options) : DbContext(options)
{


    #region 表实体声明

    public DbSet<AppSetting> AppSetting { get; set; }

    public DbSet<Article> Article { get; set; }

    public DbSet<Category> Category { get; set; }

    public DbSet<DataUpdateLog> DataUpdateLog { get; set; }

    public DbSet<File> File { get; set; }

    public DbSet<Function> Function { get; set; }

    public DbSet<FunctionAuthorize> FunctionAuthorize { get; set; }

    public DbSet<FunctionRoute> FunctionRoute { get; set; }

    public DbSet<Link> Link { get; set; }

    public DbSet<Log> Log { get; set; }

    public DbSet<Order> Order { get; set; }

    public DbSet<OrderDetail> OrderDetail { get; set; }

    public DbSet<Product> Product { get; set; }

    public DbSet<QueueTask> QueueTask { get; set; }

    public DbSet<RegionArea> RegionArea { get; set; }

    public DbSet<RegionCity> RegionCity { get; set; }

    public DbSet<RegionProvince> RegionProvince { get; set; }

    public DbSet<RegionTown> RegionTown { get; set; }

    public DbSet<Role> Role { get; set; }

    public DbSet<TaskSetting> TaskSetting { get; set; }

    public DbSet<User> User { get; set; }

    public DbSet<UserBindExternal> UserBindExternal { get; set; }

    public DbSet<UserInfo> UserInfo { get; set; }

    public DbSet<UserRole> UserRole { get; set; }

    public DbSet<UserToken> UserToken { get; set; }

    #endregion


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyAesEncryptedConverters();

        modelBuilder.ApplySoftDeleteFilters();

        modelBuilder.ApplyJsonColumns();


#if DEBUG

        var dbSetTypeList = GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p =>
         p.PropertyType.IsGenericType &&
         p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
     .Select(p => p.PropertyType.GetGenericArguments()[0])
     .ToList();

        var entityTypesInDbSet = modelBuilder.Model.GetEntityTypes().Where(e => dbSetTypeList.Contains(e.ClrType)).ToList();

        foreach (var entity in entityTypesInDbSet)
        {

            // 关闭表外键的级联删除功能
            entity.GetForeignKeys().ToList().ForEach(t => t.DeleteBehavior = DeleteBehavior.Restrict);

            modelBuilder.Entity(entity.Name, builder =>
            {
                // 设置表的备注
                builder.ToTable(t => t.HasComment(GetEntityComment(entity.Name)));

                List<string> baseTypeNames = [];
                var baseType = entity.ClrType.BaseType;
                while (baseType != null)
                {
                    baseTypeNames.Add(baseType.FullName!);
                    baseType = baseType.BaseType;
                }

                foreach (var property in entity.GetProperties())
                {
                    // 设置字段的备注
                    property.SetComment(GetEntityComment(entity.Name, property.Name, baseTypeNames));
                }

            });
        }
#endif
    }


    public static string GetEntityComment(string typeName, string? fieldName = null, List<string>? baseTypeNames = null)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Repository.xml");
        XmlDocument xml = new();
        xml.Load(path);
        XmlNodeList memebers = xml.SelectNodes("/doc/members/member")!;

        Dictionary<string, string> fieldList = [];


        if (fieldName == null)
        {
            var matchKey = "T:" + typeName;

            foreach (object m in memebers)
            {
                if (m is XmlNode node)
                {
                    var name = node.Attributes!["name"]!.Value;

                    if (name == matchKey)
                    {
                        foreach (var item in node.ChildNodes)
                        {
                            if (item is XmlNode childNode && childNode.Name == "summary")
                            {
                                var summary = childNode.InnerText.Trim();
                                fieldList.Add(name, summary);
                            }
                        }
                    }
                }
            }

            return fieldList.FirstOrDefault(t => t.Key.Equals(matchKey, StringComparison.OrdinalIgnoreCase)).Value ?? typeName.ToString().Split(".").ToList().LastOrDefault()!;
        }
        else
        {

            foreach (object m in memebers)
            {
                if (m is XmlNode node)
                {
                    string name = node.Attributes!["name"]!.Value;

                    var matchKey = "P:" + typeName + ".";
                    if (name.StartsWith(matchKey))
                    {
                        name = name.Replace(matchKey, "");

                        fieldList.Remove(name);

                        foreach (var item in node.ChildNodes)
                        {
                            if (item is XmlNode childNode && childNode.Name == "summary")
                            {
                                var summary = childNode.InnerText.Trim();
                                fieldList.Add(name, summary);
                            }
                        }
                    }

                    if (baseTypeNames != null)
                    {
                        foreach (var baseTypeName in baseTypeNames)
                        {
                            if (baseTypeName != null)
                            {
                                matchKey = "P:" + baseTypeName + ".";
                                if (name.StartsWith(matchKey))
                                {
                                    name = name.Replace(matchKey, "");

                                    foreach (var item in node.ChildNodes)
                                    {
                                        if (item is XmlNode childNode && childNode.Name == "summary")
                                        {
                                            var summary = childNode.InnerText.Trim();
                                            fieldList.Add(name, summary);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return fieldList.FirstOrDefault(t => t.Key.Equals(fieldName, StringComparison.OrdinalIgnoreCase)).Value ?? fieldName;
        }
    }


    internal void PreprocessingChangeTracker()
    {
        var list = this.ChangeTracker.Entries().Where(t => t.State == EntityState.Modified).ToList();

        foreach (var item in list)
        {
            var isValidUpdate = item.Properties.Where(t => t.IsModified && t.Metadata.Name != "UpdateTime" && t.Metadata.Name != "UpdateUserId").Any();

            if (!isValidUpdate)
            {
                item.State = EntityState.Unchanged;
                continue;
            }

            var updateTime = item.Properties.Where(t => t.Metadata.Name == "UpdateTime").FirstOrDefault();

            if (updateTime != null && updateTime.IsModified == false)
            {
                updateTime.CurrentValue = DateTimeOffset.UtcNow;
            }


            var isDelete = item.Properties.Where(t => t.Metadata.Name == "IsDelete").FirstOrDefault();

            if (isDelete != null && isDelete.IsModified == true && Convert.ToBoolean(isDelete.CurrentValue) == true)
            {
                var deleteTime = item.Properties.Where(t => t.Metadata.Name == "DeleteTime").FirstOrDefault();

                if (deleteTime != null && deleteTime.IsModified == false)
                {
                    deleteTime.CurrentValue = DateTimeOffset.UtcNow;
                }
            }

        }
    }


    public override int SaveChanges()
    {
        PreprocessingChangeTracker();

        return base.SaveChanges();
    }


    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        PreprocessingChangeTracker();

        return base.SaveChangesAsync(cancellationToken);
    }

}
