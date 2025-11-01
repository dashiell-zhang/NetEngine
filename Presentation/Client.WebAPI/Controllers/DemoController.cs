using Microsoft.AspNetCore.Mvc;

namespace Client.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class DemoController : ControllerBase
{
    private readonly Services.IDemoService _svc;

    public DemoController(IServiceProvider sp)
    {
        // Local construction with IServiceProvider so cache works
        _svc = new Services.IDemoService_Proxy(new Services.DemoService(), sp);
    }

    [HttpGet("echo")] 
    public ActionResult<string> Echo([FromQuery] string name = "world")
        => _svc.Echo(name);

    [HttpGet("add")] 
    public async Task<ActionResult<int>> Add([FromQuery] int a, [FromQuery] int b)
        => await _svc.AddAsync(a, b);
}
