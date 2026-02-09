using Application.Model.Basic.Log;
using Application.Model.LLM.LlmApp;
using Application.Model.Shared;
using Application.Model.TaskCenter;
using Application.Service.Basic;
using Application.Service.LLM;
using Application.Service.TaskCenter;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebAPI.Core.Filters;

namespace Admin.WebAPI.Controllers;

[SignVerifyFilter]
[Route("[controller]/[action]")]
[Authorize]
[ApiController]
public class OperationsController(LogManageService logManageService, TaskSettingService taskSettingService, QueueTaskManageService queueTaskManageService, LlmAppService llmAppService) : ControllerBase
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
    public Task<PageListDto<TaskSettingDto>> GetTaskSettingList([FromQuery] PageRequestDto request, [FromQuery] string? category) => taskSettingService.GetTaskSettingListAsync(request, category);


    /// <summary>
    /// 更新任务配置信息
    /// </summary>
    [HttpPost]
    public Task<bool> UpdateTaskSetting(long taskSettingId, EditTaskSettingDto updateTaskSetting) => taskSettingService.UpdateTaskSettingAsync(taskSettingId, updateTaskSetting);


    /// <summary>
    /// 获取定时任务名称列表（去重）
    /// </summary>
    [HttpGet]
    public Task<List<string>> GetArgsScheduleTaskNameList() => taskSettingService.GetArgsScheduleTaskNameListAsync();


    /// <summary>
    /// 新增带参定时任务（动态添加支持参数的 ScheduleTask）
    /// </summary>
    [HttpPost]
    public Task<long> CreateScheduleTask(CreateScheduleTaskDto createTaskSetting) => taskSettingService.CreateScheduleTaskAsync(createTaskSetting);


    /// <summary>
    /// 获取队列任务列表
    /// </summary>
    [HttpGet]
    public Task<PageListDto<QueueTaskDto>> GetQueueTaskList([FromQuery] QueueTaskPageRequestDto request) => queueTaskManageService.GetQueueTaskListAsync(request);


    /// <summary>
    /// 重试队列任务（仅限未成功的任务）
    /// </summary>
    [HttpPost]
    public Task<bool> RetryQueueTask(long id) => queueTaskManageService.RetryQueueTaskAsync(id);


    /// <summary>
    /// 获取 LLM 应用配置列表
    /// </summary>
    [HttpGet]
    public Task<PageListDto<LlmAppDto>> GetLlmAppList([FromQuery] LlmAppPageRequestDto request) => llmAppService.GetLlmAppListAsync(request);


    /// <summary>
    /// 创建 LLM 应用配置
    /// </summary>
    [HttpPost]
    public Task<long> CreateLlmApp(EditLlmAppDto createLlmApp) => llmAppService.CreateLlmAppAsync(createLlmApp);


    /// <summary>
    /// 更新 LLM 应用配置
    /// </summary>
    [HttpPost]
    public Task<bool> UpdateLlmApp(long id, EditLlmAppDto updateLlmApp) => llmAppService.UpdateLlmAppAsync(id, updateLlmApp);


    /// <summary>
    /// 删除 LLM 应用配置
    /// </summary>
    [HttpDelete]
    public Task<bool> DeleteLlmApp(long id) => llmAppService.DeleteLlmAppAsync(id);


    /// <summary>
    /// LLM 调用测试
    /// </summary>
    [HttpPost]
    public Task<TestLlmAppResultDto> TestLlmApp(TestLlmAppRequestDto request) => llmAppService.TestLlmAppAsync(request);

}
