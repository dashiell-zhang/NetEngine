using Application.Model.Shared;
using Application.Model.TaskCenter;

namespace Application.Interface.TaskCenter;

/// <summary>
/// 本业务任务配置管理
/// </summary>
public interface ITaskSettingService
{

    /// <summary>
    /// 查询本业务任务配置并转换分页结果
    /// </summary>
    Task<PageListDto<TaskSettingDto>> GetTaskSettingListAsync(PageRequestDto request, string? category);


    /// <summary>
    /// 更新本业务任务配置并记录明确传入的操作人
    /// </summary>
    Task<bool> UpdateTaskSettingAsync(long actorUserId, long taskSettingId, EditTaskSettingDto updateTaskSetting);


    /// <summary>
    /// 获取本业务已声明的带参定时任务名称
    /// </summary>
    Task<List<string>> GetArgsScheduleTaskNameListAsync();


    /// <summary>
    /// 为本业务已声明模板创建带参运行实例
    /// </summary>
    Task<long> CreateScheduleTaskAsync(long actorUserId, CreateScheduleTaskDto createTaskSetting);

}
