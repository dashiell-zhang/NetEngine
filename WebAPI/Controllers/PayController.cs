using Aop.Api;
using Aop.Api.Domain;
using Aop.Api.Request;
using Aop.Api.Response;
using Aop.Api.Util;
using Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Repository.Database;
using System.Text;
using WebAPI.Libraries;
using WebAPI.Libraries.WeiXin.App.Models;
using WebAPI.Libraries.WeiXin.MiniApp.Models;
using WebAPI.Libraries.WeiXin.Public;
using WebAPI.Models.Shared;

namespace WebAPI.Controllers
{

    /// <summary>
    /// 第三方支付发起集合，依赖于订单号
    /// </summary>
    [Route("[controller]/[action]")]
    [ApiController]
    public class PayController : ControllerBase
    {


        private readonly DatabaseContext db;
        private readonly IDistributedCache distributedCache;
        private readonly HttpClient httpClient;



        public PayController(DatabaseContext db, IDistributedCache distributedCache, IHttpClientFactory httpClientFactory)
        {
            this.db = db;
            this.distributedCache = distributedCache;
            httpClient = httpClientFactory.CreateClient();
        }



        /// <summary>
        /// 微信支付-小程序模式
        /// </summary>
        /// <remarks>用于在微信商户平台创建订单</remarks>
        /// <returns></returns>
        [HttpGet]
        public DtoCreatePayMiniApp? CreateWeiXinPayMiniAPP(string orderno, long weiXinKeyId)
        {
            var settings = db.TAppSetting.AsNoTracking().Where(t => t.Module == "WeiXinMiniApp" && t.GroupId == weiXinKeyId).ToList();

            var appId = settings.Where(t => t.Key == "AppId").Select(t => t.Value).FirstOrDefault();
            var appSecret = settings.Where(t => t.Key == "AppSecret").Select(t => t.Value).FirstOrDefault();

            var order = db.TOrder.Where(t => t.OrderNo == orderno).Select(t => new
            {
                t.OrderNo,
                t.Price,
                ProductName = DateTime.UtcNow.ToString("yyyyMMddHHmm") + "交易",
                t.CreateUserId,
                UserOpenId = db.TUserBindExternal.Where(w => w.UserId == t.CreateUserId && w.AppName == "WeiXinMiniApp" && w.AppId == appId).Select(w => w.OpenId).FirstOrDefault()
            }).FirstOrDefault();


            if (appId != null && appSecret != null && order != null)
            {
                var url = HttpContext.GetBaseURL() + "/api/Pay/WeiXinPayNotify";

                var mchId = settings.Where(t => t.Key == "MchId").Select(t => t.Value).FirstOrDefault();
                var mchKey = settings.Where(t => t.Key == "MchKey").Select(t => t.Value).FirstOrDefault();

                Libraries.WeiXin.MiniApp.WeiXinHelper weiXinHelper = new(appId, appSecret, mchId, mchKey, url);

                int price = Convert.ToInt32(order.Price * 100);

                var pay = weiXinHelper.CreatePay(httpClient, order.UserOpenId!, order.OrderNo, order.ProductName, price);

                return pay;
            }
            else
            {
                return null; ;
            }


        }



        /// <summary>
        /// 微信支付-APP模式
        /// </summary>
        /// <param name="orderNo"></param>
        /// <param name="weiXinKeyId"></param>
        /// <remarks>用于在微信商户平台创建订单</remarks>
        /// <returns></returns>
        [HttpGet]
        public DtoCreatePayApp? CreateWeiXinPayAPP(string orderNo, long weiXinKeyId)
        {

            var order = db.TOrder.AsNoTracking().Where(t => t.OrderNo == orderNo).FirstOrDefault();

            var settings = db.TAppSetting.AsNoTracking().Where(t => t.Module == "WeiXinApp" && t.GroupId == weiXinKeyId).ToList();

            var appId = settings.Where(t => t.Key == "AppId").Select(t => t.Value).FirstOrDefault();

            var mchId = settings.Where(t => t.Key == "MchId").Select(t => t.Value).FirstOrDefault();
            var mchKey = settings.Where(t => t.Key == "MchKey").Select(t => t.Value).FirstOrDefault();

            var url = HttpContext.GetBaseURL() + "/Pay/WeiXinPayNotify";

            if (appId != null && mchId != null && order != null)
            {
                Libraries.WeiXin.App.WeiXinHelper weiXinHelper = new(appId, mchId, mchKey, url);

                int price = Convert.ToInt32(order.Price * 100);

                var pay = weiXinHelper.CreatePay(httpClient, order.OrderNo, "订单号：" + orderNo, price, "119.29.29.29");

                return pay;
            }
            else
            {
                return null;
            }
        }



