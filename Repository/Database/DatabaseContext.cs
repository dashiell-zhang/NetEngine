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

        public DbSet<TAppSetting> TAppSetting => Set<TAppSetting>();


        public DbSet<TArticle> TArticle => Set<TArticle>();


        public DbSet<TCategory> TCategory => Set<TCategory>();



        public DbSet<TDataUpdateLog> TDataUpdateLog => Set<TDataUpdateLog>();



        public DbSet<TFile> TFile => Set<TFile>();


        public DbSet<TFunction> TFunction => Set<TFunction>();


        public DbSet<TFunctionAction> TFunctionAction => Set<TFunctionAction>();


        public DbSet<TFunctionAuthorize> TFunctionAuthorize => Set<TFunctionAuthorize>();


        public DbSet<TLink> TLink => Set<TLink>();


        public DbSet<TLog> TLog => Set<TLog>();


        public DbSet<TOrder> TOrder => Set<TOrder>();


        public DbSet<TOrderDetail> TOrderDetail => Set<TOrderDetail>();


        public DbSet<TProduct> TProduct => Set<TProduct>();


        public DbSet<TRegionArea> TRegionArea => Set<TRegionArea>();


        public DbSet<TRegionCity> TRegionCity => Set<TRegionCity>();


        public DbSet<TRegionProvince> TRegionProvince => Set<TRegionProvince>();


        public DbSet<TRegionTown> TRegionTown => Set<TRegionTown>();


        public DbSet<TRole> TRole => Set<TRole>();


        public DbSet<TUser> TUser => Set<TUser>();


        public DbSet<TUserBindExternal> TUserBindExternal => Set<TUserBindExternal>();


        public DbSet<TUserInfo> TUserInfo => Set<TUserInfo>();


        public DbSet<TUserRole> TUserRole => Set<TUserRole>();


        public DbSet<TUserToken> TUserToken => Set<TUserToken>();


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

                    //开启 PostgreSQL 全库行并发乐观锁
                    builder.UseXminAsConcurrencyToken();


                    //设置生成数据库时的表名移除前缀T
                    var tableName = builder.Metadata.ClrType.CustomAttributes.Where(t => t.AttributeType.Name == "TableAttribute").Select(t => t.ConstructorArguments.Select(c => c.Value?.ToString()).FirstOrDefault()).FirstOrDefault() ?? (entity.ClrType.Name[1..]);
                    builder.ToTable(tableName);


#if DEBUG
                    //设置表的备注
                    builder.HasComment(GetEntityComment(entity.Name));

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
            var path = AppContext.BaseDirectory + "/Repository.xml";
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
