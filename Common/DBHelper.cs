using Microsoft.EntityFrameworkCore;
using Models.Dtos;
using Repository.WebCore;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;

namespace Common
{
    public static class DBHelper
    {

        /// <summary>
        /// 针对数据库执行自定义的sql查询，返回泛型List
        /// </summary>
        /// <typeparam name="T">返回类型</typeparam>
        /// <param name="connection">数据库连接</param>
        /// <param name="sql">自定义查询Sql</param>
        /// <returns></returns>
        public static IList<T> SelectFromSql<T>(DbConnection connection, string sql) where T : class
        {
            connection.Open();

            var command = connection.CreateCommand();

            command.CommandText = sql;

            var reader = command.ExecuteReader();

            var dataTable = new DataTable();

            dataTable.Load(reader);

            reader.Close();

            connection.Close();

            var list = DataHelper.DataTableToList<T>(dataTable);

            return list;
        }



        /// <summary>
        /// 针对数据库执行自定义的sql查询，返回泛型List
        /// </summary>
        /// <typeparam name="T">返回类型</typeparam>
        /// <param name="connection">数据库连接</param>
        /// <param name="sql">自定义查询Sql</param>
        /// <remarks>connection = db.Database.GetDbConnection()</remarks>
        /// <returns></returns>
        public static IList<T> SelectFromSql<T>(string sql) where T : class
        {
            using (var db = new webcoreContext())
            {

                var connection = db.Database.GetDbConnection();

                connection.Open();

                var command = connection.CreateCommand();

                command.CommandText = sql;

                var reader = command.ExecuteReader();

                var dataTable = new DataTable();

                dataTable.Load(reader);

                reader.Close();

                connection.Close();

                var list = DataHelper.DataTableToList<T>(dataTable);

                return list;
            }
        }



        /// <summary>
        /// 根据Guid获取Int
        /// </summary>
        /// <param name="guid"></param>
        /// <returns></returns>
        public static int GuidToInt(string guid)
        {
            using (var db = new webcoreContext())
            {
                var info = db.TGuidToInt.Where(t => t.Guid == guid.ToLower()).FirstOrDefault() ?? new TGuidToInt();

                if (info.Id == 0)
                {
                    info.Guid = guid.ToLower();

                    var dbinfo = db.TGuidToInt.Add(info);

                    db.SaveChanges();

                    return dbinfo.Entity.Id;
                }
                else
                {
                    return info.Id;
                }
            }
        }




        /// <summary>
        /// 根据Int获取Guid
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public static string IntToGuid(int id)
        {
            using (var db = new webcoreContext())
            {
                return db.TGuidToInt.Where(t => t.Id == id).Select(t => t.Guid).FirstOrDefault();
            }
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
                using (var db = new webcoreContext())
                {
                    var log = new TLog();

                    log.Id = Guid.NewGuid().ToString();
                    log.Sign = Sign;
                    log.Type = Type;
                    log.Content = Content;
                    log.CreateTime = DateTime.Now;

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
        /// 账户合并方法
        /// </summary>
        /// <param name="oldUserId">原始账户ID</param>
        /// <param name="newUserId">新账户ID</param>
        /// <returns></returns>
        public static bool MergeUser(string oldUserId, string newUserId)
        {
            try
            {
                using (var db = new webcoreContext())
                {

                    var connection = db.Database.GetDbConnection();

                    string sql = "SELECT t.name AS [Key],c.name AS Value FROM sys.tables AS t INNER JOIN sys.columns c ON t.OBJECT_ID = c.OBJECT_ID WHERE c.system_type_id = 231 and c.name LIKE '%userid%'";

                    var list = SelectFromSql<dtoKeyValue>(connection, sql);


                    foreach (var item in list)
                    {
                        string table_name = item.Key.ToString();
                        string column_name = item.Value.ToString();

                        string upSql = "UPDATE [dbo].[" + table_name + "] SET [" + column_name + "] = N'" + newUserId + "' WHERE [" + column_name + "] = N'" + oldUserId + "'";

                        db.Database.ExecuteSqlRaw(upSql);
                    }

                    string delSql = "DELETE FROM [dbo].[t_user] WHERE [id] = N'" + oldUserId + "'";

                    db.Database.ExecuteSqlRaw(delSql);

                    return true;
                }

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
            using (var db = new webcoreContext())
            {

                var info = db.TCount.Where(t => t.Tag == tag).FirstOrDefault() ?? new TCount();

                if (!string.IsNullOrEmpty(info.Id))
                {
                    info.Count = info.Count + 1;
                    info.UpdateTime = DateTime.Now;

                    db.SaveChanges();

                    return info.Count;
                }
                else
                {
                    info.Id = Guid.NewGuid().ToString();
                    info.Tag = tag;
                    info.Count = 1;
                    info.CreateTime = DateTime.Now;

                    db.TCount.Add(info);

                    db.SaveChanges();

                    return info.Count;

                }

            }
        }



        /// <summary>
        /// 通过类型获取计数值
        /// </summary>
        /// <param name="tag">标记</param>
        /// <param name="starttime">开始时间</param>
        /// <param name="endtime">结束时间</param>
        /// <returns></returns>
        public static int RunCountGet(string tag, DateTime starttime = default(DateTime), DateTime endtime = default(DateTime))
        {

            using (var db = new webcoreContext())
            {
                var query = db.TCount.Where(t => t.Tag.Contains(tag));

                if (starttime != default(DateTime) & endtime != default(DateTime))
                {
                    query = query.Where(t => t.CreateTime >= starttime & t.CreateTime <= endtime);
                }

                return query.ToList().Sum(t => t.Count);
            }

        }
    }
}
