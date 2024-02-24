using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Repository.Attributes;
using Repository.Bases;
using Repository.ValueConverters;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using System.Xml;

namespace Repository.Database
{
    public class DatabaseContext(DbContextOptions<DatabaseContext> options) : DbContext(options)
    {


        #region 表实体声明

        public DbSet<TAppSetting> TAppSetting { get; set; }


        public DbSet<TArticle> TArticle { get; set; }


        public DbSet<TCategory> TCategory { get; set; }



        public DbSet<TDataUpdateLog> TDataUpdateLog { get; set; }



        public DbSet<TFile> TFile { get; set; }


        public DbSet<TFunction> TFunction { get; set; }


        public DbSet<TFunctionAuthorize> TFunctionAuthorize { get; set; }


        public DbSet<TFunctionRoute> TFunctionRoute { get; set; }


        public DbSet<TLink> TLink { get; set; }


        public DbSet<TLog> TLog { get; set; }


        public DbSet<TOrder> TOrder { get; set; }


        public DbSet<TOrderDetail> TOrderDetail { get; set; }


        public DbSet<TProduct> TProduct { get; set; }


        public DbSet<TQueueTask> TQueueTask { get; set; }


        public DbSet<TRegionArea> TRegionArea { get; set; }


        public DbSet<TRegionCity> TRegionCity { get; set; }


        public DbSet<TRegionProvince> TRegionProvince { get; set; }


        public DbSet<TRegionTown> TRegionTown { get; set; }


        public DbSet<TRole> TRole { get; set; }


        public DbSet<TTaskSetting> TTaskSetting { get; set; }


        public DbSet<TUser> TUser { get; set; }


        public DbSet<TUserBindExternal> TUserBindExternal { get; set; }


        public DbSet<TUserInfo> TUserInfo { get; set; }


        public DbSet<TUserRole> TUserRole { get; set; }


        public DbSet<TUserToken> TUserToken { get; set; }


        #endregion



        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {

            #region Json列映射关系维护

            List<JsonColumnOwnedNavigation> jsonBuilders = [];

            var tableEntityTypes = this.GetType().GetProperties().Where(t => t.PropertyType.Name == "DbSet`1").Select(t => t.PropertyType.GetGenericArguments()[0]).ToList();


            foreach (var entityType in tableEntityTypes)
            {
                jsonBuilders.AddRange(GetJsonColumnOwnedNavigationList(entityType.GetProperties()));
            }

            foreach (var jsonBuilder in jsonBuilders)
            {
                modelBuilder.Entity(jsonBuilder.EntityTypeName, builder =>
                {
                    if (jsonBuilder.IsOwnsMany)
                    {
                        builder.OwnsMany(jsonBuilder.OwnedTypeName, jsonBuilder.NavigationName, b =>
                        {
                            b.ToJson();
                            jsonBuilder.ChildList.ForEach(c => JsonOwnedNavigationBuilder(b, c));
                        });
                    }
                    else
                    {
                        builder.OwnsOne(jsonBuilder.OwnedTypeName, jsonBuilder.NavigationName, b =>
                        {
                            b.ToJson();
                            jsonBuilder.ChildList.ForEach(c => JsonOwnedNavigationBuilder(b, c));
                        });
                    }
                });
            }

            #endregion

            #region 为所有实体的 AesEncrypted 字段添加转换器
            foreach (var entity in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var property in entity.GetProperties())
                {
                    if (property.PropertyInfo?.GetCustomAttribute<AesEncryptedAttribute>() != null)
                    {
                        if (property.ClrType == typeof(string))
                        {
                            property.SetValueConverter(AesValueConverter.aesConverter);
                        }
                        else
                        {
                            throw new Exception("非 string 类型的字段无法添加 AesEncrypted");
                        }
                    }
                }
            }
            #endregion

