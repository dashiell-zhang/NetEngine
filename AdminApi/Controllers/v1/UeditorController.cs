using AdminApi.Libraries.Ueditor;
using Common;
using Common.DistributedLock;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Repository.Database;

namespace AdminApi.Controllers.v1
{
    [Authorize]
    [ApiVersion("1")]
    [Route("api/[controller]")]
    [ApiController]
    public class UeditorController : ControllerBase
    {

        private readonly IWebHostEnvironment webHostEnvironment;

        public UeditorController(IWebHostEnvironment webHostEnvironment)
        {
            this.webHostEnvironment = webHostEnvironment;
        }


        [DisableRequestSizeLimit]
        [HttpGet("ProcessRequest")]
        [HttpPost("ProcessRequest")]
        public string ProcessRequest()
        {
            string rootPath = webHostEnvironment.WebRootPath.Replace("\\", "/");

            Handler action = (Request.Query["action"].Count != 0 ? Request.Query["action"].ToString() : "") switch
            {
                "config" => new ConfigHandler(),
                "uploadimage" => new UploadHandler(new UploadConfig()
                {
                    AllowExtensions = Config.GetStringList("imageAllowFiles"),
                    PathFormat = Config.GetString("imagePathFormat"),
                    SizeLimit = Config.GetInt("imageMaxSize"),
                    UploadFieldName = Config.GetString("imageFieldName")
                }, rootPath),
                "uploadscrawl" => new UploadHandler(new UploadConfig()
                {
                    AllowExtensions = new string[] { ".png" },
                    PathFormat = Config.GetString("scrawlPathFormat"),
                    SizeLimit = Config.GetInt("scrawlMaxSize"),
                    UploadFieldName = Config.GetString("scrawlFieldName"),
                    Base64 = true,
                    Base64Filename = "scrawl.png"
                }, rootPath),
                "uploadvideo" => new UploadHandler(new UploadConfig()
                {
                    AllowExtensions = Config.GetStringList("videoAllowFiles"),
                    PathFormat = Config.GetString("videoPathFormat"),
                    SizeLimit = Config.GetInt("videoMaxSize"),
                    UploadFieldName = Config.GetString("videoFieldName")
                }, rootPath),
                "uploadfile" => new UploadHandler(new UploadConfig()
                {
                    AllowExtensions = Config.GetStringList("fileAllowFiles"),
                    PathFormat = Config.GetString("filePathFormat"),
                    SizeLimit = Config.GetInt("fileMaxSize"),
                    UploadFieldName = Config.GetString("fileFieldName")
                }, rootPath),
                "catchimage" => new CrawlerHandler(rootPath),
                _ => new NotSupportedHandler(),
            };
            return action.Process();
        }


    }
}