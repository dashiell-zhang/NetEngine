using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Methods.WeiXin.MiniApp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Models.DataBases.WebCore;
using WebApi.Filters;

namespace WebApi.Controllers
{
    /// <summary>
    /// 用户数据操作控制器
    /// </summary>
    [Route("api/[controller]")]
    [TokenVerify]
    [ApiController]
    public class UserController : ControllerBase
    {


        /// <summary>
        /// 获取 微信Openid
        /// </summary>
        /// <param name="weixinkeyid">微信配置密钥ID</param>
        /// <param name="code">微信临时code</param>
        /// <returns>openid,userid</returns>
        /// <remarks>传入租户ID和微信临时 code 获取 openid，如果 openid 在系统有中对应用户，则一并返回用户的ID值，否则用户ID值为空</remarks>
        [HttpGet("GetWeiXinOpenId")]
        public (string openid, string userid) GetWeiXinOpenId(string weixinkeyid, string code)
        {
            using (webcoreContext db = new webcoreContext())
            {

                var weixinkey = db.TWeiXinKey.Where(t => t.Id == weixinkeyid).FirstOrDefault();

                WeiXinHelper weiXinHelper = new WeiXinHelper(weixinkey.WxAppId, weixinkey.WxAppSecret);


                var wxinfo = weiXinHelper.GetOpenIdAndSessionKey(code);

                string openid = wxinfo.openid;
                string sessionkey = wxinfo.sessionkey;

                string userid = "";

                var userinfo = db.TUserBindWeixin.Where(t => t.WeiXinOpenId == openid).Select(t => t.User).FirstOrDefault();
                if (userinfo != null)
                {
                    userid = userinfo.Id;
                }

                return (openid, userid);

            }

        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="iv">加密算法的初始向量</param>
        /// <param name="encryptedData">包括敏感数据在内的完整用户信息的加密数据</param>
        /// <param name="code">微信临时code</param>
        /// <param name="weixinkeyid">微信配置密钥ID</param>
        [HttpGet("GetWeiXinPhone")]
        public string GetWeiXinPhone(string iv, string encryptedData, string code, string weixinkeyid)
        {

            using (webcoreContext db = new webcoreContext())
            {
                var weixinkey = db.TWeiXinKey.Where(t => t.Id == weixinkeyid).FirstOrDefault();

                WeiXinHelper weiXinHelper = new WeiXinHelper(weixinkey.WxAppId, weixinkey.WxAppSecret);


                var wxinfo = weiXinHelper.GetOpenIdAndSessionKey(code);

                string openid = wxinfo.openid;
                string sessionkey = wxinfo.sessionkey;


                var strJson = WeiXinHelper.DecryptionData(encryptedData, sessionkey, iv);

                var user = db.TUserBindWeixin.Where(t => t.WeiXinOpenId == openid & t.WeiXinKeyId == weixinkeyid).Select(t => t.User).FirstOrDefault();

                user.Phone = Methods.Json.JsonHelper.GetValueByKey(strJson, "phoneNumber");

                db.SaveChanges();

                return user.Phone;

            }
        }



        /// <summary>
        /// 通过 UserId 获取用户信息 
        /// </summary>
        /// <param name="Authorization"></param>
        /// <param name="userid">用户ID</param>
        /// <returns></returns>
        [HttpGet("GetUser")]
        [CacheData(TTL = 60,UseToken =true)]
        public TUser GetUser([Required][FromHeader] string Authorization, string userid)
        {
            using (webcoreContext db = new webcoreContext())
            {
                if (string.IsNullOrEmpty(userid))
                {
                    userid = Methods.Verify.JwtToken.GetClaims("userid");
                }

                return db.TUser.Where(t => t.Id == userid).FirstOrDefault();
            }
        }



        /// <summary>
        /// 注册新用户
        /// </summary>
        /// <param name="tenantid">租户ID</param>
        /// <param name="name">姓名</param>
        /// <param name="nickname">昵称</param>
        /// <param name="headimg">头像图片地址</param>
        /// <param name="sex">性别</param>
        /// <param name="province">省份</param>
        /// <param name="city">城市</param>
        /// <param name="address">地址</param>
        /// <param name="phone">电话</param>
        /// <param name="weixinopenid">微信OpenId</param>
        /// <param name="weixinkeyid">微信小程序识别ID</param>
        /// <param name="source">用户渠道来源信息</param>
        /// <returns></returns>
        //[HttpGet("AddUser")]
        //public dtoUser AddUser(string tenantid, string name, string nickname, string headimg, string sex, string province, string city, string address, string phone, string weixinopenid, string weixinkeyid, string source)
        //{
        //    using (webcoreContext db = new platformsysContext())
        //    {
        //        var info = db.TUserBindWeixin.Where(t => t.Weixinopenid == weixinopenid).Select(t => t.User).FirstOrDefault() ?? new TUser();

        //        info.Name = name;
        //        info.Nickname = nickname;
        //        info.Headimg = headimg;
        //        info.Sex = sex;
        //        info.Province = province;
        //        info.City = city;
        //        info.Address = address;
        //        info.Phone = phone;
        //        info.Createtime = DateTime.Now;
        //        info.Isdelete = null;
        //        info.Deletetime = null;


        //        if (info.Id == null)
        //        {
        //            info.Id = Guid.NewGuid().ToString();
        //            info.Tenantid = tenantid;

        //            if (!string.IsNullOrEmpty(weixinopenid))
        //            {
        //                TUserBindWeixin userBind = new TUserBindWeixin();
        //                userBind.Id = Guid.NewGuid().ToString();
        //                userBind.Userid = info.Id;
        //                userBind.Weixinkeyid = weixinkeyid;
        //                userBind.Weixinopenid = weixinopenid;

        //                info.TUserBindWeixin.Add(userBind);
        //            }

        //            db.TUser.Add(info);

        //            db.SaveChanges();
        //        }


        //        var additional = new TUserAdditional();
        //        additional.Id = Guid.NewGuid().ToString();
        //        additional.Userid = info.Id;
        //        additional.Source = source;

        //        db.TUserAdditional.Add(additional);

        //        db.SaveChanges();


        //        return db.TUser.Where(t => t.Id == info.Id).Select(t => new dtoUser
        //        {
        //            tenantid = t.Tenantid,
        //            id = t.Id,
        //            name = t.Name,
        //            nickname = t.Nickname,
        //            headimg = t.Headimg,
        //            sex = t.Sex,
        //            province = t.Province,
        //            city = t.City,
        //            address = t.Address,
        //            phone = t.Phone,
        //            createtime = t.Createtime,
        //            weixinopenid = weixinopenid
        //        }).FirstOrDefault();
        //    }
        //}



        /// <summary>
        /// 编辑用户信息
        /// </summary>
        /// <param name="userid">用户ID</param>
        /// <param name="phone">手机号</param>
        /// <returns></returns>
        //[HttpGet("EditUser")]
        //public dtoUser EditUser(string userid, string phone)
        //{

        //    using (platformsysContext db = new platformsysContext())
        //    {
        //        var userinfo = db.TUser.Where(t => t.Id == userid).FirstOrDefault();

        //        userinfo.Phone = phone;
        //        userinfo.Updatetime = DateTime.Now;

        //        db.SaveChanges();

        //        return db.TUser.Where(t => t.Id == userid).Select(t => new dtoUser
        //        {
        //            tenantid = t.Tenantid,
        //            id = t.Id,
        //            name = t.Name,
        //            nickname = t.Nickname,
        //            headimg = t.Headimg,
        //            sex = t.Sex,
        //            province = t.Province,
        //            city = t.City,
        //            address = t.Address,
        //            phone = t.Phone,
        //            createtime = t.Createtime

        //        }).FirstOrDefault();

        //    }
        //}



    }
}