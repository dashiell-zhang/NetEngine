using Common;
using Microsoft.EntityFrameworkCore;
using Models.Dtos;
using Repository.Database;

namespace WebApi.Actions
{
    public static class UserAction
    {


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
                using (var db = new dbContext())
                {

                    var connection = db.Database.GetDbConnection();

                    string sql = "SELECT t.name AS [Key],c.name AS Value FROM sys.tables AS t INNER JOIN sys.columns c ON t.OBJECT_ID = c.OBJECT_ID WHERE c.system_type_id = 231 and c.name LIKE '%userid%'";

                    var list = DBHelper.SelectFromSql<dtoKeyValue>(connection, sql);


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
    }
}
