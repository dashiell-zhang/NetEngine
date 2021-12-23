using Common;
using Medallion.Threading;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using WebApi.Libraries;
using WebApi.Models.Shared;

namespace WebApi.Controllers.v1
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
                ChildList = t.TRegionCity.Select(c => new DtoKeyValueChild
                {
                    Key = c.Id,
                    Value = c.City,
                    ChildList = c.TRegionArea.Select(a => new DtoKeyValueChild
                    {
                        Key = a.Id,
                        Value = a.Area
                    }).ToList()
                }).ToList()
            }).ToList();

            return list;
        }



        /// <summary>
        /// 二维码生成
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
        /// 图像验证码生成
        /// </summary>
        /// <param name="sign">标记</param>
        /// <returns></returns>
        [HttpGet("GetVerifyCode")]
        public FileResult GetVerifyCode(Guid sign)
        {
            var cacheKey = "VerifyCode" + sign.ToString();
            Random random = new();
            string text = random.Next(1000, 9999).ToString();

            var image = ImgHelper.GetVerifyCode(text);

            CacheHelper.SetString(cacheKey, text, TimeSpan.FromMinutes(5));

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

            var list = db.TAppSetting.Where(t => t.IsDelete == false & t.Module == "Dictionary" & t.GroupId == groupId).Select(t => new DtoKeyValue
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




        /// <summary>
        /// 发送一个CAP消息
        /// </summary>
        /// <returns></returns>
        [HttpGet("ShowMessage")]
        public bool ShowMessage(string msg)
        {

            cap.Publish("ShowMessage", msg);

            return true;
        }




        /// <summary>
        /// 分布式锁demo
        /// </summary>
        /// <returns></returns>
        [HttpGet("DistLock")]
        public bool DistLock()
        {

            //互斥锁
            using (distLock.AcquireLock(""))
            {
            }

            //互斥锁，可配置内部代码最多同时运行数
            using (distSemaphoreLock.AcquireSemaphore("", 5))
            {
            }

            //读锁，多人同读，与写锁互斥
            using (distUpgradeableLock.AcquireReadLock(""))
            {
            }


            //写锁，互斥
            using (distUpgradeableLock.AcquireWriteLock(""))
            {
            }

            //可升级读锁，初始状态为读锁，可手动升级为写锁
            using (var handle = distUpgradeableLock.AcquireUpgradeableReadLock(""))
            {

                //升级写锁
                handle.UpgradeToWriteLock();
            }

            return true;
        }


    }
}