            foreach (var entity in modelBuilder.Model.GetEntityTypes().Where(t => t.IsMappedToJson() == false))
            {

                //添加全局过滤器
                globalHasQueryFilter.MakeGenericMethod(entity.ClrType).Invoke(null, new object[] { modelBuilder });

#if DEBUG
                //关闭表外键的级联删除功能
                entity.GetForeignKeys().ToList().ForEach(t => t.DeleteBehavior = DeleteBehavior.Restrict);
#endif

                modelBuilder.Entity(entity.Name, builder =>
                {
                    //设置生成数据库时的表名移除前缀T
                    var tableName = builder.Metadata.ClrType.GetCustomAttribute<TableAttribute>()?.Name ?? (entity.ClrType.Name[1..]);
                    builder.ToTable(tableName);

#if DEBUG
                    //设置表的备注
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
                        //设置字段的备注
                        property.SetComment(GetEntityComment(entity.Name, property.Name, baseTypeNames));

                        //设置字段的默认值 
                        var defaultValueAttribute = property.PropertyInfo?.GetCustomAttribute<DefaultValueAttribute>();
                        if (defaultValueAttribute != null)
                        {
                            property.SetDefaultValue(defaultValueAttribute.Value);
                        }

                        //为所有 tableid 列添加索引
                        if (property.Name.Equals("tableid", StringComparison.OrdinalIgnoreCase))
                        {
                            builder.HasIndex(property.Name);
                        }
                    }
#endif

                });
            }
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


        #region Json列映射逻辑

        private class JsonColumnOwnedNavigation
        {
            public string EntityTypeName { get; set; }

            public string OwnedTypeName { get; set; }

            public string NavigationName { get; set; }

            public bool IsOwnsMany { get; set; }

            public List<JsonColumnOwnedNavigation> ChildList { get; set; }
        }


        private List<JsonColumnOwnedNavigation> GetJsonColumnOwnedNavigationList(PropertyInfo[] propertyInfos)
        {
            List<JsonColumnOwnedNavigation> jsonColumnOwnedNavigations = [];

            foreach (var propertyInfo in propertyInfos)
            {
                var jsonColumnAttribute = propertyInfo.GetCustomAttribute<JsonColumnAttribute>();

                if (jsonColumnAttribute != null)
                {

                    var isOwnsMany = propertyInfo.PropertyType.Name == "List`1";

                    JsonColumnOwnedNavigation jsonColumnOwnedNavigation = new()
                    {
                        EntityTypeName = propertyInfo.DeclaringType!.FullName!,
                        OwnedTypeName = isOwnsMany ? propertyInfo.PropertyType.GetGenericArguments().First().FullName! : propertyInfo.PropertyType.FullName!,
                        NavigationName = propertyInfo.Name,
                        IsOwnsMany = isOwnsMany
                    };

                    var clrType = isOwnsMany ? propertyInfo.PropertyType.GetGenericArguments().First() : propertyInfo.PropertyType;

                    jsonColumnOwnedNavigation.ChildList = GetJsonColumnOwnedNavigationList(clrType.GetProperties());

                    jsonColumnOwnedNavigations.Add(jsonColumnOwnedNavigation);

                }
            }

            return jsonColumnOwnedNavigations;
        }


        private void JsonOwnedNavigationBuilder(OwnedNavigationBuilder ownedNavigationBuilder, JsonColumnOwnedNavigation jsonColumnOwnedNavigation)
        {
            if (jsonColumnOwnedNavigation.IsOwnsMany)
            {
                ownedNavigationBuilder.OwnsMany(jsonColumnOwnedNavigation.OwnedTypeName, jsonColumnOwnedNavigation.NavigationName, b =>
                {
                    jsonColumnOwnedNavigation.ChildList.ForEach(c => JsonOwnedNavigationBuilder(b, c));
                });
            }
            else
            {
                ownedNavigationBuilder.OwnsOne(jsonColumnOwnedNavigation.OwnedTypeName, jsonColumnOwnedNavigation.NavigationName, b =>
                {
                    jsonColumnOwnedNavigation.ChildList.ForEach(c => JsonOwnedNavigationBuilder(b, c));
                });
            }
        }

        #endregion


        #region 全局逻辑删除过滤器

        private static void GlobalHasQueryFilter<T>(ModelBuilder builder) where T : CD
        {
            builder.Entity<T>().HasQueryFilter(e => e.IsDelete == false);
        }


        private static readonly MethodInfo globalHasQueryFilter = typeof(DatabaseContext).GetMethod("GlobalHasQueryFilter", BindingFlags.Static | BindingFlags.NonPublic)!;

        #endregion

    }
}
