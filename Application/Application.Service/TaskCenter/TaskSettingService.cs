using Application.Interface;
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

[RegisterService(Lifetime = ServiceLifetime.Scoped)]
public class TaskSettingService(DatabaseContext db, IUserContext userContext, IdService idService)
{


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
                Duration = t.Duration,
                Cron = t.Cron,
                IsEnable = t.IsEnable,
                Remarks = t.Remarks,
                CreateTime = t.CreateTime,
                UpdateTime = t.UpdateTime
            }).Skip(request.Skip()).Take(request.PageSize).ToListAsync();
        }

        return result;
    }


    /// <summary>
    /// 更新任务配置信息
    /// </summary>
    public async Task<bool> UpdateTaskSettingAsync(long taskSettingId, EditTaskSettingDto updateTaskSetting)
    {
        var taskSetting = await db.TaskSetting.Where(t => t.Id == taskSettingId).FirstOrDefaultAsync();

        if (taskSetting == null)
        {
            throw new CustomException("无效的 taskSettingId");
        }

        taskSetting.Parameter = updateTaskSetting.Parameter;
        taskSetting.Semaphore = updateTaskSetting.Semaphore;
        taskSetting.Duration = updateTaskSetting.Duration;
        taskSetting.Cron = updateTaskSetting.Cron;
        taskSetting.IsEnable = updateTaskSetting.IsEnable;
        taskSetting.Remarks = updateTaskSetting.Remarks;
        taskSetting.UpdateUserId = userContext.UserId;

        await db.SaveChangesAsync();

        return true;
    }


    /// <summary>
    /// 获取任务名称列表（去重）
    /// </summary>
    public Task<List<string>> GetTaskSettingNameListAsync(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            throw new CustomException("category 不可以空");
        }

        return db.TaskSetting.AsNoTracking()
            .Where(t => t.Category == category)
            .Select(t => t.Name)
            .Distinct()
            .OrderBy(t => t)
            .ToListAsync();
    }


    /// <summary>
    /// 新增带参定时任务（用于动态添加可带参的 ScheduleTask 实例）
    /// </summary>
    public async Task<long> CreateScheduleTaskAsync(CreateScheduleTaskDto createTaskSetting)
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
            Remarks = createTaskSetting.Remarks,
            CreateUserId = userContext.UserId
        };

        db.TaskSetting.Add(taskSetting);

        await db.SaveChangesAsync();

        return taskSetting.Id;
    }
}
