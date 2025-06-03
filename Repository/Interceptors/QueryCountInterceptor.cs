using Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Data.Common;

namespace Repository.Interceptors
{
    public class QueryCountInterceptor(IDistributedCache distributedCache, ILogger<QueryCountInterceptor> logger) : DbCommandInterceptor
    {

        private const int CacheExpiration = 60;
        private const int PendingCacheExpiration = 5;


        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            var countCacheKey = GetCountKeyFromCommand(command);
            if (countCacheKey == null)
            {
                TryActivatePendingCache(command);

                return result;
            }

            try
            {
                var cachedCount = distributedCache.GetString(countCacheKey);

                if (cachedCount != null)
                {
                    var reader = CreateDataReaderFromCount(cachedCount);
                    return InterceptionResult<DbDataReader>.SuppressWithResult(reader);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"读取缓存失败，将执行原始查询。{command.CommandText}");
            }

            return result;
        }


        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {

            var countCacheKey = GetCountKeyFromCommand(command);
            if (countCacheKey == null)
            {
                TryActivatePendingCache(command);

                return result;
            }

            try
            {
                var cachedCount = await distributedCache.GetStringAsync(countCacheKey, cancellationToken);

                if (cachedCount != null)
                {
                    var reader = CreateDataReaderFromCount(cachedCount);
                    return InterceptionResult<DbDataReader>.SuppressWithResult(reader);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"读取缓存失败，将执行原始查询。{command.CommandText}");
            }

            return result;
        }


        public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
        {
            var countCacheKey = GetCountKeyFromCommand(command);

            if (countCacheKey == null)
            {
                return result;
            }

            try
            {
                if (result.HasRows && result.Read())
                {
                    var count = result.GetInt32(0).ToString();

                    var countCache = distributedCache.GetString(countCacheKey);

                    if (countCache == null)
                    {
                        var pendingCacheKey = $"pending:{countCacheKey}";
                        _ = distributedCache.SetAsync(pendingCacheKey, count, TimeSpan.FromSeconds(PendingCacheExpiration));
                    }

                    return CreateDataReaderFromCount(count);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"处理查询结果缓存时出错。{command.CommandText}");
            }

            return result;
        }


        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        {
            var countCacheKey = GetCountKeyFromCommand(command);

            if (countCacheKey == null)
            {
                return result;
            }

            try
            {
                if (result.HasRows && await result.ReadAsync(cancellationToken))
                {
                    var count = result.GetInt32(0).ToString();

                    var countCache = await distributedCache.GetStringAsync(countCacheKey, cancellationToken);

                    if (countCache == null)
                    {
                        var pendingCacheKey = $"pending:{countCacheKey}";
                        _ = distributedCache.SetAsync(pendingCacheKey, count, TimeSpan.FromSeconds(PendingCacheExpiration));

                    }

                    return CreateDataReaderFromCount(count);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"处理查询结果缓存时出错。{command.CommandText}");
            }

            return result;
        }


        private void TryActivatePendingCache(DbCommand command)
        {
            // 检查是否为分页查询，如果是则尝试激活待处理缓存
            var sql = command.CommandText;

            if (sql.Contains("LIMIT") && sql.Contains("OFFSET"))
            {
                var listCountCacheKey = GetCountKeyByListCommand(command);
                if (listCountCacheKey != null)
                {
                    _ = ActivatePendingCache(listCountCacheKey);
                }
            }


            async Task ActivatePendingCache(string countCacheKey)
            {
                var pendingCacheKey = $"pending:{countCacheKey}";

                var pendingCache = await distributedCache.GetStringAsync(pendingCacheKey);

                if (pendingCache != null)
                {
                    await distributedCache.SetAsync(countCacheKey, pendingCache, TimeSpan.FromSeconds(CacheExpiration));

                    distributedCache.Remove(pendingCacheKey);
                }
            }
        }


