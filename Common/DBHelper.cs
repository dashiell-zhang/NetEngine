using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Repository.Database;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Xml;

namespace Common
{
    public class DBHelper
    {

        /// <summary>
        /// 针对数据库执行自定义的sql查询，返回泛型List，可自定义数据库
        /// </summary>
        /// <typeparam name="T">返回类型</typeparam>
        /// <param name="sql">自定义查询Sql</param>
        /// <param name="parameters">sql参数</param>
        /// <param name="connection">数据库连接</param>
        /// <remarks>connection = db.Database.GetDbConnection()</remarks>
        /// <returns></returns>
        public static IList<T> SelectFromSql<T>(string sql, Dictionary<string, object> parameters = default, DbConnection connection = default) where T : class
        {

            if (connection == default)
            {
                using var db = new DatabaseContext();
                connection = db.Database.GetDbConnection();
            }

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

            var dataTable = new DataTable();

            dataTable.Load(reader);

            reader.Close();
            command.Dispose();
            connection.Close();

            var list = DataHelper.DataTableToList<T>(dataTable);

            return list;
        }





        /// <summary>
        /// 针对数据库执行自定义的sql
        /// </summary>
        /// <param name="sql">自定义查询Sql</param>
        /// <param name="parameters">sql参数</param>
        /// <param name="connection">自定义数据库连接</param>
        /// <remarks>connection = db.Database.GetDbConnection()</remarks>
        /// <returns></returns>
        public static void ExecuteSql(string sql, Dictionary<string, object> parameters = default, DbConnection connection = default)
        {

            if (connection == default)
            {
                using var db = new DatabaseContext();
                connection = db.Database.GetDbConnection();
            }

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
        /// 保存日志信息
        /// </summary>
        /// <param name="Sign">自定义标记</param>
        /// <param name="Type">日志类型</param>
        /// <param name="Content">日志内容</param>
        /// <returns></returns>
        public static bool LogSet(string Sign, string Type, string Content)
        {
            try
            {
                using (var db = new DatabaseContext())
                {

                    Type programType = Assembly.GetEntryAssembly()!.GetTypes().Where(t => t.Name == "Program").FirstOrDefault()!;
                    IServiceProvider serviceProvider = (IServiceProvider)programType.GetProperty("ServiceProvider", BindingFlags.Public | BindingFlags.Static)!.GetValue(programType)!;
                    var snowflakeHelper = serviceProvider.GetService<SnowflakeHelper>()!;

                    TLog log = new();

                    log.Id = snowflakeHelper.GetId();
                    log.Sign = Sign;
                    log.Type = Type;
                    log.Content = Content;
                    log.CreateTime = DateTime.UtcNow;

                    db.TLog.Add(log);

                    db.SaveChanges();
                }
                return true;
            }
            catch
            {
                return false;
            }
        }




        /// <summary>
        /// 为指定类型的计数 +1
        /// </summary>
        /// <param name="tag">标签</param>
        /// <returns></returns>
        public static int RunCountSet(string tag)
        {
            using var db = new DatabaseContext();

            var info = db.TCount.Where(t => t.Tag == tag).FirstOrDefault() ?? new TCount();

            if (info.Id != default)
            {
                info.Count++;
                info.UpdateTime = DateTime.UtcNow;

                db.SaveChanges();

                return info.Count;
            }
            else
            {

                var programType = Assembly.GetEntryAssembly()!.GetTypes().Where(t => t.Name == "Program").FirstOrDefault()!;
                var serviceProvider = (IServiceProvider)programType.GetProperty("ServiceProvider", BindingFlags.Public | BindingFlags.Static)!.GetValue(programType)!;
                var snowflakeHelper = serviceProvider.GetService<SnowflakeHelper>()!;

                info.Id = snowflakeHelper.GetId();
                info.Tag = tag;
                info.Count = 1;
                info.CreateTime = DateTime.UtcNow;

                db.TCount.Add(info);

                db.SaveChanges();

                return info.Count;

            }
        }



        /// <summary>
        /// 通过类型获取计数值
        /// </summary>
        /// <param name="tag">标记</param>
        /// <param name="starttime">开始时间</param>
        /// <param name="endtime">结束时间</param>
        /// <returns></returns>
        public static int RunCountGet(string tag, DateTime starttime = default, DateTime endtime = default)
        {

            using var db = new DatabaseContext();
            var query = db.TCount.Where(t => t.Tag.Contains(tag));

            if (starttime != default & endtime != default)
            {
                query = query.Where(t => t.CreateTime >= starttime & t.CreateTime <= endtime);
            }

            return query.ToList().Sum(t => t.Count);

        }




        /// <summary>
        /// 获取一个表的注释信息
        /// </summary>
        /// <typeparam name="T">表的实体类型</typeparam>
        /// <param name="fieldName">字段名称</param>
        /// <remarks>字段名称为空时返回表的注释</remarks>
        /// <returns></returns>
        public static string GetEntityComment<T>(string fieldName = null) where T : new()
        {
            var path = AppContext.BaseDirectory + "/Repository.xml";
            var xml = new XmlDocument();
            xml.Load(path);

            var fieldList = new Dictionary<string, string>();

            var memebers = xml.SelectNodes("/doc/members/member");

            var t = new T();


            if (fieldName == null)
            {
                var matchKey = "T:" + t.ToString();

                foreach (object m in memebers)
                {
                    if (m is XmlNode node)
                    {
                        var name = node.Attributes["name"].Value;

                        var summary = node.InnerText.Trim();

                        if (name == matchKey)
                        {
                            fieldList.Add(name, summary);
                        }
                    }
                }

                return fieldList.FirstOrDefault(t => t.Key.ToLower() == matchKey.ToLower()).Value ?? t.ToString().Split(".").ToList().LastOrDefault();
            }
            else
            {
                var baseTypeNames = new List<string>();
                var baseType = t.GetType().BaseType;
                while (baseType != null)
                {
                    baseTypeNames.Add(baseType.FullName);
                    baseType = baseType.BaseType;
                }

                foreach (object m in memebers)
                {
                    if (m is XmlNode node)
                    {
                        var name = node.Attributes["name"].Value;

                        var summary = node.InnerText.Trim();

                        var matchKey = "P:" + t.ToString() + ".";

                        if (name.StartsWith(matchKey))
                        {
                            name = name.Replace(matchKey, "");
                            fieldList.Add(name, summary);
                        }

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

                return fieldList.FirstOrDefault(t => t.Key.ToLower() == fieldName.ToLower()).Value ?? fieldName;
            }

        }


    }
}
