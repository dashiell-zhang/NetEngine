using Admin.WebAPI.Libraries.Ueditor;
using Application.Service.Basic;
using Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Admin.WebAPI.Controllers;

/// <summary>
/// UEditor 请求协议适配控制器
/// </summary>
[Authorize]
[Route("[controller]/[action]")]
[ApiController]
public class UeditorController(IWebHostEnvironment webHostEnvironment, IConfiguration configuration, FileService fileService, IHttpClientFactory httpClientFactory) : ControllerBase
{

    /// <summary>
    /// 处理 UEditor 配置、上传和远程图片抓取请求
    /// </summary>
    /// <param name="uploadKey">本次文章编辑上传批次标识</param>
    /// <returns>UEditor 协议响应</returns>
    [DisableRequestSizeLimit]
    [HttpGet]
    [HttpPost]
    public async Task<string> ProcessRequest([FromQuery] string? uploadKey)
    {

        string rootPath = webHostEnvironment.WebRootPath;
        string fileServerUrl = configuration["FileServerUrl"]?.ToString() ?? string.Empty;
        var action = Request.Query["action"].ToString();

        if (action == "config")
        {
            return JsonHelper.ObjectToJson(Config.Items(fileServerUrl));
        }

        if (!long.TryParse(uploadKey, out var parsedUploadKey) || parsedUploadKey <= 0)
        {
            return WriteError("uploadKey 参数为空或格式不正确");
        }

        if (action == "catchimage")
        {
            CrawlerHandler crawlerHandler = new(rootPath, HttpContext, fileService, httpClientFactory, new()
            {
                AllowExtensions = Config.GetStringList("catcherAllowFiles", fileServerUrl),
                SizeLimit = Config.GetInt("catcherMaxSize", fileServerUrl)
            }, parsedUploadKey, fileServerUrl);

            return await crawlerHandler.ProcessAsync();
        }

        UploadHandler? uploadHandler = action switch
        {
            "uploadimage" => CreateUploadHandler(new()
            {
                AllowExtensions = Config.GetStringList("imageAllowFiles", fileServerUrl),
                SizeLimit = Config.GetInt("imageMaxSize", fileServerUrl),
                UploadFieldName = Config.GetString("imageFieldName", fileServerUrl)
            }, parsedUploadKey, "content-image", fileServerUrl),
            "uploadscrawl" => CreateUploadHandler(new()
            {
                AllowExtensions = [".png"],
                SizeLimit = Config.GetInt("scrawlMaxSize", fileServerUrl),
                UploadFieldName = Config.GetString("scrawlFieldName", fileServerUrl),
                Base64 = true,
                Base64Filename = "scrawl.png"
            }, parsedUploadKey, "content-scrawl", fileServerUrl),
            "uploadvideo" => CreateUploadHandler(new()
            {
                AllowExtensions = Config.GetStringList("videoAllowFiles", fileServerUrl),
                SizeLimit = Config.GetInt("videoMaxSize", fileServerUrl),
                UploadFieldName = Config.GetString("videoFieldName", fileServerUrl)
            }, parsedUploadKey, "content-video", fileServerUrl),
            "uploadfile" => CreateUploadHandler(new()
            {
                AllowExtensions = Config.GetStringList("fileAllowFiles", fileServerUrl),
                SizeLimit = Config.GetInt("fileMaxSize", fileServerUrl),
                UploadFieldName = Config.GetString("fileFieldName", fileServerUrl)
            }, parsedUploadKey, "content-attachment", fileServerUrl),
            _ => null
        };

        return uploadHandler == null ? WriteError("action 参数为空或者 action 不被支持") : await uploadHandler.ProcessAsync();

    }


    /// <summary>
    /// 创建 UEditor 上传处理器
    /// </summary>
    /// <param name="uploadConfig">上传配置</param>
    /// <param name="uploadKey">上传批次标识</param>
    /// <param name="sign">文件类型标记</param>
    /// <param name="fileServerUrl">文件服务地址</param>
    /// <returns>UEditor 上传处理器</returns>
    private UploadHandler CreateUploadHandler(UploadConfig uploadConfig, long uploadKey, string sign, string fileServerUrl)
    {

        return new(uploadConfig, webHostEnvironment.WebRootPath, HttpContext, fileService, uploadKey, sign, fileServerUrl);

    }


    /// <summary>
    /// 生成 UEditor 失败响应
    /// </summary>
    /// <param name="message">错误信息</param>
    /// <returns>UEditor JSON 响应</returns>
    private static string WriteError(string message)
    {

        return JsonHelper.ObjectToJson(new
        {
            state = message
        });

    }

}
