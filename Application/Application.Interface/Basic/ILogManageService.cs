using Application.Model.Basic.Log;
using Application.Model.Shared;

namespace Application.Interface.Basic;

/// <summary>
/// 管理端日志查询
/// </summary>
public interface ILogManageService
{

    /// <summary>
    /// 获取日志列表
    /// </summary>
    Task<PageListDto<LogDto>> GetLogListAsync(LogPageRequestDto request);

}
