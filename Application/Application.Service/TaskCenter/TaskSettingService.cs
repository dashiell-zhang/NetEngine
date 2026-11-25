using Application.Interface.TaskCenter;
using Application.Model.Shared;
using Application.Model.TaskCenter;
using EventBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using global::QueueTask.Database;
using Repository;
using SourceGenerator.Runtime.Attributes;

namespace Application.Service.TaskCenter;

/// <summary>
/// 适配本业务任务配置与管理端契约，操作人由接口层传入
/// </summary>
[RegisterService(typeof(ITaskSettingService), Lifetime = ServiceLifetime.Scoped)]
public class TaskSettingService(QueueTaskManagement<DatabaseContext> management, IEventPublisher eventPublisher, ILogger<TaskSettingService> logger) : ITaskSettingService
{

    /// <summary>
    /// 查询本业务任务配置并转换分页结果
    /// </summary>
    public async Task<PageListDto<TaskSettingDto>> GetTaskSettingListAsync(PageRequestDto request, string? category)
    {

        var result = await management.QuerySettingsAsync(request.Skip(), request.PageSize, category);
        return new PageListDto<TaskSettingDto>
        {
            Total = result.Total,
            List = result.Items.Select(t => new TaskSettingDto
            {
                Id = t.Id,
                Name = t.Name,
                Category = t.Category,
                Parameter = t.Parameter,
                Semaphore = t.Semaphore,
                Cron = t.Cron,
                IsEnable = t.IsEnable,
                Remark = t.Remark,
                CreateTime = t.CreateTime,
                UpdateTime = t.UpdateTime
            }).ToList()
        };

    }


    /// <summary>
    /// 更新本业务任务配置并记录明确传入的操作人
    /// </summary>
    public async Task<bool> UpdateTaskSettingAsync(long actorUserId, long taskSettingId, EditTaskSettingDto updateTaskSetting)
    {

        await management.UpdateSettingAsync(taskSettingId, actorUserId, updateTaskSetting.Parameter, updateTaskSetting.Semaphore, updateTaskSetting.Cron, updateTaskSetting.IsEnable, updateTaskSetting.Remark);
        await NotifySettingChangedAsync(taskSettingId);
        return true;

    }


    /// <summary>
    /// 获取本业务已声明的带参定时任务名称
    /// </summary>
    public Task<List<string>> GetArgsScheduleTaskNameListAsync()
    {

        return management.GetArgsScheduleNamesAsync();

    }


    /// <summary>
    /// 为本业务已声明模板创建带参运行实例
    /// </summary>
    public async Task<long> CreateScheduleTaskAsync(long actorUserId, CreateScheduleTaskDto createTaskSetting)
    {

        var taskSettingId = await management.CreateScheduleAsync(actorUserId, createTaskSetting.Name, createTaskSetting.Parameter, createTaskSetting.Cron, createTaskSetting.IsEnable, createTaskSetting.Remark);
        await NotifySettingChangedAsync(taskSettingId);
        return taskSettingId;

    }


    /// <summary>
    /// 保存后通知各任务宿主刷新，发布失败时由周期同步补收配置
    /// </summary>
    private async Task NotifySettingChangedAsync(long taskSettingId)
    {

        try
        {
            await eventPublisher.PublishAsync("TaskSetting.Changed");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "任务配置 {TaskSettingId} 已保存，变更通知发布失败，将由周期同步刷新", taskSettingId);
        }

    }

}
