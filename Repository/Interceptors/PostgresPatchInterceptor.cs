using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;


namespace Repository.Interceptors
{
    public class PostgresPatchInterceptor : DbCommandInterceptor
    {

        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {

            string sql = command.CommandText;

            //分区表插入数据补丁（分区表在插入数据时，如果直接执行 RETURNING xmin;会返回 cannot retrieve a system column in this context，所以采用 oid(txid_current()) 修复
            if (sql.Contains("INSERT INTO"))
            {
                command.CommandText = sql.Replace("RETURNING xmin;", "RETURNING oid(txid_current()) as xmin;");
            }

            return result;
        }



        public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
        {

            //var runtime = eventData.Duration.TotalSeconds;

            ////如果执行时间超过 5秒 则记录日志
            //if (runtime > 5)
            //{

            //}

            return result;
        }

    }
}
