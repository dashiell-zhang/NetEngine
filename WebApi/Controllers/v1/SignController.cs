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
    public class SignController : ControllerCore
    {



        /// <summary>
        /// 获取标记总数
        /// </summary>
        /// <param name="table"></param>
        /// <param name="tableId"></param>
        /// <param name="sign"></param>
        /// <returns></returns>
        [HttpGet("GetSignCount")]
        public int GetSignCount(string table, long tableId, string sign)
        {

            var count = db.TSign.AsNoTracking().Where(t => t.IsDelete == false && t.Table == table && t.TableId == tableId && t.Sign == sign).Count();

            return count;
        }


        /// <summary>
        /// 新增标记
        /// </summary>
        /// <param name="addSign"></param>
        /// <returns></returns>
        [HttpPost("AddSign")]
        public bool AddSign([FromBody] dtoSign addSign)
        {
            TSign sign = new();

            sign.Id = snowflakeHelper.GetId();
            sign.CreateTime = DateTime.UtcNow;
            sign.CreateUserId = userId;
            sign.Table = addSign.Table;
            sign.TableId = addSign.TableId;
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
        public bool DeleteSign(dtoSign deleteSign)
        {
            var sign = db.TSign.Where(t => t.IsDelete == false && t.CreateUserId == userId && t.Table == deleteSign.Table && t.TableId == deleteSign.TableId && t.Sign == deleteSign.Sign).FirstOrDefault();

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
