using IdentifierGenerator;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Repository.Database;
using System.Data;
using System.Data.Common;
using System.Reflection;

namespace Repository.Extensions
{
    public static class DatabaseContextExtension
    {

        /// <summary>
        /// 针对数据库执行自定义的sql查询，返回DataTable
        /// </summary>
        /// <param name="db"></param>
        /// <param name="sql">自定义查询Sql</param>
        /// <param name="parameters">sql参数</param>
        /// <returns></returns>
        public static DataTable SelectFromSql(this DatabaseContext db, string sql, Dictionary<string, object>? parameters = default)
        {

            DbConnection connection = db.Database.GetDbConnection();

            connection.Open();

            var command = connection.CreateCommand();

            command.CommandTimeout = 600;

            command.CommandText = sql;

            if (parameters != default)
            {
                foreach (var item in parameters)
                {
                    var parameter = command.CreateParameter();
                    parameter.ParameterName = item.Key;
                    parameter.Value = item.Value;

                    command.Parameters.Add(parameter);
                }
            }

            var reader = command.ExecuteReader();

            DataTable dataTable = new();

            dataTable.Load(reader);

            reader.Close();
            command.Dispose();
            connection.Close();

            return dataTable;
        }




        /// <summary>
        /// 针对数据库执行自定义的sql
        /// </summary>
        /// <param name="db"></param>
        /// <param name="sql">自定义查询Sql</param>
        /// <param name="parameters">sql参数</param>
        /// <returns></returns>
        public static void ExecuteSql(this DatabaseContext db, string sql, Dictionary<string, object>? parameters = default)
        {

            DbConnection connection = db.Database.GetDbConnection();

            connection.Open();

            var command = connection.CreateCommand();

            command.CommandTimeout = 600;

            command.CommandText = sql;

            if (parameters != default)
            {
                foreach (var item in parameters)
                {
                    var parameter = command.CreateParameter();
                    parameter.ParameterName = item.Key;
                    parameter.Value = item.Value;

                    command.Parameters.Add(parameter);
                }
            }

            command.ExecuteNonQuery();

            command.Dispose();
            connection.Close();
        }



