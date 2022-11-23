using AdminAPI.Libraries.Ueditor;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdminAPI.Controllers
{
    [Authorize]
    [Route("[controller]")]
    [ApiController]
    public class UeditorController : ControllerBase
    {

        private readonly IWebHostEnvironment webHostEnvironment;
        private readonly IConfiguration configuration;

        public UeditorController(IWebHostEnvironment webHostEnvironment, IConfiguration configuration)
        {
            this.webHostEnvironment = webHostEnvironment;
            this.configuration = configuration;
        }


        [DisableRequestSizeLimit]
        [HttpGet("ProcessRequest")]
        [HttpPost("ProcessRequest")]
        public string ProcessRequest()
        {
            string rootPath = webHostEnvironment.WebRootPath;

            string fileServerUrl = configuration["FileServerUrl"]?.ToString() ?? "";

            Handler action = (Request.Query["action"].Count != 0 ? Request.Query["action"].ToString() : "") switch
            {
                "config" => new ConfigHandler(),
                "uploadimage" => new UploadHandler(new UploadConfig()
                {
                    AllowExtensions = Config.GetStringList("imageAllowFiles", fileServerUrl),
                    PathFormat = Config.GetString("imagePathFormat", fileServerUrl),
                    SizeLimit = Config.GetInt("imageMaxSize", fileServerUrl),
                    UploadFieldName = Config.GetString("imageFieldName", fileServerUrl)
                }, rootPath, HttpContext),
                "uploadscrawl" => new UploadHandler(new UploadConfig()
                {
                    AllowExtensions = new string[] { ".png" },
                    PathFormat = Config.GetString("scrawlPathFormat", fileServerUrl),
                    SizeLimit = Config.GetInt("scrawlMaxSize", fileServerUrl),
                    UploadFieldName = Config.GetString("scrawlFieldName", fileServerUrl),
                    Base64 = true,
                    Base64Filename = "scrawl.png"
                }, rootPath, HttpContext),
                "uploadvideo" => new UploadHandler(new UploadConfig()
                {
                    AllowExtensions = Config.GetStringList("videoAllowFiles", fileServerUrl),
                    PathFormat = Config.GetString("videoPathFormat", fileServerUrl),
                    SizeLimit = Config.GetInt("videoMaxSize", fileServerUrl),
                    UploadFieldName = Config.GetString("videoFieldName", fileServerUrl)
                }, rootPath, HttpContext),
                "uploadfile" => new UploadHandler(new UploadConfig()
                {
                    AllowExtensions = Config.GetStringList("fileAllowFiles", fileServerUrl),
                    PathFormat = Config.GetString("filePathFormat", fileServerUrl),
                    SizeLimit = Config.GetInt("fileMaxSize", fileServerUrl),
                    UploadFieldName = Config.GetString("fileFieldName", fileServerUrl)
                }, rootPath, HttpContext),
                "catchimage" => new CrawlerHandler(rootPath, HttpContext),
                _ => new NotSupportedHandler(),
            };
            return action.Process(fileServerUrl);
        }


    }
}