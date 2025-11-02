using Client.WebAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace Client.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]/[action]")]
public sealed class Demo2Controller(IDemo2Service _svc) : ControllerBase
{


    [HttpGet("echo")]
    public ActionResult<string> Echo([FromQuery] string name = "world")
        => _svc.Echo(name);

    [HttpGet("add")]
    public async Task<ActionResult<int>> Add([FromQuery] int a, [FromQuery] int b)
        => await _svc.AddAsync(a, b);
}
