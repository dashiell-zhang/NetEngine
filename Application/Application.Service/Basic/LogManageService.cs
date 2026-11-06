using Application.Model.Basic.Log;
using Application.Model.Shared;
using Logger.Database.Query;

namespace Application.Service.Basic;

/// <summary>
/// 将管理端日志请求转换为组件查询并保持应用契约
/// </summary>
public class LogManageService(LogQueryService logQuery)
{

    /// <summary>
    /// 获取日志列表
    /// </summary>
    public async Task<PageListDto<LogDto>> GetLogListAsync(LogPageRequestDto request)
    {

        var result = await logQuery.QueryAsync(new LogQueryRequest
        {
            Project = request.Project,
            MachineName = request.MachineName,
            Content = request.Content,
            Level = request.Level,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            Skip = request.Skip(),
            Take = request.PageSize
        });
        return new PageListDto<LogDto>
        {
            Total = result.Total,
            List = result.List.Select(item => new LogDto
            {
                Id = item.Id,
                Project = item.Project,
                MachineName = item.MachineName,
                Level = item.Level,
                Category = item.Category,
                Content = item.Content,
                CreateTime = item.CreateTime
            }).ToList()
        };

    }

}
