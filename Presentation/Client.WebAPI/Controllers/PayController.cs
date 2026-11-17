using Application.Model.Pay;
using Application.Interface;
using Microsoft.AspNetCore.Mvc;
using WebAPI.Core.Extensions;

namespace Client.WebAPI.Controllers;

/// <summary>
/// 第三方支付发起集合，依赖于订单号
/// </summary>
[Route("[controller]/[action]")]
[ApiController]
public class PayController(IPayService payService) : ControllerBase
{


    /// <summary>
    /// 微信支付-JSAPI模式
    /// </summary>
    /// <param name="orderNo">订单号</param>
    /// <param name="openId">用户OpenId</param>
    /// <returns></returns>
    [HttpGet]
    public Task<CreateWeiXinPayJSAPIRetDto?> CreateWeiXinPayJSAPI(string orderNo, string openId)
    {

        var notifyUrl = HttpContext.GetBaseUrl() + "/Pay/WeiXinPayNotify/";

        return payService.CreateWeiXinPayJSAPIAsync(orderNo, openId, notifyUrl);

    }



    /// <summary>
    /// 微信支付-App模式
    /// </summary>
    /// <param name="orderNo">订单号</param>
    /// <returns></returns>
    [HttpGet]
    public Task<CreateWeiXinPayAppRetDto?> CreateWeiXinPayApp(string orderNo)
    {

        var notifyUrl = HttpContext.GetBaseUrl() + "/Pay/WeiXinPayNotify/";

        return payService.CreateWeiXinPayAppAsync(orderNo, notifyUrl);

    }



    /// <summary>
    /// 微信支付-H5模式
    /// </summary>
    /// <param name="orderNo">订单号</param>
    /// <returns></returns>
    [HttpGet]
    public Task<string?> CreateWeiXinPayH5(string orderNo)
    {

        var notifyUrl = HttpContext.GetBaseUrl() + "/Pay/WeiXinPayNotify/";

        var clientIP = HttpContext.GetRemoteIP();

        return payService.CreateWeiXinPayH5Async(orderNo, notifyUrl, clientIP);

    }



    /// <summary>
    /// 微信支付-PC模式
    /// </summary>
    /// <param name="orderNo">订单号</param>
    /// <returns></returns>
    [HttpGet]
    public Task<string?> CreateWeiXinPayPC(string orderNo)
    {

        var notifyUrl = HttpContext.GetBaseUrl() + "/Pay/WeiXinPayNotify/";

        return payService.CreateWeiXinPayPCAsync(orderNo, notifyUrl);

    }



    /// <summary>
    /// 接收微信支付原始通知，处理成功后返回无正文的确认响应
    /// </summary>
    [HttpPost("{mchid}")]
    public async Task<IActionResult> WeiXinPayNotify(string mchId)
    {

        using var reader = new StreamReader(Request.Body, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        string requestBody = await reader.ReadToEndAsync();

        var headers = HttpContext.Request.Headers.ToDictionary(t => t.Key, t => t.Value.ToString());

        bool accepted = await payService.WeiXinPayNotifyAsync(mchId, headers, requestBody);

        if (!accepted)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { code = "FAIL", message = "处理失败" });
        }

        return NoContent();

    }



    /// <summary>
    /// 支付宝支付-小程序模式
    /// </summary>
    /// <param name="orderNo">订单号</param>
    /// <returns></returns>
    [HttpGet]
    public Task<string?> CreateAliPayMiniApp(string orderNo)
    {

        var notifyUrl = HttpContext.GetBaseUrl() + "/Pay/AliPayNotify";

        return payService.CreateAliPayMiniAppAsync(orderNo, notifyUrl);

    }




    /// <summary>
    /// 支付宝支付-PC模式
    /// </summary>
    /// <param name="orderNo">订单号</param>
    /// <returns>支付宝支付Url</returns>
    [HttpGet]
    public Task<string?> CreateAliPayPC(string orderNo)
    {

        var notifyUrl = HttpContext.GetBaseUrl() + "/Pay/AliPayNotify";

        return payService.CreateAliPayPCAsync(orderNo, notifyUrl, null);

    }



    /// <summary>
    /// 支付宝支付-H5模式
    /// </summary>
    /// <param name="orderNo">订单号</param>
    /// <returns>支付宝支付Url</returns>
    [HttpGet]
    public Task<string?> CreateAliPayH5(string orderNo)
    {

        var returnUrl = "";

        var notifyUrl = HttpContext.GetBaseUrl() + "/Pay/AliPayNotify";

        var quitUrl = "";

        return payService.CreateAliPayH5Async(orderNo, notifyUrl, returnUrl, quitUrl);

    }



    /// <summary>
    /// 接收支付宝表单通知并以纯文本确认处理结果
    /// </summary>
    /// <returns></returns>
    [HttpPost]
    public async Task<ContentResult> AliPayNotify()
    {

        var form = await Request.ReadFormAsync();
        var parameters = form.ToDictionary(t => t.Key, t => t.Value.ToString());
        bool accepted = await payService.AliPayNotifyAsync(parameters);
        return Content(accepted ? "success" : "failure", "text/plain");

    }

}
