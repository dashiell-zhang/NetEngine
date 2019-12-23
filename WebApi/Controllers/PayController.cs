using Aop.Api.Util;
using Methods.AliPay;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Models.DataBases.WebCore;
using Models.Dtos;
using System;
using System.Collections.Generic;
using System.Linq;
using WebApi.Libraries.WeiXin.MiniApp;
using WebApi.Libraries.WeiXin.MiniApp.Models;
using WebApi.Libraries.WeiXin.Public;

namespace WebApi.Controllers
{
    /// <summary>
    /// 第三方支付发起集合，依赖于订单号
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class PayController : ControllerBase
    {


        /// <summary>
        /// 微信小程序商户平台下单接口
        /// </summary>
        /// <remarks>用于在微信商户平台创建订单</remarks>
        /// <returns></returns>
        [HttpGet("CreateWeiXinPay")]
        public CreatePay CreateWeiXinPay(string orderno, string weixinkeyid)
        {

            using (webcoreContext db = new webcoreContext())
            {
                var order = db.TOrder.Where(t => t.OrderNo == orderno).FirstOrDefault();

                var userinfo = db.TUser.Where(t => t.Id == order.CreateUserId).FirstOrDefault();

                var weixinkey = db.TWeiXinKey.Where(t => t.Id == weixinkeyid).FirstOrDefault();

                WeiXinHelper weiXinHelper = new WeiXinHelper(weixinkey.WxAppId, weixinkey.WxAppSecret, weixinkey.MchId, weixinkey.MchKey, "http://www.mikekeji.com/api/Pay/WeiXinPayNotify");

                int price = Convert.ToInt32(order.Price * 100);

                string productname = db.TOrder.Where(t=>t.OrderNo==orderno).Select(t=>t.Product.Name).FirstOrDefault();


                var openid = db.TUserBindWeixin.Where(t => t.UserId == order.CreateUserId & t.WeiXinKeyId == weixinkeyid).Select(t => t.WeiXinOpenId).FirstOrDefault();

                var pay = weiXinHelper.CreatePay(openid, order.OrderNo, productname, productname + "购买", price);

                return pay;
            }

        }



        /// <summary>
        /// 微信支付异步通知接口
        /// </summary>
        [HttpPost("WeiXinPayNotify")]
        public string WeiXinPayNotify()
        {
            WxPayData notifyData = JsApiPay.GetNotifyData(); //获取微信传过来的参数

            //构造对微信的应答信息
            WxPayData res = new WxPayData();

            if (!notifyData.IsSet("transaction_id"))
            {
                //若transaction_id不存在，则立即返回结果给微信支付后台
                res.SetValue("return_code", "FAIL");
                res.SetValue("return_msg", "支付结果中微信订单号不存在");
                return res.ToXml();
            }

            //获取订单信息
            string transaction_id = notifyData.GetValue("transaction_id").ToString(); //微信流水号
            string order_no = notifyData.GetValue("out_trade_no").ToString().ToUpper(); //商户订单号
            string total_fee = notifyData.GetValue("total_fee").ToString(); //获取总金额


            //从微信验证信息真实性
            WxPayData req = new WxPayData();
            req.SetValue("transaction_id", transaction_id);


            using (webcoreContext db = new webcoreContext())
            {

                var weixinkey = db.TOrder.Where(t => t.OrderNo==order_no).Select(t=>t.CreateUser.TUserBindWeixin.FirstOrDefault().WeiXinKey).FirstOrDefault();

                JsApiPay jsApiPay = new JsApiPay(weixinkey.WxAppId, weixinkey.WxAppSecret, weixinkey.MchId, weixinkey.MchKey);

                WxPayData send = jsApiPay.OrderQuery(req);
                if (!(send.GetValue("return_code").ToString() == "SUCCESS" && send.GetValue("result_code").ToString() == "SUCCESS"))
                {
                    //如果订单信息在微信后台不存在,立即返回失败
                    res.SetValue("return_code", "FAIL");
                    res.SetValue("return_msg", "订单查询失败");
                    return res.ToXml();
                }
                else
                {

                    var order = db.TOrder.Where(t => t.OrderNo == order_no).FirstOrDefault();

                    if (order == null)
                    {
                        res.SetValue("return_code", "FAIL");
                        res.SetValue("return_msg", "订单不存在或已删除");
                        return res.ToXml();
                    }

                    if (!string.IsNullOrEmpty(order.SerialNo)) //已付款
                    {
                        res.SetValue("return_code", "SUCCESS");
                        res.SetValue("return_msg", "OK");
                        return res.ToXml();
                    }

                    try
                    {
                        order.Payprice = decimal.Parse(total_fee) / 100;
                        order.SerialNo = transaction_id;
                        order.Paystate = true;
                        order.Paytime = DateTime.Now;
                        order.PayType = "微信支付";
                        order.State = "已支付";

                        db.SaveChanges();

                        if (order.Type == "")
                        {
                           //执行业务处理逻辑
                        }


                        //返回成功通知
                        res.SetValue("return_code", "SUCCESS");
                        res.SetValue("return_msg", "OK");
                        return res.ToXml();
                    }
                    catch
                    {
                        res.SetValue("return_code", "FAIL");
                        res.SetValue("return_msg", "修改订单状态失败");
                        return res.ToXml();
                    }

                }
            }

        }




        /// <summary>
        /// 支付宝小程序商户平台下单接口
        /// </summary>
        /// <remarks>用于在支付宝商户平台创建订单</remarks>
        /// <returns></returns>
        [HttpGet("CreateAliPay")]
        public dtoKeyValue CreateAliPay(string orderno, string alipaykeyid)
        {

            using (webcoreContext db = new webcoreContext())
            {
                var order = db.TOrder.Where(t => t.OrderNo == orderno).FirstOrDefault();

                var userinfo = db.TUser.Where(t => t.Id == order.CreateUserId).FirstOrDefault();


                var alipaykey = db.TAlipayKey.Where(t => t.Id == alipaykeyid).FirstOrDefault();

                AliPayHelper aliPayHelper = new AliPayHelper(alipaykey.AppId, alipaykey.AppPrivateKey, alipaykey.AlipayPublicKey, "http://xxx.com/api/Pay/AliPayNotify");


                string price = Convert.ToString(order.Price);

                string productname = db.TOrder.Where(t => t.OrderNo == orderno).Select(t => t.Product.Name).FirstOrDefault();


                var buyuserid = db.TUserBindAlipay.Where(t => t.UserId == order.CreateUserId).Select(t => t.AlipayUserId).FirstOrDefault();

                var TradeNo = aliPayHelper.AlipayTradeCreate(order.OrderNo, productname, price, buyuserid);

                if (string.IsNullOrEmpty(TradeNo))
                {
                    HttpContext.Response.StatusCode = 400;

                    HttpContext.Items.Add("errMsg", "支付宝交易订单创建失败！");
                }

                var keyValue = new dtoKeyValue
                {
                    Key = "TradeNo",
                    Value = TradeNo
                };

                return keyValue;
            }

        }


        /// <summary>
        /// 支付宝异步通知接口
        /// </summary>
        /// <returns></returns>
        [HttpPost("AliPayNotify")]
        public string AliPayNotify()
        {
            string retValue = "";

            //获取当前请求中的post参数
            var dict = new Dictionary<string, string>();

            var keys = Request.Form.Keys;

            if (keys != null)
            {
                foreach (string key in keys)
                {
                    dict.Add(key, Request.Form[key]);
                }
            }

            if (dict.Count > 0)
            {
                var appid = Request.Form["auth_app_id"].ToString();

                using (webcoreContext db = new webcoreContext())
                {
                    var Alipaypublickey = db.TAlipayKey.Where(t => t.AppId == appid).Select(t => t.AlipayPublicKey).FirstOrDefault();


                    bool flag = AlipaySignature.RSACheckV1(dict, Alipaypublickey, "utf-8", "RSA2", false);

                    if (flag)
                    {
                        var orderno = Request.Form["out_trade_no"].ToString();

                        var order = db.TOrder.Where(t => t.OrderNo == orderno).FirstOrDefault();

                        order.Payprice = decimal.Parse(Request.Form["total_amount"].ToString());
                        order.SerialNo = Request.Form["trade_no"].ToString();
                        order.Paystate = true;
                        order.Paytime = Convert.ToDateTime(Request.Form["gmt_payment"].ToString());
                        order.PayType = "支付宝";
                        order.State = "已支付";

                        db.SaveChanges();

                        switch (order.Type)
                        {
                            case "业务逻辑":
                                {
                    
                                    retValue = "success";

                                    break;
                                }

                        }
                    }

                }

            }

            return retValue;
        }

    }
}