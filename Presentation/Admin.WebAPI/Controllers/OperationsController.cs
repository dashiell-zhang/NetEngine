using Application.Model.Basic.Log;
using Application.Model.Shared;
using Application.Model.TaskCenter;
using Application.Service.Basic;
using Application.Service.TaskCenter;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebAPI.Core.Filters;

namespace Admin.WebAPI.Controllers;

[SignVerifyFilter]
[Route("[controller]/[action]")]
[Authorize]
[ApiController]
public class OperationsController(LogManageService logManageService, TaskSettingService taskSettingService, QueueTaskManageService queueTaskManageService) : ControllerBase
{

    /// <summary>
    /// 获取日志列表
    /// </summary>
    [HttpGet]
    public Task<PageListDto<LogDto>> GetLogList([FromQuery] LogPageRequestDto request) => logManageService.GetLogListAsync(request);


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


    /// <summary>
    /// 获取队列任务列表
    /// </summary>
    [HttpGet]
    public Task<PageListDto<QueueTaskDto>> GetQueueTaskList([FromQuery] PageRequestDto request) => queueTaskManageService.GetQueueTaskListAsync(request);


    /// <summary>
    /// 重试队列任务（仅限未成功的任务）
    /// </summary>
    [HttpPost]
    public Task<bool> RetryQueueTask(long id) => queueTaskManageService.RetryQueueTaskAsync(id);

}
