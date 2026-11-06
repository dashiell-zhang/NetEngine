using Logger.Database.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Logger.Database.Query;

/// <summary>
/// 提供日志查询管理能力
/// </summary>
public class LogQueryService(IDbContextFactory<ReadLogDatabaseContext> contextFactory)
{

    /// <summary>
    /// 获取日志列表
    /// </summary>
    public async Task<LogQueryResult> QueryAsync(LogQueryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(request.Skip);
        if (request.Take is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(request.Take));
        }

        await using var readDb = await contextFactory.CreateDbContextAsync(cancellationToken);
        LogQueryResult result = new();

        var query = readDb.Log.AsQueryable();

        // 添加检索条件
        if (!string.IsNullOrWhiteSpace(request.Project))
        {
            query = query.Where(t => t.Project.Contains(request.Project));
        }

        if (!string.IsNullOrWhiteSpace(request.MachineName))
        {
            query = query.Where(t => t.MachineName.Contains(request.MachineName));
        }

        if (!string.IsNullOrWhiteSpace(request.Content))
        {
            var keyword = request.Content.Trim();
            query = query.Where(t => t.Content != null && t.Content.Contains(keyword));
        }

        if (!string.IsNullOrWhiteSpace(request.Level))
        {
            query = query.Where(t => t.Level == request.Level);
        }

        if (request.StartTime.HasValue)
        {
            query = query.Where(t => t.CreateTime >= request.StartTime.Value);
        }

        if (request.EndTime.HasValue)
        {
            query = query.Where(t => t.CreateTime <= request.EndTime.Value);
        }

        result.Total = await query.CountAsync(cancellationToken);

        if (result.Total != 0)
        {
            result.List = await query.OrderByDescending(t => t.Id).Select(t => new LogQueryItem
            {
                Id = t.Id,
                Project = t.Project,
                MachineName = t.MachineName,
                Level = t.Level,
                Category = t.Category,
                Content = t.Content,
                CreateTime = t.CreateTime
            }).Skip(request.Skip).Take(request.Take).ToListAsync(cancellationToken);
        }

        return result;
    }
}
