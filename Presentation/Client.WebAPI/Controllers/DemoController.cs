using WebAPI.Core.Extensions;
using Application.Model.Site.Article;
using Application.Service.LLM;
using Client.WebAPI.Services;
using IdentifierGenerator;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace Client.WebAPI.Controllers;

/// <summary>
/// 提供源码生成器和 LLM 能力演示接口
/// </summary>
[ApiController]
[Route("api/[controller]/[action]")]
public sealed class DemoController(IDemoService _svc, Demo2Service _svc2) : ControllerBase
{


    /// <summary>
    /// 测试非流式 LLM 调用
    /// </summary>
    /// <param name="llmInvokeService">LLM 调用服务</param>
    /// <param name="idService">ID生成服务</param>
    /// <returns>LLM 回复内容</returns>
    [HttpGet]
    public async Task<string> TestLLM([FromServices] LlmInvokeService llmInvokeService, [FromServices]IdService idService)
    {
        string code = "jiafa";

        Dictionary<string, string> args = new();

        args["a"] = "5";
        args["b"] = "9";
        args["c"] = "13";


        
        var s = idService.GetId();
        

        var actorUserId = User.GetUserIdOrNull();
        var result = await llmInvokeService.ChatContentAsync(actorUserId, code, args);

        return result ?? "";
    }


    /// <summary>
    /// 测试流式 LLM 调用
    /// </summary>
    /// <param name="llmInvokeService">LLM 调用服务</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>聚合后的 LLM 回复内容</returns>
    [HttpGet]
    public async Task<string> TestLLMStream([FromServices] LlmInvokeService llmInvokeService, CancellationToken cancellationToken = default)
    {
        string code = "sumqw";

        Dictionary<string, string> args = new();

        args["a"] = "5";
        args["b"] = "9";
        args["c"] = "13";

        var sb = new StringBuilder(4096);   // 避免频繁扩容

        var actorUserId = User.GetUserIdOrNull();

        await foreach (var chunk in llmInvokeService.ChatStreamAsync(actorUserId, code, args, cancellationToken).WithCancellation(cancellationToken))
        {
            var content = chunk.Choices.FirstOrDefault()?.Delta?.Content;

            if (!string.IsNullOrEmpty(content))
            {
                sb.Append(content);
            }
        }

        return sb.ToString();
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
