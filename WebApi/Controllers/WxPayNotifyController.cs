using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Methods.WeiXin.Public;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace WebApi.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class WxPayNotifyController : Controller
    {


        //public void Post()
        //{
        //    WxPayData notifyData = JsApiPay.GetNotifyData(); //获取微信传过来的参数

        //    //构造对微信的应答信息
        //    WxPayData res = new WxPayData();

        //    if (!notifyData.IsSet("transaction_id"))
        //    {
        //        //若transaction_id不存在，则立即返回结果给微信支付后台
        //        res.SetValue("return_code", "FAIL");
        //        res.SetValue("return_msg", "支付结果中微信订单号不存在");
        //        Response.WriteAsync(res.ToXml());
        //        return;
        //    }

        //    //获取订单信息
        //    string transaction_id = notifyData.GetValue("transaction_id").ToString(); //微信流水号
        //    string order_no = notifyData.GetValue("out_trade_no").ToString().ToUpper(); //商户订单号
        //    string total_fee = notifyData.GetValue("total_fee").ToString(); //获取总金额


        //    //从微信验证信息真实性
        //    WxPayData req = new WxPayData();
        //    req.SetValue("transaction_id", transaction_id);


        //    using (onlineeduContext db = new onlineeduContext())
        //    {
        //        var tenant = db.TOrder.Where(t => t.Orderno == order_no).Select(t => t.User.Tenant).FirstOrDefault();

        //        JsApiPay jsApiPay = new JsApiPay(tenant.Wxappid, tenant.Wxappsecret, tenant.Mchid, tenant.Mchkey);

        //        WxPayData send = jsApiPay.OrderQuery(req);
        //        if (!(send.GetValue("return_code").ToString() == "SUCCESS" && send.GetValue("result_code").ToString() == "SUCCESS"))
        //        {
        //            //如果订单信息在微信后台不存在,立即返回失败
        //            res.SetValue("return_code", "FAIL");
        //            res.SetValue("return_msg", "订单查询失败");
        //            Response.WriteAsync(res.ToXml());
        //            return;
        //        }
        //        else
        //        {

        //            var order = db.TOrder.Where(t => t.Orderno == order_no).FirstOrDefault();

        //            if (order == null)
        //            {
        //                res.SetValue("return_code", "FAIL");
        //                res.SetValue("return_msg", "订单不存在或已删除");
        //                Response.WriteAsync(res.ToXml());
        //                return;
        //            }

        //            if (!string.IsNullOrEmpty(order.Serialno)) //已付款
        //            {
        //                res.SetValue("return_code", "SUCCESS");
        //                res.SetValue("return_msg", "OK");
        //                Response.WriteAsync(res.ToXml());
        //                return;
        //            }

        //            try
        //            {
        //                order.Payprice = decimal.Parse(total_fee) / 100;
        //                order.Serialno = transaction_id;
        //                order.Paystate = 1;
        //                order.Paytime = DateTime.Now;
        //                order.Paytype = "微信支付";
        //                order.State = "已支付";

        //                db.SaveChanges();


        //                //返回成功通知
        //                res.SetValue("return_code", "SUCCESS");
        //                res.SetValue("return_msg", "OK");
        //                Response.WriteAsync(res.ToXml());
        //                return;
        //            }
        //            catch
        //            {
        //                res.SetValue("return_code", "FAIL");
        //                res.SetValue("return_msg", "修改订单状态失败");
        //                Response.WriteAsync(res.ToXml());
        //                return;
        //            }

        //        }
        //    }

        //}

    }
}
