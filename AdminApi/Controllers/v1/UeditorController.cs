using AdminApi.Libraries.Ueditor;
using Common;
using Common.DistributedLock;
using Microsoft.AspNetCore.Authorization;
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



        public UeditorController()
        {
        
        }


        [DisableRequestSizeLimit]
        [HttpGet("ProcessRequest")]
        [HttpPost("ProcessRequest")]
        public string ProcessRequest()
        {
            Handler action = (Request.Query["action"].Count != 0 ? Request.Query["action"].ToString() : "") switch
            {
                "config" => new ConfigHandler(),
                "uploadimage" => new UploadHandler(new UploadConfig()
                {
                    AllowExtensions = Config.GetStringList("imageAllowFiles"),
                    PathFormat = Config.GetString("imagePathFormat"),
                    SizeLimit = Config.GetInt("imageMaxSize"),
                    UploadFieldName = Config.GetString("imageFieldName")
                }),
                "uploadscrawl" => new UploadHandler(new UploadConfig()
                {
                    AllowExtensions = new string[] { ".png" },
                    PathFormat = Config.GetString("scrawlPathFormat"),
                    SizeLimit = Config.GetInt("scrawlMaxSize"),
                    UploadFieldName = Config.GetString("scrawlFieldName"),
                    Base64 = true,
                    Base64Filename = "scrawl.png"
                }),
                "uploadvideo" => new UploadHandler(new UploadConfig()
                {
                    AllowExtensions = Config.GetStringList("videoAllowFiles"),
                    PathFormat = Config.GetString("videoPathFormat"),
                    SizeLimit = Config.GetInt("videoMaxSize"),
                    UploadFieldName = Config.GetString("videoFieldName")
                }),
                "uploadfile" => new UploadHandler(new UploadConfig()
                {
                    AllowExtensions = Config.GetStringList("fileAllowFiles"),
                    PathFormat = Config.GetString("filePathFormat"),
                    SizeLimit = Config.GetInt("fileMaxSize"),
                    UploadFieldName = Config.GetString("fileFieldName")
                }),
                "catchimage" => new CrawlerHandler(),
                _ => new NotSupportedHandler(),
            };
            return action.Process();
        }


    }
}