using AdminApi.Filters;
using AdminShared.Models;
using Common;
using Microsoft.AspNetCore.Mvc;
using Repository.Database;
using System.Collections.Generic;
using System.Linq;

namespace AdminApi.Controllers.v1
{
    /// <summary>
    /// 系统基础方法控制器
    /// </summary>
    [SignVerifyFilter]
    [ApiVersion("1")]
    [Route("[controller]")]
    [ApiController]
    public class BaseController : ControllerBase
    {

        private readonly DatabaseContext db;
        private readonly SnowflakeHelper snowflakeHelper;



        public BaseController(DatabaseContext db, SnowflakeHelper snowflakeHelper)
        {
            this.db = db;
            this.snowflakeHelper = snowflakeHelper;
        }



        /// <summary>
        /// 获取省市级联地址数据
        /// </summary>
        /// <param name="provinceId">省份ID</param>
        /// <param name="cityId">城市ID</param>
        /// <returns></returns>
        /// <remarks>不传递任何参数返回省份数据，传入省份ID返回城市数据，传入城市ID返回区域数据</remarks>
        [HttpGet("GetRegion")]
        public List<DtoKeyValue> GetRegion(int provinceId, int cityId)
        {
            var list = new List<DtoKeyValue>();

            if (provinceId == 0 && cityId == 0)
            {
                list = db.TRegionProvince.Select(t => new DtoKeyValue { Key = t.Id, Value = t.Province }).ToList();
            }

            if (provinceId != 0)
            {
                list = db.TRegionCity.Where(t => t.ProvinceId == provinceId).Select(t => new DtoKeyValue { Key = t.Id, Value = t.City }).ToList();
            }

            if (cityId != 0)
            {
                list = db.TRegionArea.Where(t => t.CityId == cityId).Select(t => new DtoKeyValue { Key = t.Id, Value = t.Area }).ToList();
            }

            return list;
        }



        /// <summary>
        /// 获取全部省市级联地址数据
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetRegionAll")]
        public List<DtoKeyValueChild> GetRegionAll()
        {

            var list = db.TRegionProvince.Select(t => new DtoKeyValueChild
            {
                Key = t.Id,
                Value = t.Province,
                ChildList = t.TRegionCity!.Select(c => new DtoKeyValueChild
                {
                    Key = c.Id,
                    Value = c.City,
                    ChildList = c.TRegionArea!.Select(a => new DtoKeyValueChild
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
            return File(image, "image/png");
        }



        /// <summary>
        /// 获取指定组ID的KV键值对
        /// </summary>
        /// <param name="groupId"></param>
        /// <returns></returns>
        [HttpGet("GetValueList")]
        public List<DtoKeyValue> GetValueList(long groupId)
        {

            var list = db.TAppSetting.Where(t => t.IsDelete == false && t.Module == "Dictionary" && t.GroupId == groupId).Select(t => new DtoKeyValue
            {
                Key = t.Key,
                Value = t.Value
            }).ToList();

            return list;
        }



        /// <summary>
        /// 获取一个雪花ID
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetSnowflakeId")]
        public long GetSnowflakeId()
        {
            return snowflakeHelper.GetId();
        }

    }
}