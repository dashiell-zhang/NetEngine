using Application.Model.Basic.Log;
using Application.Model.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Repository;
using SourceGenerator.Runtime.Attributes;

namespace Application.Service.Basic;

[RegisterService(Lifetime = ServiceLifetime.Scoped)]
public class LogManageService(DatabaseContext db)
{

    /// <summary>
    /// 获取日志列表
    /// </summary>
    public async Task<PageListDto<LogDto>> GetLogListAsync(LogPageRequestDto request)
    {
        PageListDto<LogDto> result = new();

        var query = db.Log.AsNoTracking().AsQueryable();

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

        result.Total = await query.CountAsync();

        if (result.Total != 0)
        {
            result.List = await query.OrderByDescending(t => t.Id).Select(t => new LogDto
            {
                Id = t.Id,
                Project = t.Project,
                MachineName = t.MachineName,
                Level = t.Level,
                Category = t.Category,
                Content = t.Content,
                CreateTime = t.CreateTime
            }).Skip(request.Skip()).Take(request.PageSize).ToListAsync();
        }

        return result;
    }
}
