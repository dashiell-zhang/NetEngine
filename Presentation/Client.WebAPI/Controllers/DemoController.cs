using Application.Model.Site.Article;
using Application.Service.LLM;
using Client.WebAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace Client.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]/[action]")]
public sealed class DemoController(IDemoService _svc, Demo2Service _svc2) : ControllerBase
{


    [HttpGet]
    public async Task<bool> Test([FromServices] LlmInvokeService llmInvokeService)
    {
        //string code = "sum";

        string code = "sumqw";
        Dictionary<string, string> args = new();
        args["a"] = "5";
        args["b"] = "9";

        var s = await llmInvokeService.ChatContentAsync(code, args);

        return true;
    }


    [HttpGet]
    public ActionResult<string> Echo([FromQuery] string name = "world")
        => _svc.Echo(name);

    [HttpGet]
    public async Task<ActionResult<int>> Add([FromQuery] int a, [FromQuery] int b)
        => await _svc.AddAsync(a, b);


    [HttpGet]
    public ActionResult<string> Echo2([FromQuery] string name = "world")
        => _svc2.Echo(name);

    [HttpPost]
    public async Task<ActionResult<int>> Add2([FromQuery] int a, [FromQuery] int b, [FromBody] ArticleDto dtoArticle)
        => await _svc2.AddAsync(a, b, dtoArticle);


    [HttpGet]
    public int ARef([FromQuery] int a, [FromQuery] int b)
    {
        var s = _svc2.Add(ref a, b);

        return s;
    }


    [HttpGet]
    public IAsyncEnumerable<int> StreamNumbers([FromQuery] int count = 5)
    {
        return _svc2.StreamNumbers(count);
    }


    [HttpGet]
    public Task<IAsyncEnumerable<int>> StreamNumbers2([FromQuery] int count = 5)
    {
        return _svc2.StreamNumbersAsync(count);
    }

}