        /// <summary>
        /// 微信支付-H5模式
        /// </summary>
        /// <param name="orderNo"></param>
        /// <returns></returns>
        [HttpGet]
        public string? CreateWeiXinPayH5(string orderNo)
        {
            string key = "wxpayH5URL" + orderNo;

            string? h5URL = distributedCache.GetString(key);

            if (string.IsNullOrEmpty(h5URL))
            {
                var order = db.TOrder.AsNoTracking().Where(t => t.OrderNo == orderNo).Select(t => new { t.Id, t.OrderNo, t.Price }).FirstOrDefault();

                if (order != null)
                {
                    var settings = db.TAppSetting.AsNoTracking().Where(t => t.Module == "WeiXinPay").ToList();

                    var appId = settings.Where(t => t.Key == "AppId").Select(t => t.Value).FirstOrDefault();
                    var mchId = settings.Where(t => t.Key == "MchId").Select(t => t.Value).FirstOrDefault();
                    var mchApiCertId = settings.Where(t => t.Key == "MchApiCertId").Select(t => t.Value).FirstOrDefault();
                    var mchApiCertKey = settings.Where(t => t.Key == "MchApiCertKey").Select(t => t.Value).FirstOrDefault();

                    if (appId != null && mchId != null && mchApiCertId != null && mchApiCertKey != null && order != null)
                    {
                        int price = Convert.ToInt32(order.Price * 100);

                        var notifyURL = HttpContext.GetBaseURL() + "/Pay/WeiXinPayNotify";


                        var reqData = new
                        {
                            mchid = mchId,
                            out_trade_no = order.OrderNo,
                            appid = appId,
                            description = DateTime.UtcNow.ToString("yyyyMMddHHmm") + "交易",
                            notify_url = notifyURL,
                            amount = new
                            {
                                total = price,
                                currency = "CNY"
                            },
                            scene_info = new
                            {
                                payer_client_ip = HttpContext.GetRemoteIP(),
                                h5_info = new
                                {
                                    type = "Wap"
                                }
                            }
                        };

                        var reqDataJson = JsonHelper.ObjectToJson(reqData);

                        string wxURL = "https://api.mch.weixin.qq.com/v3/pay/transactions/h5";

                        string method = "POST";
                        long timeStamp = DateTimeOffset.Now.ToUnixTimeSeconds();
                        string nonceStr = Path.GetRandomFileName();
                        string message = $"{method}\n{wxURL[29..]}\n{timeStamp}\n{nonceStr}\n{reqDataJson}\n";
                        string signature = CryptoHelper.SHA256withRSAToBase64(message, mchApiCertKey);
                        string authorization = $"WECHATPAY2-SHA256-RSA2048 mchid=\"{mchId}\",nonce_str=\"{nonceStr}\",timestamp=\"{timeStamp}\",serial_no=\"{mchApiCertId}\",signature=\"{signature}\"";

                        Dictionary<string, string> headers = new()
                        {
                            { "Accept", "*/*" },
                            { "User-Agent", ".NET HttpClient" },
                            { "Authorization", authorization }
                        };

                        var resultJson = httpClient.Post(wxURL, reqDataJson, "json", headers);

                        h5URL = JsonHelper.GetValueByKey(resultJson, "h5_url");

                        if (h5URL == null)
                        {
                            string? errCode = JsonHelper.GetValueByKey(resultJson, "code");
                            string? errMessage = JsonHelper.GetValueByKey(resultJson, "message");

                            HttpContext.SetErrMsg($"{errCode}:{errMessage}");
                        }
                        else
                        {
                            distributedCache.SetString(key, h5URL);
                        }
                    }
                }
            }

            return h5URL;
        }



