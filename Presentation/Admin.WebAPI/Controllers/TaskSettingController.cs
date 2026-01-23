using Application.Model.Shared;
using Application.Model.TaskCenter;
using Application.Service.TaskCenter;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebAPI.Core.Filters;

namespace Admin.WebAPI.Controllers;

[SignVerifyFilter]
[Route("[controller]/[action]")]
[Authorize]
[ApiController]
public class TaskSettingController(TaskSettingService taskSettingService) : ControllerBase
{

    /// <summary>
    /// 获取任务配置列表
    /// </summary>
    [HttpGet]
    public Task<PageListDto<TaskSettingDto>> GetTaskSettingList([FromQuery] PageRequestDto request) => taskSettingService.GetTaskSettingListAsync(request);


    /// <summary>
    /// 更新任务配置信息
    /// </summary>
    [HttpPost]
    public Task<bool> UpdateTaskSetting(long taskSettingId, EditTaskSettingDto updateTaskSetting) => taskSettingService.UpdateTaskSettingAsync(taskSettingId, updateTaskSetting);
}

