using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Models.WebCore;

namespace WebApi.Controllers
{

    /// <summary>
    /// 文件上传控制器
    /// </summary>
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class FileController : ControllerBase
    {



        /// <summary>
        /// 单文件上传接口
        /// </summary>
        /// <param name="Authorization">token</param>
        /// <param name="file">file</param>
        /// <returns>文件ID</returns>
        [HttpPost("UploadFile")]
        public string UploadFile([Required][FromHeader] string Authorization, [Required]IFormFile file)
        {

            string userid = Methods.Verify.JwtToken.GetClaims("userid");

            var url = string.Empty;
            var fileName = string.Empty;
            var fileExtension = string.Empty;
            var fullFileName = string.Empty;

            string basepath = "\\Files\\" + DateTime.Now.ToString("yyyyMMdd");
            string filepath = Methods.IO.Path.ContentRootPath() + basepath;

            Directory.CreateDirectory(filepath);

            fileName = Guid.NewGuid().ToString();
            fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
            fullFileName = string.Format("{0}{1}", fileName, fileExtension);

            string path = "";

            if (file != null && file.Length > 0)
            {
                path = filepath + "\\" + fullFileName;

                using (FileStream fs = System.IO.File.Create(path))
                {
                    file.CopyTo(fs);
                    fs.Flush();
                }

                path = basepath + "\\" + fullFileName;
            }

            using (webcoreContext db = new webcoreContext())
            {
                var f = new TFile();
                f.Id = fileName;
                f.Name = file.FileName;
                f.Path = path;
                f.Createuserid = userid;
                f.Createtime = DateTime.Now;

                db.TFile.Add(f);
                db.SaveChanges();
            }

            return fileName;

        }




        /// <summary>
        /// 通过文件ID获取文件
        /// </summary>
        /// <param name="Authorization">Authorization</param>
        /// <param name="fileid">文件ID</param>
        /// <returns></returns>
        [HttpGet("GetFile")]
        public FileResult GetFile([Required][FromHeader] string Authorization, [Required]string fileid)
        {
            using (webcoreContext db = new webcoreContext())
            {
                var file = db.TFile.Where(t => t.Id == fileid).FirstOrDefault();
                string path = Methods.IO.Path.ContentRootPath() + file.Path;


                //读取文件入流
                var stream = System.IO.File.OpenRead(path);

                //获取文件后缀
                string fileExt = Path.GetExtension(path);

                //获取系统常规全部mime类型
                var provider = new FileExtensionContentTypeProvider();

                //通过文件后缀寻找对呀的mime类型
                var memi = provider.Mappings[fileExt];


                //通过路径获取文件名称
                //Path.GetFileName(path)

                return File(stream, memi, file.Name);

            }

        }


    }
}