        /// <summary>
        /// 微信支付-PC模式
        /// </summary>
        /// <param name="orderNo"></param>
        /// <returns></returns>
        [HttpGet]
        public string? CreateWeiXinPayPC(string orderNo)
        {
            string key = "wxpayPCURL" + orderNo;

            string? codeURL = distributedCache.GetString(key);

            if (string.IsNullOrEmpty(codeURL))
            {
                var order = db.TOrder.AsNoTracking().Where(t => t.OrderNo == orderNo).Select(t => new { t.Id, t.OrderNo, t.Price }).FirstOrDefault();

                if (order != null)
                {
                    var settings = db.TAppSetting.AsNoTracking().Where(t => t.Module == "WeiXinPay").ToList();

                    var appId = settings.Where(t => t.Key == "AppId").Select(t => t.Value).FirstOrDefault();
                    var mchId = settings.Where(t => t.Key == "MchId").Select(t => t.Value).FirstOrDefault();
                    var mchApiCertId = settings.Where(t => t.Key == "MchApiCertId").Select(t => t.Value).FirstOrDefault();
                    var mchApiCertKey = settings.Where(t => t.Key == "MchApiCertKey").Select(t => t.Value).FirstOrDefault();

                    if (appId != null && mchId != null && mchApiCertId != null && mchApiCertKey != null && order != null)
                    {
                        int price = Convert.ToInt32(order.Price * 100);

                        var notifyURL = HttpContext.GetBaseURL() + "/Pay/WeiXinPayNotify";

                        var reqData = new
                        {
                            mchid = mchId,
                            out_trade_no = order.OrderNo,
                            appid = appId,
                            description = DateTime.UtcNow.ToString("yyyyMMddHHmm") + "交易",
                            notify_url = notifyURL,
                            amount = new
                            {
                                total = price,
                                currency = "CNY"
                            }
                        };

                        var reqDataJson = JsonHelper.ObjectToJson(reqData);

                        string wxURL = "https://api.mch.weixin.qq.com/v3/pay/transactions/native";

                        string method = "POST";
                        long timeStamp = DateTimeOffset.Now.ToUnixTimeSeconds();
                        string nonceStr = Path.GetRandomFileName();
                        string message = $"{method}\n{wxURL[29..]}\n{timeStamp}\n{nonceStr}\n{reqDataJson}\n";
                        string signature = CryptoHelper.SHA256withRSAToBase64(message, mchApiCertKey);
                        string authorization = $"WECHATPAY2-SHA256-RSA2048 mchid=\"{mchId}\",nonce_str=\"{nonceStr}\",timestamp=\"{timeStamp}\",serial_no=\"{mchApiCertId}\",signature=\"{signature}\"";

                        Dictionary<string, string> headers = new()
                        {
                            { "Accept", "*/*" },
                            { "User-Agent", ".NET HttpClient" },
                            { "Authorization", authorization }
                        };

                        var resultJson = httpClient.Post(wxURL, reqDataJson, "json", headers);

                        codeURL = JsonHelper.GetValueByKey(resultJson, "code_url");

                        if (codeURL != null)
                        {
                            distributedCache.SetString(key,codeURL);
                        }
                        else
                        {
                            string? errCode = JsonHelper.GetValueByKey(resultJson, "code");
                            string? errMessage = JsonHelper.GetValueByKey(resultJson, "message");

                            HttpContext.SetErrMsg($"{errCode}:{errMessage}");
                        }
                    }
                }
            }

            return codeURL;
        }



