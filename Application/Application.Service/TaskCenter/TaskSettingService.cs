using Application.Model.Shared;
using Application.Model.TaskCenter;
using Common;
using IdentifierGenerator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Repository;
using Repository.Database;
using SourceGenerator.Runtime.Attributes;
using System.Text.Json;

namespace Application.Service.TaskCenter;

/// <summary>
/// 提供任务配置查询和维护能力
/// </summary>
[RegisterService(Lifetime = ServiceLifetime.Scoped)]
public class TaskSettingService(DatabaseContext db, IdService idService)
{
    private const string ArgsDefaultParameter = "__args_default__";


    /// <summary>
    /// 获取任务配置列表
    /// </summary>
    public async Task<PageListDto<TaskSettingDto>> GetTaskSettingListAsync(PageRequestDto request, string? category)
    {
        PageListDto<TaskSettingDto> result = new();

        var query = db.TaskSetting.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(t => t.Category == category);
        }

        result.Total = await query.CountAsync();

        if (result.Total != 0)
        {
            result.List = await query.OrderByDescending(t => t.Id).Select(t => new TaskSettingDto
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
            }).Skip(request.Skip()).Take(request.PageSize).ToListAsync();
        }

        return result;
    }


    /// <summary>
    /// 更新任务配置信息
    /// </summary>
    /// <param name="actorUserId">行为发起人用户ID</param>
    /// <param name="taskSettingId">任务配置ID</param>
    /// <param name="updateTaskSetting">任务配置修改内容</param>
    /// <returns>是否更新成功</returns>
    public async Task<bool> UpdateTaskSettingAsync(long actorUserId, long taskSettingId, EditTaskSettingDto updateTaskSetting)
    {
        var taskSetting = await db.TaskSetting.Where(t => t.Id == taskSettingId).FirstOrDefaultAsync();

        if (taskSetting == null)
        {
            throw new CustomException("无效的 taskSettingId");
        }

        taskSetting.Parameter = updateTaskSetting.Parameter;
        taskSetting.Semaphore = updateTaskSetting.Semaphore;
        taskSetting.Cron = updateTaskSetting.Cron;
        taskSetting.IsEnable = updateTaskSetting.IsEnable;
        taskSetting.Remark = updateTaskSetting.Remark;
        taskSetting.UpdateUserId = actorUserId;

        await db.SaveChangesAsync();

        return true;
    }


    /// <summary>
    /// 获取支持参数的定时任务名称列表（去重）
    /// </summary>
    public Task<List<string>> GetArgsScheduleTaskNameListAsync()
    {
        return db.TaskSetting.AsNoTracking()
            .Where(t => t.Category == "ScheduleTask" && t.Parameter == ArgsDefaultParameter)
            .Select(t => t.Name)
            .Distinct()
            .OrderBy(t => t)
            .ToListAsync();
    }


    /// <summary>
    /// 新增带参定时任务（用于动态添加可带参的 ScheduleTask 实例）
    /// </summary>
    /// <param name="actorUserId">行为发起人用户ID</param>
    /// <param name="createTaskSetting">定时任务创建内容</param>
    /// <returns>任务配置ID</returns>
    public async Task<long> CreateScheduleTaskAsync(long actorUserId, CreateScheduleTaskDto createTaskSetting)
    {
        try
        {
            JsonDocument.Parse(createTaskSetting.Parameter);
        }
        catch
        {
            throw new CustomException("任务参数必须是合法 JSON");
        }

        var isHave = await db.TaskSetting.AsNoTracking()
            .Where(t => t.Category == "ScheduleTask" && t.Name == createTaskSetting.Name)
            .AnyAsync();

        if (!isHave)
        {
            throw new CustomException("任务名称不存在，请先确认 TaskService 已同步任务配置");
        }

        TaskSetting taskSetting = new()
        {
            Id = idService.GetId(),
            Category = "ScheduleTask",
            Name = createTaskSetting.Name,
            Parameter = createTaskSetting.Parameter,
            Cron = createTaskSetting.Cron,
            IsEnable = createTaskSetting.IsEnable,
            Remark = createTaskSetting.Remark,
            CreateUserId = actorUserId
        };

        db.TaskSetting.Add(taskSetting);

        await db.SaveChangesAsync();

        return taskSetting.Id;
    }
}
