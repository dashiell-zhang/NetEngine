using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Xml;

namespace Repository.Database
{
    public class DatabaseContext : DbContext
    {


        public static string ConnectionString { get; set; } = null!;



        public DatabaseContext(DbContextOptions<DatabaseContext> options = default!) : base(GetDbContextOptions(options))
        {
        }




        public DbSet<TAppSetting> TAppSetting => Set<TAppSetting>();


        public DbSet<TArticle> TArticle => Set<TArticle>();


        public DbSet<TCategory> TCategory => Set<TCategory>();


        public DbSet<TChannel> TChannel => Set<TChannel>();


        public DbSet<TCount> TCount => Set<TCount>();


        public DbSet<TOSLog> TOSLog => Set<TOSLog>();


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


        public DbSet<TSign> TSign => Set<TSign>();


        public DbSet<TUser> TUser => Set<TUser>();


        public DbSet<TUserBindExternal> TUserBindExternal => Set<TUserBindExternal>();


        public DbSet<TUserInfo> TUserInfo => Set<TUserInfo>();


        public DbSet<TUserRole> TUserRole => Set<TUserRole>();


        public DbSet<TUserToken> TUserToken => Set<TUserToken>();




        private static DbContextOptions<DatabaseContext> GetDbContextOptions(DbContextOptions<DatabaseContext> options = default!)
        {


            var optionsBuilder = new DbContextOptionsBuilder<DatabaseContext>();

            if (options != default)
            {
                optionsBuilder = new DbContextOptionsBuilder<DatabaseContext>(options);
            }

            if (!optionsBuilder.IsConfigured)
            {
                //SQLServer:"Data Source=127.0.0.1;Initial Catalog=webcore;User ID=sa;Password=123456;Max Pool Size=100;Encrypt=True"
                //MySQL:"server=127.0.0.1;database=webcore;user id=root;password=123456;maxpoolsize=100"
                //SQLite:"Data Source=../Repository/database.db"
                //PostgreSQL:"Host=127.0.0.1;Database=webcore;Username=postgres;Password=123456;Maximum Pool Size=30;SSL Mode=VerifyFull"

                //optionsBuilder.UseSqlServer(ConnectionString, o => o.MigrationsHistoryTable("__efmigrationshistory"));
                //optionsBuilder.UseMySQL(ConnectionString, o => o.MigrationsHistoryTable("__efmigrationshistory"));
                //optionsBuilder.UseMySql(ConnectionString, new MySqlServerVersion(new Version(8, 0, 27)), o => o.MigrationsHistoryTable("__efmigrationshistory"));
                //optionsBuilder.UseSqlite(ConnectionString, o => o.MigrationsHistoryTable("__efmigrationshistory"));
                optionsBuilder.UseNpgsql(ConnectionString, o => o.MigrationsHistoryTable("__efmigrationshistory"));
            }

            //开启调试拦截器
            //optionsBuilder.AddInterceptors(new DeBugInterceptor());



            //开启全局懒加载
            //optionsBuilder.UseLazyLoadingProxies();

            return optionsBuilder.Options;
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

                    //开启 PostgreSQL 全库行并发乐观锁
                    builder.UseXminAsConcurrencyToken();


                    //设置生成数据库时的表名为小写格式并添加前缀 t_
                    var tableName = builder.Metadata.ClrType.CustomAttributes.Where(t => t.AttributeType.Name == "TableAttribute").Select(t => t.ConstructorArguments.Select(c => c.Value?.ToString()).FirstOrDefault()).FirstOrDefault() ?? ("t_" + entity.ClrType.Name[1..]);
                    builder.ToTable(tableName.ToLower());


                    //设置表的备注
                    builder.HasComment(GetEntityComment(entity.Name));


                    foreach (var property in entity.GetProperties())
                    {

                        string columnName = property.GetColumnName(StoreObjectIdentifier.Create(property.DeclaringEntityType, StoreObjectType.Table)!.Value)!;


                        //设置字段名为小写
                        property.SetColumnName(columnName.ToLower());

                        var baseTypeNames = new List<string>();
                        var baseType = entity.ClrType.BaseType;
                        while (baseType != null)
                        {
                            baseTypeNames.Add(baseType.FullName!);
                            baseType = baseType.BaseType;
                        }


                        //设置字段的备注
                        property.SetComment(GetEntityComment(entity.Name, property.Name, baseTypeNames));


                        //设置字段的默认值 
                        var defaultValueAttribute = property.PropertyInfo?.GetCustomAttribute<DefaultValueAttribute>();
                        if (defaultValueAttribute != null)
                        {
                            property.SetDefaultValue(defaultValueAttribute.Value);
                        }


                        //bool to bit 使用 MySQL 时需要取消注释
                        //if (property.ClrType.Name == "Boolean" || property.ClrType.FullName.StartsWith("System.Nullable`1[[System.Boolean"))
                        //{
                        //    property.SetColumnType("bit");
                        //}


                        //guid to char(36) 使用 MySQL 并且采用 MySql.EntityFrameworkCore 时需要取消注释
                        //if (/*property.ClrType.Name == "Guid" ||*/ property.ClrType.FullName.StartsWith("System.Nullable`1[[System.Guid"))
                        //{
                        //    property.SetColumnType("char(36)");
                        //}


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
            var xml = new XmlDocument();
            xml.Load(path);
            XmlNodeList memebers = xml.SelectNodes("/doc/members/member")!;

            var fieldList = new Dictionary<string, string>();


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



        public static string ComparisonEntity<T>(T original, T after) where T : new()
        {
            var retValue = "";

            var fields = typeof(T).GetProperties();

            var baseTypeNames = new List<string>();
            var baseType = original?.GetType().BaseType;
            while (baseType != null)
            {
                baseTypeNames.Add(baseType.FullName!);
                baseType = baseType.BaseType;
            }

            for (int i = 0; i < fields.Length; i++)
            {
                PropertyInfo pi = fields[i];

                string? oldValue = pi.GetValue(original)?.ToString();
                string? newValue = pi.GetValue(after)?.ToString();

                string typename = pi.PropertyType.FullName!;

                if ((typename != "System.Decimal" && oldValue != newValue) || (typename == "System.Decimal" && decimal.Parse(oldValue!) != decimal.Parse(newValue!)))
                {

                    retValue += GetEntityComment(original!.GetType().ToString(), pi.Name, baseTypeNames) + ":";


                    if (pi.Name != "Id" && pi.Name.EndsWith("Id"))
                    {
                        var foreignTable = fields.FirstOrDefault(t => t.Name == pi.Name.Replace("Id", ""));

                        using var db = new DatabaseContext();
                        var foreignName = foreignTable?.PropertyType.GetProperties().Where(t => t.CustomAttributes.Where(c => c.AttributeType.Name == "ForeignNameAttribute").Any()).FirstOrDefault();

                        if (foreignName != null)
                        {

                            if (oldValue != null)
                            {
                                var oldForeignInfo = db.Find(foreignTable!.PropertyType, Guid.Parse(oldValue));
                                oldValue = foreignName.GetValue(oldForeignInfo)?.ToString();
                            }

                            if (newValue != null)
                            {
                                var newForeignInfo = db.Find(foreignTable!.PropertyType, Guid.Parse(newValue));
                                newValue = foreignName.GetValue(newForeignInfo)?.ToString();
                            }

                        }

                        retValue += (oldValue ?? "") + " -> ";
                        retValue += (newValue ?? "") + "； \n";

                    }
                    else if (typename == "System.Boolean")
                    {
                        retValue += (oldValue != null ? (bool.Parse(oldValue) ? "是" : "否") : "") + " -> ";
                        retValue += (newValue != null ? (bool.Parse(newValue) ? "是" : "否") : "") + "； \n";
                    }
                    else if (typename == "System.DateTime")
                    {
                        retValue += (oldValue != null ? DateTime.Parse(oldValue).ToString("yyyy-MM-dd") : "") + " ->";
                        retValue += (newValue != null ? DateTime.Parse(newValue).ToString("yyyy-MM-dd") : "") + "； \n";
                    }
                    else
                    {
                        retValue += (oldValue ?? "") + " -> ";
                        retValue += (newValue ?? "") + "； \n";
                    }

                }



            }

            return retValue;
        }


        ///// <summary>
        ///// 通用的RowVersion重写保存方法
        ///// </summary>
        ///// <returns></returns>
        //public override int SaveChanges()
        //{
        //    DatabaseContext db = this;
        //    var list = db.ChangeTracker.Entries().Where(t => t.State == EntityState.Modified).ToList();
        //    foreach (var item in list)
        //    {
        //        item.Entity.GetType().GetProperty("RowVersion")?.SetValue(item.Entity, Guid.NewGuid());
        //    }
        //    return base.SaveChanges();
        //}



        public int SaveChangesWithSaveLog(long osLogId, long? actionUserId, string? ipAddress, string? deviceMark)
        {

            DatabaseContext db = this;

            var list = db.ChangeTracker.Entries().Where(t => t.State == EntityState.Modified).ToList();

            foreach (var item in list)
            {

                var type = item.Entity.GetType();

                var oldEntity = item.OriginalValues.ToObject();

                var newEntity = item.CurrentValues.ToObject();

                var entityId = item.CurrentValues.GetValue<long>("Id");

                if (actionUserId == null)
                {
                    var isHaveUpdateUserId = item.Properties.Where(t => t.Metadata.Name == "UpdateUserId").Count();

                    if (isHaveUpdateUserId > 0)
                    {
                        actionUserId = item.CurrentValues.GetValue<long?>("UpdateUserId");
                    }
                }

                object[] parameters = { oldEntity, newEntity };

                string result = new DatabaseContext().GetType().GetMethod("ComparisonEntity")!.MakeGenericMethod(type).Invoke(new DatabaseContext(), parameters)!.ToString()!;

                if (result != "")
                {
                    if (ipAddress == null || deviceMark == null)
                    {
                        var assembly = Assembly.GetEntryAssembly();
                        var httpContextType = assembly!.GetTypes().Where(t => t.FullName!.Contains("Libraries.Http.HttpContext")).FirstOrDefault();

                        if (httpContextType != null)
                        {
                            if (ipAddress == null)
                            {
                                ipAddress = httpContextType.GetMethod("GetIpAddress", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, null)!.ToString()!;
                            }

                            if (deviceMark == null)
                            {
                                deviceMark = httpContextType.GetMethod("GetHeader", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, new object[] { "DeviceMark" })!.ToString()!;

                                if (deviceMark == "")
                                {
                                    deviceMark = httpContextType.GetMethod("GetHeader", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, new object[] { "User-Agent" })!.ToString()!;
                                }
                            }
                        }
                    }



                    TOSLog osLog = new(type.Name, "Modified", result);
                    osLog.Id = osLogId;
                    osLog.CreateTime = DateTime.UtcNow;
                    osLog.TableId = entityId;
                    osLog.IpAddress = ipAddress == "" ? null : ipAddress;
                    osLog.DeviceMark = deviceMark == "" ? null : deviceMark;
                    osLog.ActionUserId = actionUserId;

                    db.TOSLog.Add(osLog);
                }

            }

            return db.SaveChanges();
        }


    }
}
