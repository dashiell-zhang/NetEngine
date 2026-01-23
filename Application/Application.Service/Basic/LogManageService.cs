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
    public async Task<PageListDto<LogDto>> GetLogListAsync(PageRequestDto request)
    {
        PageListDto<LogDto> result = new();

        var query = db.Log.AsNoTracking().AsQueryable();

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

