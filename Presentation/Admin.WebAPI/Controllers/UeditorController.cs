using Admin.WebAPI.Libraries.Ueditor;
using Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Admin.WebAPI.Controllers;
[Authorize]
[Route("[controller]/[action]")]
[ApiController]
public class UeditorController(IWebHostEnvironment webHostEnvironment, IConfiguration configuration) : ControllerBase
{

    [DisableRequestSizeLimit]
    [HttpGet]
    [HttpPost]
    public async Task<string> ProcessRequest()
    {
        string rootPath = webHostEnvironment.WebRootPath;

        string fileServerUrl = configuration["FileServerUrl"]?.ToString() ?? "";

        var actionStr = Request.Query["action"].ToString();

        if (actionStr == "config")
        {
            return JsonHelper.ObjectToJson(Config.Items(fileServerUrl));
        }
        else if (actionStr == "uploadimage")
        {
            UploadHandler uploadHandler = new(new()
            {
                AllowExtensions = Config.GetStringList("imageAllowFiles", fileServerUrl),
                PathFormat = Config.GetString("imagePathFormat", fileServerUrl),
                SizeLimit = Config.GetInt("imageMaxSize", fileServerUrl),
                UploadFieldName = Config.GetString("imageFieldName", fileServerUrl)
            }, rootPath, HttpContext);

            return await uploadHandler.ProcessAsync();
        }
        else if (actionStr == "uploadscrawl")
        {
            UploadHandler uploadHandler = new(new()
            {
                AllowExtensions = [".png"],
                PathFormat = Config.GetString("scrawlPathFormat", fileServerUrl),
                SizeLimit = Config.GetInt("scrawlMaxSize", fileServerUrl),
                UploadFieldName = Config.GetString("scrawlFieldName", fileServerUrl),
                Base64 = true,
                Base64Filename = "scrawl.png"
            }, rootPath, HttpContext);

            return await uploadHandler.ProcessAsync();
        }
        else if (actionStr == "uploadvideo")
        {
            UploadHandler uploadHandler = new(new()
            {
                AllowExtensions = Config.GetStringList("videoAllowFiles", fileServerUrl),
                PathFormat = Config.GetString("videoPathFormat", fileServerUrl),
                SizeLimit = Config.GetInt("videoMaxSize", fileServerUrl),
                UploadFieldName = Config.GetString("videoFieldName", fileServerUrl)
            }, rootPath, HttpContext);

            return await uploadHandler.ProcessAsync();
        }
        else if (actionStr == "uploadfile")
        {
            UploadHandler uploadHandler = new(new()
            {
                AllowExtensions = Config.GetStringList("fileAllowFiles", fileServerUrl),
                PathFormat = Config.GetString("filePathFormat", fileServerUrl),
                SizeLimit = Config.GetInt("fileMaxSize", fileServerUrl),
                UploadFieldName = Config.GetString("fileFieldName", fileServerUrl)
            }, rootPath, HttpContext);

            return await uploadHandler.ProcessAsync();
        }
        else if (actionStr == "catchimage")
        {
            CrawlerHandler crawlerHandler = new(rootPath, HttpContext);
            return await crawlerHandler.ProcessAsync(fileServerUrl);
        }
        else
        {
            return JsonHelper.ObjectToJson(new
            {
                state = "action 参数为空或者 action 不被支持。"
            });
        }

    }

}