        /// <summary>
        /// 保存数据并记录更新日志
        /// </summary>
        /// <param name="db"></param>
        /// <param name="actionUserId"></param>
        /// <param name="ipAddress"></param>
        /// <param name="deviceMark"></param>
        /// <returns></returns>
        public static int SaveChangesWithUpdateLog(this DatabaseContext db, long? actionUserId = null, string? ipAddress = null, string? deviceMark = null)
        {

            var idService = db.Database.GetService<IdService>();

            db.PreprocessingChangeTracker();

            var list = db.ChangeTracker.Entries().Where(t => t.State == EntityState.Modified).ToList();

            foreach (var item in list)
            {
                var type = item.Entity.GetType();

                var entityType = db.Model.FindEntityType(type)!;

                string tableName = entityType.GetTableName()!;

                var oldEntity = item.OriginalValues.ToObject();

                var newEntity = item.CurrentValues.ToObject();

                var isMappedToJson = entityType.IsMappedToJson();

                long entityId = 0;

                if (isMappedToJson)
                {
                    string jsonPropertyName = "";

                    var ownership = entityType.FindOwnership();

                    if (ownership?.PrincipalEntityType?.IsMappedToJson() ?? false)
                    {
                        jsonPropertyName = entityType.GetJsonPropertyName()!;
                    }

                    var navigations = ownership?.PrincipalEntityType.FindNavigation(entityType.GetJsonPropertyName()!)!;

                    if (navigations.IsCollection)
                    {
                        //如果当前是 List 子表集合直接跳过日志记录（因为数据无唯一ID日志无意义）
                        continue;
                    }


                    while (ownership?.PrincipalEntityType != null)
                    {

                        if (ownership.PrincipalEntityType.IsMappedToJson())
                        {

                            if (ownership.PrincipalEntityType.FindOwnership()?.PrincipalEntityType?.IsMappedToJson() ?? false)
                            {
                                jsonPropertyName = ownership.PrincipalEntityType.GetJsonPropertyName() + "." + jsonPropertyName;
                            }
                        }
                        ownership = ownership.PrincipalEntityType.FindOwnership();

                    }


                    entityId = (long)item.Properties.Where(t => t.Metadata.IsPrimaryKey()).First().CurrentValue!;

                    if (jsonPropertyName != "")
                    {
                        tableName = tableName + "." + entityType.GetContainerColumnName() + "." + jsonPropertyName;
                    }
                    else
                    {
                        tableName = tableName + "." + entityType.GetContainerColumnName();
                    }

                }
                else
                {
                    entityId = item.CurrentValues.GetValue<long>("Id");
                }


                if (actionUserId == null)
                {
                    var isHaveUpdateUserId = item.Properties.Where(t => t.Metadata.Name == "UpdateUserId").Count();

                    if (isHaveUpdateUserId > 0)
                    {
                        actionUserId = item.CurrentValues.GetValue<long?>("UpdateUserId");
                    }
                }

                object[] parameters = [db, oldEntity, newEntity];


                string result = typeof(DatabaseContextExtension).GetMethod("ComparisonEntity")!.MakeGenericMethod(type).Invoke(null, parameters)!.ToString()!;

                if (result != "")
                {
                    if (ipAddress == null || deviceMark == null)
                    {
                        var assembly = Assembly.GetEntryAssembly();
                        var httpContextType = assembly!.GetTypes().Where(t => t.FullName!.Contains("Libraries.Http.HttpContext")).FirstOrDefault();

                        if (httpContextType != null)
                        {
                            ipAddress ??= httpContextType.GetMethod("GetIpAddress", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, null)!.ToString()!;

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



                    TDataUpdateLog osLog = new()
                    {
                        Id = idService.GetId(),
                        Table = tableName,
                        Content = result,
                        TableId = entityId,
                        IpAddress = ipAddress == "" ? null : ipAddress,
                        DeviceMark = deviceMark == "" ? null : deviceMark,
                        ActionUserId = actionUserId
                    };

                    db.TDataUpdateLog.Add(osLog);
                }

            }

            return db.SaveChanges();
        }



        /// <summary>
        /// 比较两个实体获取修改内容
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="db"></param>
        /// <param name="original"></param>
        /// <param name="after"></param>
        /// <returns></returns>
        public static string ComparisonEntity<T>(this DatabaseContext db, T original, T after) where T : new()
        {
            var retValue = "";

            var fields = typeof(T).GetProperties();

            List<string> baseTypeNames = [];
            var baseType = original?.GetType().BaseType;
            while (baseType != null)
            {
                baseTypeNames.Add(baseType.FullName!);
                baseType = baseType.BaseType;
            }

            for (int i = 0; i < fields.Length; i++)
            {
                PropertyInfo pi = fields[i];

                if (pi.DeclaringType != null && pi.DeclaringType.FullName != null && pi.DeclaringType.FullName.StartsWith("Castle.Proxies."))
                {
                    continue;
                }
                else
                {
                    string? oldValue = pi.GetValue(original)?.ToString();
                    string? newValue = pi.GetValue(after)?.ToString();

                    string typename = pi.PropertyType.FullName!;

                    if ((typename != "System.Decimal" && oldValue != newValue) || (typename == "System.Decimal" && decimal.Parse(oldValue!) != decimal.Parse(newValue!)))
                    {

                        retValue += DatabaseContext.GetEntityComment(original!.GetType().ToString(), pi.Name, baseTypeNames) + ":";


                        if (pi.Name != "Id" && pi.Name.EndsWith("Id"))
                        {
                            var foreignTable = fields.FirstOrDefault(t => t.Name == pi.Name.Replace("Id", ""));

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

            }

            return retValue;
        }


    }
}
