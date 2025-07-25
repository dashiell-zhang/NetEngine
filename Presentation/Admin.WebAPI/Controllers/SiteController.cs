using Application.Model.Shared;
using Application.Model.Site.Site;
using Application.Service.Site;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebAPI.Core.Filters;

namespace Admin.WebAPI.Controllers
{

    /// <summary>
    /// 站点控制器
    /// </summary>
    [SignVerifyFilter]
    [Authorize]
    [Route("[controller]/[action]")]
    [ApiController]
    public class SiteController(SiteService siteService) : ControllerBase
    {


        /// <summary>
        /// 获取站点信息
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public Task<DtoSite> GetSite() => siteService.GetSiteAsync();



        /// <summary>
        /// 编辑站点信息
        /// </summary>
        /// <param name="editSite"></param>
        /// <returns></returns>
        [HttpPost]
        public Task<bool> EditSite(DtoSite editSite) => siteService.EditSiteAsync(editSite);



        /// <summary>
        /// 获取服务器信息
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public List<DtoKeyValue> GetServerInfo()
        {
            List<DtoKeyValue> list =
            [
                new DtoKeyValue
                {
                    Key = "服务器名称",
                    Value = Environment.MachineName
                },

                new DtoKeyValue
                {
                    Key = "服务器IP",
                    Value = HttpContext.Connection.LocalIpAddress!.ToString()
                },

                new DtoKeyValue
                {
                    Key = "操作系统",
                    Value = Environment.OSVersion.ToString()
                },

                new DtoKeyValue
                {
                    Key = "外部端口",
                    Value = HttpContext.Connection.LocalPort.ToString()
                },

                new DtoKeyValue
                {
                    Key = "目录物理路径",
                    Value = Environment.CurrentDirectory
                },

                new DtoKeyValue
                {
                    Key = "服务器CPU",
                    Value = Environment.ProcessorCount.ToString() + "核"
                },

                new DtoKeyValue
                {
                    Key = "本网站占用内存",
                    Value = ((double)GC.GetTotalMemory(false) / 1048576).ToString("N2") + "M"
                }
            ];

            return list;
        }


    }
}