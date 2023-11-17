using Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Repository.Attributes;
using Repository.Database;
using Repository.Extensions;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;

namespace Repository.Tool.Tasks
{
    public class SyncJsonIndexTask(IServiceProvider serviceProvider) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(500);

            Console.WriteLine();


            var db = serviceProvider.GetRequiredService<DatabaseContext>();

            var dbIndexDT = db.SelectFromSql("SELECT tablename,indexname,indexdef FROM pg_indexes where schemaname = 'public' AND indexname like 'jsonbIX%'");

            var dbIndexList = DataHelper.DataTableToList<DBIndex>(dbIndexDT);

            List<DBIndex> codeIndexList = [];

            PropertyInfo[] dbSetList = db.GetType().GetProperties().Where(x => x.PropertyType.Name == "DbSet`1").ToArray();


            foreach (PropertyInfo dbSet in dbSetList)
            {

                Type tableType = dbSet.PropertyType.GetGenericArguments()[0];

                var tableName = tableType.GetCustomAttribute<TableAttribute>()?.Name ?? tableType.Name[1..];

                var jsonIndexAttributes = tableType.GetCustomAttributes<JsonIndexAttribute>();

                foreach (var jsonIndex in jsonIndexAttributes)
                {

                    List<string> indexColumnSQLList = [];

                    List<string> columnNameList = [];

                    foreach (var indexPropertyName in jsonIndex.PropertyNames)
                    {
                        string columnName = indexPropertyName.Split(".").FirstOrDefault()!;

                        string orderBy = "";

                        if (jsonIndex.AllDescending)
                        {
                            orderBy = " DESC";
                        }
                        else
                        {
                            if (jsonIndex.IsDescending != null)
                            {
                                var indexV = jsonIndex.PropertyNames.IndexOf(indexPropertyName);

                                if (indexV < jsonIndex.IsDescending.Length)
                                {
                                    bool isDescending = jsonIndex.IsDescending[indexV];

                                    if (isDescending)
                                    {
                                        orderBy = " DESC";
                                    }
                                }
                            }
                        }

                        if (indexPropertyName.Contains('.') == false)
                        {
                            columnNameList.Add(columnName);
                            indexColumnSQLList.Add($"\"{columnName}\"" + orderBy);
                        }
                        else
                        {
                            string childColumnName = indexPropertyName.Split(".").LastOrDefault()!;

                            var column = tableType.GetProperty(columnName) ?? throw new Exception(tableName + "表中不存在" + columnName);
                            var childColumn = column!.PropertyType!.GetProperty(childColumnName)! ?? throw new Exception(tableName + "表中不存在" + columnName + "." + childColumnName);
                            string columnSQL = $"(\"{columnName}\" ->> '{childColumnName}'::text)";

                            columnNameList.Add(columnName + "." + childColumnName);

                            string childColumnTypeName = childColumn.PropertyType.Name;

                            if (childColumnTypeName == "Nullable`1")
                            {
                                childColumnTypeName = childColumn.PropertyType.GenericTypeArguments[0].Name;
                            }

                            switch (childColumnTypeName)
                            {
                                case "String":
                                    {
                                        columnSQL = "(" + columnSQL + ")";
                                        break;
                                    }
                                case "Double":
                                    {
                                        columnSQL = "((" + columnSQL + ")::double precision" + ")";
                                        break;
                                    }
                                case "Single":
                                    {
                                        columnSQL = "((" + columnSQL + ")::real" + ")";
                                        break;
                                    }
                                case "Int16":
                                    {
                                        columnSQL = "((" + columnSQL + ")::smallint" + ")";
                                        break;
                                    }
                                case "UInt16":
                                    {
                                        columnSQL = "((" + columnSQL + ")::integer" + ")";
                                        break;
                                    }
                                case "Int64":
                                    {
                                        columnSQL = "((" + columnSQL + ")::bigint" + ")";
                                        break;
                                    }
                                case "UInt64":
                                    {
                                        columnSQL = "((" + columnSQL + ")::numeric(20,0)" + ")";
                                        break;
                                    }
                                case "Boolean":
                                    {
                                        columnSQL = "((" + columnSQL + ")::boolean" + ")";
                                        break;
                                    }
                                case "Decimal":
                                    {
                                        columnSQL = "((" + columnSQL + ")::numeric" + ")";
                                        break;
                                    }
                                case "UInt32":
                                    {
                                        columnSQL = "((" + columnSQL + ")::bigint" + ")";
                                        break;
                                    }
                                case "Int32":
                                    {
                                        columnSQL = "((" + columnSQL + ")::integer" + ")";
                                        break;
                                    }
                                case "Guid":
                                    {
                                        columnSQL = "((" + columnSQL + ")::uuid" + ")";
                                        break;
                                    }
                                default:
                                    throw new Exception($"JSONB 列中不支持 {childColumnTypeName} 类型建立索引");
                            }

                            indexColumnSQLList.Add(columnSQL + orderBy);
                        }
                    }


                    if (indexColumnSQLList.Count != 0)
                    {
                        string indexName = "jsonbIX_" + CryptoHelper.MD5HashData(tableName + "_" + string.Join("_", columnNameList));

                        string columnSQLStr = string.Join(", ", indexColumnSQLList);

                        string unique = "";
                        if (jsonIndex.IsUnique)
                        {
                            unique = " UNIQUE";
                        }

                        string sql = $"CREATE{unique} INDEX \"{indexName}\" ON public.\"{tableName}\" USING btree ({columnSQLStr})";

                        codeIndexList.Add(new()
                        {
                            tablename = tableName,
                            indexname = indexName,
                            indexdef = sql
                        });
                    }
                }
            }
            Console.ForegroundColor = ConsoleColor.Red;
            foreach (var item in dbIndexList)
            {
                var isHave = codeIndexList.Where(t => t.tablename == item.tablename && t.indexname == item.indexname && t.indexdef == item.indexdef).Any();

                if (isHave == false)
                {
                    string sql = $"DROP INDEX \"public\".\"{item.indexname}\";";
                    Console.WriteLine(sql);
                    Console.WriteLine();
                    //db.ExecuteSql(sql);
                }
            }

            Console.ForegroundColor = ConsoleColor.Green;

            foreach (var item in codeIndexList)
            {
                var isHave = dbIndexList.Where(t => t.tablename == item.tablename && t.indexname == item.indexname && t.indexdef == item.indexdef).Any();

                if (isHave == false)
                {
                    Console.WriteLine(item.indexdef + ";");
                    Console.WriteLine();
                    //db.ExecuteSql(item.indexdef);
                }
            }

            Console.ResetColor();
            Console.WriteLine("---------------------------------------Json索引同步任务执行完成--------------------------------------------------");
        }



        private class DBIndex
        {
#pragma warning disable IDE1006 // 命名样式
            public string tablename { get; set; }

            public string indexname { get; set; }

            public string indexdef { get; set; }
#pragma warning restore IDE1006 // 命名样式
        }
    }
}
