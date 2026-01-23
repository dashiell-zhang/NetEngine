using Application.Model.Basic.Log;
using Application.Model.Shared;
using Application.Service.Basic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebAPI.Core.Filters;

namespace Admin.WebAPI.Controllers;

[SignVerifyFilter]
[Route("[controller]/[action]")]
[Authorize]
[ApiController]
public class LogController(LogManageService logManageService) : ControllerBase
{

    /// <summary>
    /// 获取日志列表
    /// </summary>
    [HttpGet]
    public Task<PageListDto<LogDto>> GetLogList([FromQuery] LogPageRequestDto request) => logManageService.GetLogListAsync(request);
}

