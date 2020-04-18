using System.Collections.Generic;
using System.Data;
using System.Data.Common;

namespace Common.UseDB
{
    public static class EFCoreSql
    {


        /// <summary>
        /// 针对数据库执行自定义的sql查询，返回泛型List
        /// </summary>
        /// <typeparam name="T">返回类型</typeparam>
        /// <param name="connection">数据库连接</param>
        /// <param name="sql">自定义查询Sql</param>
        /// <remarks>connection = db.Database.GetDbConnection()</remarks>
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

            var list = Datas.DataTableHelper.DataTableToList<T>(dataTable);

            return list;
        }

    }
}
