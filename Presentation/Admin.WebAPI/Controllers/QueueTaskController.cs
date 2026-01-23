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
public class QueueTaskController(QueueTaskManageService queueTaskManageService) : ControllerBase
{

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
