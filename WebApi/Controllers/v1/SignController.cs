using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Repository.Database;
using System;
using System.Linq;
using WebApi.Libraries;
using WebApi.Models.v1.Sign;

namespace WebApi.Controllers.v1
{

    /// <summary>
    /// 标记相关控制器
    /// </summary>
    [ApiVersion("1")]
    [Route("api/[controller]")]
    [Authorize]
    [ApiController]
    public class SignController : ControllerCore
    {



        /// <summary>
        /// 获取标记总数
        /// </summary>
        /// <param name="business">业务领域</param>
        /// <param name="sign">自定义标记</param>
        /// <param name="key">记录值</param>
        /// <returns></returns>
        [HttpGet("GetSignCount")]
        public int GetSignCount(string business, string sign, long key)
        {

            var count = db.TSign.AsNoTracking().Where(t => t.IsDelete == false && t.Table == business && t.TableId == key && t.Sign == sign).Count();

            return count;
        }



        /// <summary>
        /// 新增标记
        /// </summary>
        /// <param name="addSign"></param>
        /// <returns></returns>
        [HttpPost("AddSign")]
        public bool AddSign(DtoSign addSign)
        {
            TSign sign = new();

            sign.Id = snowflakeHelper.GetId();
            sign.CreateTime = DateTime.UtcNow;
            sign.CreateUserId = userId;
            sign.Table = addSign.Business;
            sign.TableId = addSign.Key;
            sign.Sign = addSign.Sign;
            db.TSign.Add(sign);
            db.SaveChanges();

            return true;
        }



        /// <summary>
        /// 删除标记
        /// </summary>
        /// <param name="deleteSign"></param>
        /// <returns></returns>
        [HttpDelete("DeleteSign")]
        public bool DeleteSign([FromQuery] DtoSign deleteSign)
        {
            var sign = db.TSign.Where(t => t.IsDelete == false && t.CreateUserId == userId && t.Table == deleteSign.Business && t.TableId == deleteSign.Key && t.Sign == deleteSign.Sign).FirstOrDefault();

            if (sign != null)
            {
                sign.IsDelete = true;
                sign.DeleteTime = DateTime.UtcNow;
                sign.DeleteUserId = userId;

                db.SaveChanges();
            }

            return true;
        }

    }
}
