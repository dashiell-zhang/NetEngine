using Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Models.Dtos;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AdminApi.Libraries;

namespace AdminApi.Controllers.v1
{
    /// <summary>
    /// 系统基础方法控制器
    /// </summary>
    [ApiVersion("1")]
    [Route("api/[controller]")]
    [ApiController]
    public class BaseController : ControllerCore
    {



        /// <summary>
        /// 获取省市级联地址数据
        /// </summary>
        /// <param name="provinceId">省份ID</param>
        /// <param name="cityId">城市ID</param>
        /// <returns></returns>
        /// <remarks>不传递任何参数返回省份数据，传入省份ID返回城市数据，传入城市ID返回区域数据</remarks>
        [HttpGet("GetRegion")]
        public List<dtoKeyValue> GetRegion(int provinceId, int cityId)
        {
            var list = new List<dtoKeyValue>();

            if (provinceId == 0 && cityId == 0)
            {
                list = db.TRegionProvince.Select(t => new dtoKeyValue { Key = t.Id, Value = t.Province }).ToList();
            }

            if (provinceId != 0)
            {
                list = db.TRegionCity.Where(t => t.ProvinceId == provinceId).Select(t => new dtoKeyValue { Key = t.Id, Value = t.City }).ToList();
            }

            if (cityId != 0)
            {
                list = db.TRegionArea.Where(t => t.CityId == cityId).Select(t => new dtoKeyValue { Key = t.Id, Value = t.Area }).ToList();
            }

            return list;
        }



        /// <summary>
        /// 获取全部省市级联地址数据
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetRegionAll")]
        public List<dtoKeyValueChild> GetRegionAll()
        {

            var list = db.TRegionProvince.Select(t => new dtoKeyValueChild
            {
                Key = t.Id,
                Value = t.Province,
                ChildList = t.TRegionCity.Select(c => new dtoKeyValueChild
                {
                    Key = c.Id,
                    Value = c.City,
                    ChildList = c.TRegionArea.Select(a => new dtoKeyValueChild
                    {
                        Key = a.Id,
                        Value = a.Area
                    }).ToList()
                }).ToList()
            }).ToList();

            return list;
        }



        /// <summary>
        /// 自定义二维码生成方法
        /// </summary>
        /// <param name="text">数据内容</param>
        /// <returns></returns>
        [HttpGet("GetQrCode")]
        public FileResult GetQrCode(string text)
        {
            var image = ImgHelper.GetQrCode(text);

            using (var ms = new MemoryStream())
            {
                image.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                return File(ms.ToArray(), "image/png");
            }
        }



        /// <summary>
        /// 获取指定组ID的KV键值对
        /// </summary>
        /// <param name="groupId"></param>
        /// <returns></returns>
        [HttpGet("GetValueList")]
        public List<dtoKeyValue> GetValueList(Guid groupId)
        {

            var list = db.TAppSetting.Where(t => t.IsDelete == false & t.Module == "Dictionary" & t.GroupId == groupId).Select(t => new dtoKeyValue
            {
                Key = t.Key,
                Value = t.Value
            }).ToList();

            return list;
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