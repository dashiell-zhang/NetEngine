using Application.Interface;
using Application.Model.Shared;
using Application.Model.TaskCenter;
using Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Repository;
using SourceGenerator.Runtime.Attributes;

namespace Application.Service.TaskCenter;

[RegisterService(Lifetime = ServiceLifetime.Scoped)]
public class TaskSettingService(DatabaseContext db, IUserContext userContext)
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
}
