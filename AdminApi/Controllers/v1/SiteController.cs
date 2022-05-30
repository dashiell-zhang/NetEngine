using AdminApi.Filters;
using AdminApi.Services.v1;
using AdminShared.Models;
using AdminShared.Models.v1.Site;
using Common;
using Common.DistributedLock;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Repository.Database;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AdminApi.Controllers.v1
{

    /// <summary>
    /// 站点控制器
    /// </summary>
    [SignVerifyFilter]
    [ApiVersion("1")]
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class SiteController : ControllerBase
    {

        private readonly DatabaseContext db;

        private readonly SiteService siteService;



        public SiteController(DatabaseContext db, SiteService siteService)
        {
            this.db = db;

            this.siteService = siteService;

        }



        /// <summary>
        /// 获取站点信息
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetSite")]
        public DtoSite GetSite()
        {
            var kvList = db.TAppSetting.Where(t => t.IsDelete == false && t.Module == "Site").Select(t => new
            {
                t.Key,
                t.Value
            }).ToList();

            DtoSite site = new()
            {
                WebUrl = kvList.Where(t => t.Key == "WebUrl").Select(t => t.Value).FirstOrDefault(),
                ManagerName = kvList.Where(t => t.Key == "ManagerName").Select(t => t.Value).FirstOrDefault(),
                ManagerAddress = kvList.Where(t => t.Key == "ManagerAddress").Select(t => t.Value).FirstOrDefault(),
                ManagerPhone = kvList.Where(t => t.Key == "ManagerPhone").Select(t => t.Value).FirstOrDefault(),
                ManagerEmail = kvList.Where(t => t.Key == "ManagerEmail").Select(t => t.Value).FirstOrDefault(),
                RecordNumber = kvList.Where(t => t.Key == "RecordNumber").Select(t => t.Value).FirstOrDefault(),
                SeoTitle = kvList.Where(t => t.Key == "SeoTitle").Select(t => t.Value).FirstOrDefault(),
                SeoKeyWords = kvList.Where(t => t.Key == "SeoKeyWords").Select(t => t.Value).FirstOrDefault(),
                SeoDescription = kvList.Where(t => t.Key == "SeoDescription").Select(t => t.Value).FirstOrDefault(),
                FootCode = kvList.Where(t => t.Key == "FootCode").Select(t => t.Value).FirstOrDefault()
            };

            return site;
        }




        /// <summary>
        /// 编辑站点信息
        /// </summary>
        /// <param name="editSite"></param>
        /// <returns></returns>
        [HttpPost("EditSite")]
        public bool EditSite(DtoSite editSite)
        {
            var query = db.TAppSetting.Where(t => t.IsDelete == false && t.Module == "Site");

            siteService.SetSiteInfo("WebUrl", editSite.WebUrl);
            siteService.SetSiteInfo("ManagerName", editSite.ManagerName);
            siteService.SetSiteInfo("ManagerAddress", editSite.ManagerAddress);
            siteService.SetSiteInfo("ManagerPhone", editSite.ManagerPhone);
            siteService.SetSiteInfo("ManagerEmail", editSite.ManagerEmail);
            siteService.SetSiteInfo("RecordNumber", editSite.RecordNumber);
            siteService.SetSiteInfo("SeoTitle", editSite.SeoTitle);
            siteService.SetSiteInfo("SeoKeyWords", editSite.SeoKeyWords);
            siteService.SetSiteInfo("SeoDescription", editSite.SeoDescription);
            siteService.SetSiteInfo("FootCode", editSite.FootCode);

            return true;
        }



        /// <summary>
        /// 获取服务器信息
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetServerInfo")]
        public List<DtoKeyValue> GetServerInfo()
        {
            var list = new List<DtoKeyValue>
            {
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
                    Value = ((Double)GC.GetTotalMemory(false) / 1048576).ToString("N2") + "M"
                }
            };

            return list;
        }


    }
}