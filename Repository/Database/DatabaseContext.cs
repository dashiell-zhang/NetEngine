using Microsoft.EntityFrameworkCore;
using System.ComponentModel;
using System.Reflection;
using System.Xml;

namespace Repository.Database
{
    public class DatabaseContext : DbContext
    {


        public DatabaseContext(DbContextOptions<DatabaseContext> options) : base(options) { }


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


        public DbSet<TRegionArea> TRegionArea { get; set; }


        public DbSet<TRegionCity> TRegionCity { get; set; }


        public DbSet<TRegionProvince> TRegionProvince { get; set; }


        public DbSet<TRegionTown> TRegionTown { get; set; }


        public DbSet<TRole> TRole { get; set; }


        public DbSet<TUser> TUser { get; set; }


        public DbSet<TUserBindExternal> TUserBindExternal { get; set; }


        public DbSet<TUserInfo> TUserInfo { get; set; }


        public DbSet<TUserRole> TUserRole { get; set; }


        public DbSet<TUserToken> TUserToken { get; set; }


        #endregion



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

                    //设置生成数据库时的表名移除前缀T
                    var tableName = builder.Metadata.ClrType.CustomAttributes.Where(t => t.AttributeType.Name == "TableAttribute").Select(t => t.ConstructorArguments.Select(c => c.Value?.ToString()).FirstOrDefault()).FirstOrDefault() ?? (entity.ClrType.Name[1..]);
                    builder.ToTable(tableName);


#if DEBUG
                    //设置表的备注
                    builder.ToTable(t => t.HasComment(GetEntityComment(entity.Name)));

                    List<string> baseTypeNames = new();
                    var baseType = entity.ClrType.BaseType;
                    while (baseType != null)
                    {
                        baseTypeNames.Add(baseType.FullName!);
                        baseType = baseType.BaseType;
                    }
#endif


                    foreach (var property in entity.GetProperties())
                    {

#if DEBUG
                        //设置字段的备注
                        property.SetComment(GetEntityComment(entity.Name, property.Name, baseTypeNames));
#endif

                        //设置字段的默认值 
                        var defaultValueAttribute = property.PropertyInfo?.GetCustomAttribute<DefaultValueAttribute>();
                        if (defaultValueAttribute != null)
                        {
                            property.SetDefaultValue(defaultValueAttribute.Value);
                        }


                        //为所有 tableid 列添加索引
                        if (property.Name.ToLower() == "tableid")
                        {
                            builder.HasIndex(property.Name);
                        }

                    }
                });
            }
        }




        public static string GetEntityComment(string typeName, string? fieldName = null, List<string>? baseTypeNames = null)
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Repository.xml");
            XmlDocument xml = new();
            xml.Load(path);
            XmlNodeList memebers = xml.SelectNodes("/doc/members/member")!;

            Dictionary<string, string> fieldList = new();

            if (fieldName == null)
            {
                var matchKey = "T:" + typeName;

                foreach (object m in memebers)
                {
                    if (m is XmlNode node)
                    {
                        var name = node.Attributes!["name"]!.Value;

                        var summary = node.InnerText.Trim();

                        if (name == matchKey)
                        {
                            fieldList.Add(name, summary);
                        }
                    }
                }

                return fieldList.FirstOrDefault(t => t.Key.ToLower() == matchKey.ToLower()).Value ?? typeName.ToString().Split(".").ToList().LastOrDefault()!;
            }
            else
            {

                foreach (object m in memebers)
                {
                    if (m is XmlNode node)
                    {
                        string name = node.Attributes!["name"]!.Value;

                        var summary = node.InnerText.Trim();

                        var matchKey = "P:" + typeName + ".";
                        if (name.StartsWith(matchKey))
                        {
                            name = name.Replace(matchKey, "");

                            if (fieldList.ContainsKey(name))
                            {
                                fieldList.Remove(name);
                            }

                            fieldList.Add(name, summary);
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
                                        fieldList.Add(name, summary);
                                    }
                                }
                            }
                        }

                    }
                }

                return fieldList.FirstOrDefault(t => t.Key.ToLower() == fieldName.ToLower()).Value ?? fieldName;
            }
        }


    }
}