        /// <summary>
        /// 从分页查询命令中获取对应的Count缓存键
        /// </summary>
        /// <param name="command">数据库命令对象</param>
        /// <returns>Count缓存键，如果无法解析则返回null</returns>
        private string? GetCountKeyByListCommand(DbCommand command)
        {
            var sql = command.CommandText;

            try
            {
                // 提取核心查询部分用于构建 count 查询
                var coreQuery = ExtractCoreQueryForCount(sql);
                if (string.IsNullOrEmpty(coreQuery))
                {
                    return null;
                }

                var countSql = $"SELECT count(*)::int{Environment.NewLine}{coreQuery}";

                // 构建参数字典
                Dictionary<string, string?> paramDict = [];
                foreach (DbParameter param in command.Parameters)
                {
                    // 只包含 count 查询中需要的参数
                    if (countSql.Contains(param.ParameterName))
                    {
                        paramDict[param.ParameterName] = param.Value?.ToString();
                    }
                }

                return GetCountKey(countSql, paramDict);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"从分页查询推断count缓存键失败。{sql}");
                return null;
            }
        }


        /// <summary>
        /// 从复杂的分页查询中提取用于 count 查询的核心部分
        /// </summary>
        /// <param name="sql">原始 SQL 查询</param>
        /// <returns>用于 count 查询的核心 SQL 部分</returns>
        private string? ExtractCoreQueryForCount(string sql)
        {
            try
            {
                // 查找最外层的 FROM 子句
                var lines = sql.Split(Environment.NewLine);

                // 寻找包含子查询的 FROM 语句
                var fromLineIndex = -1;
                var subQueryStartIndex = -1;

                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i].Trim();
                    if (line.StartsWith("FROM ("))
                    {
                        fromLineIndex = i;
                        subQueryStartIndex = i + 1;
                        break;
                    }
                }

                if (fromLineIndex == -1 || subQueryStartIndex == -1)
                {
                    // 如果没有找到子查询，使用原来的简单方法
                    return ExtractSimpleQuery(sql);
                }

                // 提取子查询中的核心部分
                List<string> subQueryLines = [];
                var parenthesesCount = 0;
                var foundCoreQuery = false;

                for (int i = subQueryStartIndex; i < lines.Length; i++)
                {
                    var line = lines[i];

                    // 计算括号层级
                    parenthesesCount += line.Count(c => c == '(') - line.Count(c => c == ')');

                    if (parenthesesCount < 0)
                    {
                        // 子查询结束
                        break;
                    }

                    var trimmedLine = line.Trim();

                    // 跳过 SELECT 字段列表，直接找 FROM 开始的部分
                    if (!foundCoreQuery && trimmedLine.StartsWith("FROM "))
                    {
                        foundCoreQuery = true;
                    }

                    if (foundCoreQuery)
                    {
                        // 遇到 ORDER BY 和 LIMIT/OFFSET 直接阶段跳出
                        if (trimmedLine.StartsWith("ORDER BY") || trimmedLine.StartsWith("LIMIT") || trimmedLine.StartsWith("OFFSET"))
                        {
                            break;
                        }

                        subQueryLines.Add(line);
                    }
                }

                return string.Join(Environment.NewLine, subQueryLines).Trim();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"解析复杂查询失败。{sql}");
            }

            return null;
        }


        /// <summary>
        /// 简单查询的提取方法
        /// </summary>
        /// <param name="sql">原始 SQL</param>
        /// <returns>提取的查询部分</returns>
        private static string? ExtractSimpleQuery(string sql)
        {
            var fromIndex = sql.IndexOf("FROM ");
            if (fromIndex == -1) return null;

            var queryPart = sql[fromIndex..];

            // 移除 ORDER BY 及之后的内容
            var orderByIndex = queryPart.LastIndexOf("ORDER BY ");
            if (orderByIndex > 0)
            {
                queryPart = queryPart[..orderByIndex];
            }

            return queryPart.Trim();
        }


        /// <summary>
        /// 从DbCommand获取Count缓存键的辅助方法
        /// </summary>
        /// <param name="command">数据库命令对象</param>
        /// <returns>缓存键，如果不是count查询则返回null</returns>
        private static string? GetCountKeyFromCommand(DbCommand command)
        {
            if (!command.CommandText.StartsWith("SELECT count(*)::int"))
            {
                return null;
            }

            Dictionary<string, string?> paramDict = [];

            foreach (DbParameter param in command.Parameters)
            {
                paramDict[param.ParameterName] = param.Value?.ToString();
            }

            return GetCountKey(command.CommandText, paramDict);
        }


        /// <summary>
        /// 根据命令文本和参数生成Count缓存键
        /// </summary>
        /// <param name="commandText">SQL命令文本</param>
        /// <param name="parameters">参数字典</param>
        /// <returns>缓存键，如果不是count查询则返回null</returns>
        private static string? GetCountKey(string commandText, Dictionary<string, string?> parameters)
        {
            commandText = string.Join("", [.. commandText.Split(Environment.NewLine).Select(t => t.Trim())]);

            string key = "queryCount:" + commandText;

            if (parameters.Count > 0)
            {
                foreach (var param in parameters)
                {
                    var paramValue = param.Key + param.Value?.ToString() + "*|r-r|*";
                    key += paramValue;
                }
            }

            return CryptoHelper.SHA256HashData(key);
        }


        /// <summary>
        /// 构建一个Count的查询返回
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>
        /// <remarks>因为 string 类型方便返回null 判断缓存是否存在，所以count不用int</remarks>
        private static DataTableReader CreateDataReaderFromCount(string count)
        {
            DataTable dataTable = new();
            dataTable.Columns.Add("count", typeof(int));
            var row = dataTable.NewRow();
            row["count"] = count;
            dataTable.Rows.Add(row);
            return dataTable.CreateDataReader();
        }
    }
}
