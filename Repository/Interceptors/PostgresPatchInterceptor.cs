using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;


namespace Repository.Interceptors
{
    public class PostgresPatchInterceptor : DbCommandInterceptor
    {

        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            command.CommandText = GetNewCommandText(command.CommandText);

            return result;
        }


        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            command.CommandText = GetNewCommandText(command.CommandText);

            return new ValueTask<InterceptionResult<DbDataReader>>(result);
        }


        private static string GetNewCommandText(string commandText)
        {
            //分区表插入数据补丁（分区表在插入数据时，如果直接执行 RETURNING xmin;会返回 cannot retrieve a system column in this context，所以采用 oid(txid_current()) 修复
            if (commandText.Contains("INSERT INTO"))
            {
                commandText = commandText.Replace("RETURNING xmin;", "RETURNING oid(txid_current()) as xmin;");
            }

            return commandText;
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
