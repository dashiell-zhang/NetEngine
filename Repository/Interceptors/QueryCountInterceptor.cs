using Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Data.Common;

namespace Repository.Interceptors
{
    public class QueryCountInterceptor(IDistributedCache _distributedCache, ILogger<QueryCountInterceptor> _logger) : DbCommandInterceptor
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
                var cachedCount = _distributedCache.GetString(countCacheKey);

                if (cachedCount != null)
                {
                    var reader = CreateDataReaderFromCount(cachedCount);
                    Console.WriteLine($"使用缓存的查询计数结果: {cachedCount}, 缓存键: {countCacheKey}");
                    return InterceptionResult<DbDataReader>.SuppressWithResult(reader);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "读取缓存失败，将执行原始查询。缓存键: {CacheKey}", countCacheKey);
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
                var cachedCount = await _distributedCache.GetStringAsync(countCacheKey, cancellationToken);

                if (cachedCount != null)
                {
                    var reader = CreateDataReaderFromCount(cachedCount);
                    Console.WriteLine($"使用缓存的查询计数结果: {cachedCount}, 缓存键: {countCacheKey}");
                    return InterceptionResult<DbDataReader>.SuppressWithResult(reader);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "读取缓存失败，将执行原始查询。缓存键: {CacheKey}", countCacheKey);
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

                    var countCache = _distributedCache.GetString(countCacheKey);

                    if (countCache == null)
                    {
                        var pendingCacheKey = $"pending:{countCacheKey}";
                        _ = _distributedCache.SetAsync(pendingCacheKey, count, TimeSpan.FromSeconds(PendingCacheExpiration));
                        Console.WriteLine($"创建待激活缓存埋点: Count={count}, 缓存键={countCacheKey}");
                    }

                    return CreateDataReaderFromCount(count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"处理查询结果缓存时出错。缓存键: {countCacheKey}");
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

                    var countCache = await _distributedCache.GetStringAsync(countCacheKey, cancellationToken);

                    if (countCache == null)
                    {
                        var pendingCacheKey = $"pending:{countCacheKey}";
                        _ = _distributedCache.SetAsync(pendingCacheKey, count, TimeSpan.FromSeconds(PendingCacheExpiration));

                        Console.WriteLine($"创建待激活缓存埋点: Count={count}, 缓存键={countCacheKey}");
                    }

                    return CreateDataReaderFromCount(count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"处理查询结果缓存时出错。缓存键: {countCacheKey}");
            }

            return result;
        }



        private void TryActivatePendingCache(DbCommand command)
        {
            // 检查是否为分页查询，如果是则尝试激活待处理缓存
            var sql = command.CommandText;
            var lastLine = sql.Split("\r\n").Last();

            if (lastLine.StartsWith("LIMIT") && lastLine.Contains("OFFSET"))
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

                var pendingCache = await _distributedCache.GetStringAsync(pendingCacheKey);

                if (pendingCache != null)
                {
                    await _distributedCache.SetAsync(countCacheKey, pendingCache, TimeSpan.FromSeconds(CacheExpiration));

                    _distributedCache.Remove(pendingCacheKey);

                    Console.WriteLine($"通过分页查询激活缓存: Count={pendingCache}, 缓存键={countCacheKey}");
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
                //提取 FROM 语句
                var fromIndex = sql.IndexOf("FROM ");
                if (fromIndex == -1) return null;
                var countSql = string.Concat("SELECT count(*)::int\r\n", sql.AsSpan(fromIndex));


                //找到 ORDER BY 开始移除后面的内容
                var orderByIndex = countSql.LastIndexOf("ORDER BY ");
                if (orderByIndex > 0)
                {
                    countSql = countSql[..orderByIndex];
                }

                countSql = countSql.Trim();

                // 构建参数字典
                Dictionary<string, string?> paramDict = [];
                foreach (DbParameter param in command.Parameters)
                {
                    if (countSql.Contains(param.ParameterName))
                    {
                        paramDict[param.ParameterName] = param.Value?.ToString();
                    }
                }

                return GetCountKey(countSql, paramDict);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "从分页查询推断count缓存键失败");
                return null;
            }
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
            string key = "queryCount:" + commandText;

            if (parameters.Count > 0)
            {
                foreach (var param in parameters)
                {
                    var paramValue = param.Key + param.Value?.ToString() + "|-|";
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