        /// <summary>
        /// 微信支付异步通知接口
        /// </summary>
        [HttpPost]
        public string WeiXinPayNotify()
        {
            try
            {
                WxPayData notifyData = new();
                notifyData.FromXml(HttpContext.GetRequestBody());

                //构造对微信的应答信息
                WxPayData res = new();

                if (!notifyData.IsSet("transaction_id"))
                {
                    //若transaction_id不存在，则立即返回结果给微信支付后台
                    res.SetValue("return_code", "FAIL");
                    res.SetValue("return_msg", "支付结果中微信订单号不存在");
                    return res.ToXml();
                }

                //获取订单信息
                string transaction_id = notifyData.GetValue("transaction_id")!.ToString()!; //微信流水号
                string order_no = notifyData.GetValue("out_trade_no")!.ToString()!.ToUpper(); //商户订单号
                string total_fee = notifyData.GetValue("total_fee")!.ToString()!; //获取总金额

                string appid = notifyData.GetValue("appid")!.ToString()!;

                string paytimeStr = notifyData.GetValue("time_end")!.ToString()!;
                var payTime = DateTime.ParseExact(paytimeStr, "yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture);

                //从微信验证信息真实性
                WxPayData req = new();
                req.SetValue("transaction_id", transaction_id);

                var appIdSettingGroupId = db.TAppSetting.Where(t => t.Module.StartsWith("WeiXin") && t.Key == "AppId" && t.Value == appid).Select(t => t.GroupId).FirstOrDefault();
                var settings = db.TAppSetting.Where(t => t.GroupId == appIdSettingGroupId).ToList();

                var appId = settings.Where(t => t.Key == "AppId").Select(t => t.Value).FirstOrDefault();
                var appSecret = settings.Where(t => t.Key == "AppSecret").Select(t => t.Value).FirstOrDefault();
                var mchId = settings.Where(t => t.Key == "MchId").Select(t => t.Value).FirstOrDefault();
                var mchKey = settings.Where(t => t.Key == "MchKey").Select(t => t.Value).FirstOrDefault();


                if (appId != null && appSecret != null && mchId != null && mchKey != null)
                {
                    JsApiPay jsApiPay = new(appId, mchId, mchKey);

                    WxPayData send = jsApiPay.OrderQuery(req, httpClient);
                    if (!(send.GetValue("return_code")!.ToString() == "SUCCESS" && send.GetValue("result_code")!.ToString() == "SUCCESS"))
                    {
                        //如果订单信息在微信后台不存在,立即返回失败
                        res.SetValue("return_code", "FAIL");
                        res.SetValue("return_msg", "订单查询失败");
                        return res.ToXml();
                    }
                    else
                    {

                        var order = db.TOrder.AsNoTracking().Where(t => t.OrderNo == order_no).FirstOrDefault();

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
                            order.PayPrice = decimal.Parse(total_fee) / 100;
                            order.SerialNo = transaction_id;
                            order.PayState = true;
                            order.PayTime = payTime;
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
                else
                {
                    res.SetValue("return_code", "FAIL");
                    res.SetValue("return_msg", "修改订单状态失败,内部配置丢失");
                    return res.ToXml();
                }

            }
            catch (WxPayException ex)
            {
                //若有错误，则立即返回结果给微信支付后台
                WxPayData res = new();
                res.SetValue("return_code", "FAIL");
                res.SetValue("return_msg", ex.Message);

                return res.ToXml();
            }

        }




        /// <summary>
        /// 支付宝支付-小程序模式
        /// </summary>
        /// <param name="orderNo">订单号</param>
        /// <param name="aliPayKeyId"></param>
        /// <returns></returns>
        [HttpGet]
        public DtoKeyValue? CreateAliPayMiniAPP(string orderNo, long aliPayKeyId)
        {

            var settings = db.TAppSetting.AsNoTracking().Where(t => t.Module == "AliPayMiniApp" && t.GroupId == aliPayKeyId).ToList();

            var appId = settings.Where(t => t.Key == "AppId").Select(t => t.Value).FirstOrDefault();
            var appPrivateKey = settings.Where(t => t.Key == "AppPrivateKey").Select(t => t.Value).FirstOrDefault();
            var aliPayPublicKey = settings.Where(t => t.Key == "AliPayPublicKey").Select(t => t.Value).FirstOrDefault();

            if (appId != null && appPrivateKey != null && aliPayPublicKey != null)
            {
                var order = db.TOrder.Where(t => t.OrderNo == orderNo).Select(t => new
                {
                    t.OrderNo,
                    t.Price,
                    AliPayUserId = db.TUserBindExternal.Where(a => a.UserId == t.CreateUserId && a.AppName == "AliPayMiniApp" && a.AppId == appId).Select(a => a.OpenId).FirstOrDefault(),
                    t.CreateTime
                }).FirstOrDefault();

                if (order != null && order.AliPayUserId != null)
                {
                    var notifyURL = HttpContext.GetBaseURL() + "/api/Pay/AliPayNotify";


                    string price = Convert.ToString(order.Price);


                    DefaultAopClient client = new("https://openapi.alipay.com/gateway.do", appId, appPrivateKey, "json", "1.0", "RSA2", aliPayPublicKey, "utf-8", false);

                    AlipayTradeCreateRequest request = new();

                    string orderTitle = order.CreateTime.ToString("yyyyMMddHHmm") + "交易";

                    AlipayTradeCreateModel model = new()
                    {
                        TotalAmount = price,
                        Subject = orderTitle,
                        OutTradeNo = order.OrderNo,
                        BuyerId = order.AliPayUserId
                    };

                    request.SetBizModel(model);

                    request.SetNotifyUrl(notifyURL);

                    AlipayTradeCreateResponse response = client.Execute(request);

                    string tradeNo = response.TradeNo;

                    if (string.IsNullOrEmpty(tradeNo))
                    {
                        HttpContext.SetErrMsg("支付宝交易订单创建失败");
                    }
                    else
                    {
                        DtoKeyValue keyValue = new()
                        {
                            Key = "TradeNo",
                            Value = tradeNo
                        };

                        return keyValue;
                    }
                }

            }

            return null;
        }



        /// <summary>
        /// 支付宝支付-PC模式
        /// </summary>
        /// <param name="orderNo">订单号</param>
        /// <returns>支付宝支付URL</returns>
        [HttpGet]
        public string? CreateAliPayPC(string orderNo)
        {
            var settings = db.TAppSetting.AsNoTracking().Where(t => t.Module == "AliPayWeb").ToList();

            var appId = settings.Where(t => t.Key == "AppId").Select(t => t.Value).FirstOrDefault();
            var appPrivateKey = settings.Where(t => t.Key == "AppPrivateKey").Select(t => t.Value).FirstOrDefault();
            var aliPayPublicKey = settings.Where(t => t.Key == "AliPayPublicKey").Select(t => t.Value).FirstOrDefault();


            if (appId != null && appPrivateKey != null && aliPayPublicKey != null)
            {
                var order = db.TOrder.Where(t => t.OrderNo == orderNo).Select(t => new
                {
                    t.OrderNo,
                    t.Price,
                    t.State,
                    t.CreateTime
                }).FirstOrDefault();

                if (order != null && order.State == "待支付")
                {

                    var returnURL = HttpContext.GetBaseURL();
                    var notifyURL = HttpContext.GetBaseURL() + "/Pay/AliPayNotify";

                    string price = order.Price.ToString();

                    //string gatewayUrl = "https://openapi.alipay.com/gateway.do";
                    string gatewayUrl = "https://openapi-sandbox.dl.alipaydev.com/gateway.do";

                    DefaultAopClient client = new(gatewayUrl, appId, appPrivateKey, "json", "1.0", "RSA2", aliPayPublicKey, "UTF-8", false);

                    string orderTitle = order.CreateTime.ToString("yyyyMMddHHmm") + "交易";
                    string orderDescription = "商品描述";

                    AlipayTradePagePayModel model = new()
                    {
                        Body = orderDescription,
                        Subject = orderTitle,
                        TotalAmount = price,
                        OutTradeNo = orderNo,
                        ProductCode = "FAST_INSTANT_TRADE_PAY",
                        QrPayMode = "4" //采用订单码模式会返回一个支付宝二维码
                    };

                    AlipayTradePagePayRequest request = new();

                    request.SetReturnUrl(returnURL);// 设置支付完成同步回调地址

                    request.SetNotifyUrl(notifyURL);// 设置支付完成异步通知接收地址

                    request.SetBizModel(model);// 将业务model载入到request

                    var response = client.SdkExecute(request);

                    string url = gatewayUrl + "?" + response.Body;

                    return url;
                }
            }

            return null;
        }



        /// <summary>
        /// 支付宝支付-H5模式
        /// </summary>
        /// <param name="orderNo">订单号</param>
        /// <returns>支付宝支付URL</returns>
        [HttpGet]
        public string? CreateAliPayH5(string orderNo)
        {
            var settings = db.TAppSetting.AsNoTracking().Where(t => t.Module == "AliPayWeb").ToList();

            var appId = settings.Where(t => t.Key == "AppId").Select(t => t.Value).FirstOrDefault();
            var appPrivateKey = settings.Where(t => t.Key == "AppPrivateKey").Select(t => t.Value).FirstOrDefault();
            var aliPayPublicKey = settings.Where(t => t.Key == "AliPayPublicKey").Select(t => t.Value).FirstOrDefault();

            if (appId != null && appPrivateKey != null && aliPayPublicKey != null)
            {
                var order = db.TOrder.Where(t => t.OrderNo == orderNo).Select(t => new { t.OrderNo, t.Price, t.State, t.CreateTime }).FirstOrDefault();

                if (order != null && order.State == "待支付")
                {

                    var returnURL = HttpContext.GetBaseURL();
                    var notifyURL = HttpContext.GetBaseURL() + "/Pay/AliPayNotify";

                    var quitURL = HttpContext.GetBaseURL();

                    string price = order.Price.ToString();

                    //string gatewayUrl = "https://openapi.alipay.com/gateway.do";
                    string gatewayUrl = "https://openapi-sandbox.dl.alipaydev.com/gateway.do";

                    DefaultAopClient client = new(gatewayUrl, appId, appPrivateKey, "json", "1.0", "RSA2", aliPayPublicKey, "UTF-8", false);

                    string orderTitle = order.CreateTime.ToString("yyyyMMddHHmm") + "交易";
                    string orderDescription = "商品描述";

                    AlipayTradeWapPayModel model = new()
                    {
                        OutTradeNo = orderNo,
                        Subject = orderTitle,
                        TotalAmount = price,
                        Body = orderDescription,
                        ProductCode = "QUICK_WAP_WAY",
                        QuitUrl = quitURL
                    };

                    AlipayTradeWapPayRequest request = new();

                    request.SetReturnUrl(returnURL);// 设置支付完成同步回调地址

                    request.SetNotifyUrl(notifyURL);// 设置支付完成异步通知接收地址

                    request.SetBizModel(model);// 将业务model载入到request


                    var response = client.SdkExecute(request);

                    string url = gatewayUrl + "?" + response.Body;

                    return url;
                }
            }

            return null;
        }



        /// <summary>
        /// 支付宝异步通知接口
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        public string AliPayNotify()
        {
            string retValue = "";

            //获取当前请求中的post参数
            var parameters = Request.Form.ToDictionary(t => t.Key, t => t.Value.ToString());

            if (parameters.Count > 0)
            {
                var appId = parameters.GetValueOrDefault("app_id");

                var appIdSettingGroupId = db.TAppSetting.Where(t => t.Module.StartsWith("AliPay") && t.Key == "AppId" && t.Value == appId).Select(t => t.GroupId).FirstOrDefault();

                var settings = db.TAppSetting.AsNoTracking().Where(t => t.GroupId == appIdSettingGroupId).ToList();

                var appPrivateKey = settings.Where(t => t.Key == "AppPrivateKey").Select(t => t.Value).FirstOrDefault();
                var aliPayPublicKey = settings.Where(t => t.Key == "AliPayPublicKey").Select(t => t.Value).FirstOrDefault();


                bool flag = AlipaySignature.RSACheckV1(parameters, aliPayPublicKey, "utf-8", "RSA2", false);

                if (flag)
                {
                    var orderno = parameters.GetValueOrDefault("out_trade_no");

                    var order = db.TOrder.Where(t => t.OrderNo == orderno).FirstOrDefault();

                    if (order != null)
                    {
                        order.PayPrice = decimal.Parse(parameters.GetValueOrDefault("total_amount")!);
                        order.SerialNo = parameters.GetValueOrDefault("trade_no");
                        order.PayState = true;
                        order.PayTime = Convert.ToDateTime(parameters.GetValueOrDefault("gmt_payment")).ToUniversalTime();
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




        /// <summary>
        /// 支付宝退款
        /// </summary>
        /// <returns></returns>
        private void AliPayRefund()
        {

            var settings = db.TAppSetting.AsNoTracking().Where(t => t.Module == "AliPayWeb").ToList();

            var appId = settings.Where(t => t.Key == "AppId").Select(t => t.Value).FirstOrDefault();
            var appPrivateKey = settings.Where(t => t.Key == "AppPrivateKey").Select(t => t.Value).FirstOrDefault();
            var aliPayPublicKey = settings.Where(t => t.Key == "AliPayPublicKey").Select(t => t.Value).FirstOrDefault();

            string gatewayUrl = "https://openapi.alipay.com/gateway.do";
            //string gatewayUrl = "https://openapi-sandbox.dl.alipaydev.com/gateway.do";

            DefaultAopClient client = new(gatewayUrl, appId, appPrivateKey, "json", "1.0", "RSA2", aliPayPublicKey, "UTF-8", false);
            AlipayTradeRefundRequest request = new();

            AlipayTradeRefundModel model = new()
            {
                TradeNo = "支付宝交易流水号",
                RefundAmount = "退款金额 05.00",
                OutRequestNo = "退款单识别号"
            };

            request.SetBizModel(model);

            AlipayTradeRefundResponse response = client.Execute(request);

            bool isError = response.IsError;

            if (isError)
            {
                string errMsg = response.SubMsg;
            }

        }

    }
}