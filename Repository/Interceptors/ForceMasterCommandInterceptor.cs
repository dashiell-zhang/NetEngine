using Microsoft.EntityFrameworkCore.Diagnostics;
using Repository.Extensions;
using System.Data.Common;

namespace Repository.Interceptors
{
    public class ForceMasterCommandInterceptor : DbCommandInterceptor
    {

        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            command.CommandText = GetNewCommandText(command.CommandText);

            return base.ReaderExecuting(command, eventData, result);
        }


        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            command.CommandText = GetNewCommandText(command.CommandText);

            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }


        private static string GetNewCommandText(string commandText)
        {
            if (DatabaseFacadeExtension.IsUseForceMaster())
            {
                commandText = "/*FORCE_MASTER*/ " + commandText;
            }

            return commandText;
        }
    }
}
