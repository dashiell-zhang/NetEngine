using Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Models.Dtos;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AdminApi.Libraries;
using AdminApi.Models.v1.Site;
using Microsoft.AspNetCore.Authorization;
using Repository.Database;
using AdminApi.Actions.v1;

namespace AdminApi.Controllers.v1
{

    /// <summary>
    /// 站点控制器
    /// </summary>
    [ApiVersion("1")]
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class SiteController : ControllerCore
    {


        /// <summary>
        /// 获取站点信息
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetSite")]
        public dtoSite GetSite()
        {
            var kvList = db.TAppSetting.Where(t => t.IsDelete == false & t.Module == "Site").Select(t => new
            {
                t.Key,
                t.Value
            }).ToList();

            var site = new dtoSite();
            site.WebUrl = kvList.Where(t => t.Key == "WebUrl").Select(t => t.Value).FirstOrDefault();
            site.ManagerName = kvList.Where(t => t.Key == "ManagerName").Select(t => t.Value).FirstOrDefault();
            site.ManagerAddress = kvList.Where(t => t.Key == "ManagerAddress").Select(t => t.Value).FirstOrDefault();
            site.ManagerPhone = kvList.Where(t => t.Key == "ManagerPhone").Select(t => t.Value).FirstOrDefault();
            site.ManagerEmail = kvList.Where(t => t.Key == "ManagerEmail").Select(t => t.Value).FirstOrDefault();
            site.RecordNumber = kvList.Where(t => t.Key == "RecordNumber").Select(t => t.Value).FirstOrDefault();
            site.SeoTitle = kvList.Where(t => t.Key == "SeoTitle").Select(t => t.Value).FirstOrDefault();
            site.SeoKeyWords = kvList.Where(t => t.Key == "SeoKeyWords").Select(t => t.Value).FirstOrDefault();
            site.SeoDescription = kvList.Where(t => t.Key == "SeoDescription").Select(t => t.Value).FirstOrDefault();
            site.FootCode = kvList.Where(t => t.Key == "FootCode").Select(t => t.Value).FirstOrDefault();

            return site;
        }




        /// <summary>
        /// 编辑站点信息
        /// </summary>
        /// <param name="editSite"></param>
        /// <returns></returns>
        [HttpPost("EditSite")]
        public bool EditSite(dtoSite editSite)
        {
            var query = db.TAppSetting.Where(t => t.IsDelete == false & t.Module == "Site");

            var appSetting = new TAppSetting();

            SiteAction.SetSiteInfo("WebUrl", editSite.WebUrl);
            SiteAction.SetSiteInfo("ManagerName", editSite.ManagerName);
            SiteAction.SetSiteInfo("ManagerAddress", editSite.ManagerAddress);
            SiteAction.SetSiteInfo("ManagerPhone", editSite.ManagerPhone);
            SiteAction.SetSiteInfo("ManagerEmail", editSite.ManagerEmail);
            SiteAction.SetSiteInfo("RecordNumber", editSite.RecordNumber);
            SiteAction.SetSiteInfo("SeoTitle", editSite.SeoTitle);
            SiteAction.SetSiteInfo("SeoKeyWords", editSite.SeoKeyWords);
            SiteAction.SetSiteInfo("SeoDescription", editSite.SeoDescription);
            SiteAction.SetSiteInfo("FootCode", editSite.FootCode);

            return true;
        }



        /// <summary>
        /// 获取服务器信息
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetServerInfo")]
        public List<dtoKeyValue> GetServerInfo()
        {
            var list = new List<dtoKeyValue>();

            list.Add(new dtoKeyValue
            {
                Key = "服务器名称",
                Value = Environment.MachineName
            });

            list.Add(new dtoKeyValue
            {
                Key = "服务器IP",
                Value = HttpContext.Connection.LocalIpAddress.ToString()
            });

            list.Add(new dtoKeyValue
            {
                Key = "操作系统",
                Value = Environment.OSVersion.ToString()
            });

            list.Add(new dtoKeyValue
            {
                Key = "外部端口",
                Value = HttpContext.Connection.LocalPort.ToString()
            });

            list.Add(new dtoKeyValue
            {
                Key = "目录物理路径",
                Value = Environment.CurrentDirectory
            });

            list.Add(new dtoKeyValue
            {
                Key = "服务器CPU",
                Value = Environment.ProcessorCount.ToString() + "核"
            });

            list.Add(new dtoKeyValue
            {
                Key = "本网站占用内存",
                Value = ((Double)GC.GetTotalMemory(false) / 1048576).ToString("N2") + "M"
            });

            return list;
        }


    }
}