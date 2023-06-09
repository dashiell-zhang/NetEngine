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
using WebAPI.Models.Pay;
using WebAPI.Models.Shared;
using WebAPI.Services;

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
        private readonly ILogger logger;
        private readonly PayService payService;



        public PayController(DatabaseContext db, IDistributedCache distributedCache, IHttpClientFactory httpClientFactory, ILogger<PayController> logger, PayService payService)
        {
            this.db = db;
            this.distributedCache = distributedCache;
            httpClient = httpClientFactory.CreateClient();
            this.logger = logger;
            this.payService = payService;
        }





        /// <summary>
        /// 微信支付-JSAPI模式
        /// </summary>
        /// <param name="orderNo">订单号</param>
        /// <param name="openId">用户OpenId</param>
        /// <returns></returns>
        [HttpGet]
        public DtoCreateWeiXinPayJSAPIRet? CreateWeiXinPayJSAPI(string orderNo, string openId)
        {
            string key = "wxpayJSAPI" + orderNo;

            var ret = distributedCache.Get<DtoCreateWeiXinPayJSAPIRet>(key);

            if (ret == null)
            {
                var order = db.TOrder.AsNoTracking().Where(t => t.OrderNo == orderNo).Select(t => new { t.Id, t.OrderNo, t.Price }).FirstOrDefault();

                if (order != null)
                {
                    var settings = db.TAppSetting.AsNoTracking().Where(t => t.Module == "WeiXinPay").ToList();

                    var appId = settings.Where(t => t.Key == "AppId").Select(t => t.Value).FirstOrDefault();
                    var mchId = settings.Where(t => t.Key == "MchId").Select(t => t.Value).FirstOrDefault();
                    var mchApiCertKey = settings.Where(t => t.Key == "MchApiCertKey").Select(t => t.Value).FirstOrDefault();

                    if (appId != null && mchId != null && order != null && mchApiCertKey != null)
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
                            payer = new
                            {
                                openid = openId
                            }
                        };

                        string wxURL = "https://api.mch.weixin.qq.com/v3/pay/transactions/jsapi";

                        var resultJson = payService.WeiXinPayHttp(mchId, wxURL, reqData);

                        var prepayId = JsonHelper.GetValueByKey(resultJson, "prepay_id");

                        if (prepayId == null)
                        {
                            string? errCode = JsonHelper.GetValueByKey(resultJson, "code");
                            string? errMessage = JsonHelper.GetValueByKey(resultJson, "message");

                            HttpContext.SetErrMsg($"{errCode}:{errMessage}");
                        }
                        else
                        {

                            string package = "prepay_id=" + prepayId;

                            var timeStamp = DateTimeOffset.Now.ToUnixTimeSeconds();
                            var nonceStr = Path.GetRandomFileName();
                            var message = $"{appId}\n{timeStamp}\n{nonceStr}\n{package}\n";
                            var signature = CryptoHelper.GetSHA256withRSA(message, mchApiCertKey, "base64");

                            ret = new()
                            {
                                AppId = appId,
                                Package = package,
                                NonceStr = nonceStr,
                                TimeStamp = timeStamp,
                                Sign = signature
                            };

                            distributedCache.Set(key, ret, TimeSpan.FromHours(1.8));
                        }
                    }
                }
            }

            return ret;
        }



        /// <summary>
        /// 微信支付-APP模式
        /// </summary>
        /// <param name="orderNo">订单号</param>
        /// <returns></returns>
        [HttpGet]
        public DtoCreateWeiXinPayAPPRet? CreateWeiXinPayAPP(string orderNo)
        {
            string key = "wxpayAPP" + orderNo;

            var ret = distributedCache.Get<DtoCreateWeiXinPayAPPRet>(key);

            if (ret == null)
            {
                var order = db.TOrder.AsNoTracking().Where(t => t.OrderNo == orderNo).Select(t => new { t.Id, t.OrderNo, t.Price }).FirstOrDefault();

                if (order != null)
                {
                    var settings = db.TAppSetting.AsNoTracking().Where(t => t.Module == "WeiXinPay").ToList();

                    var appId = settings.Where(t => t.Key == "AppId").Select(t => t.Value).FirstOrDefault();
                    var mchId = settings.Where(t => t.Key == "MchId").Select(t => t.Value).FirstOrDefault();
                    var mchApiCertKey = settings.Where(t => t.Key == "MchApiCertKey").Select(t => t.Value).FirstOrDefault();

                    if (appId != null && mchId != null && mchApiCertKey != null && order != null)
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


                        string wxURL = "https://api.mch.weixin.qq.com/v3/pay/transactions/app";

                        var resultJson = payService.WeiXinPayHttp(mchId, wxURL, reqData);

                        var prepayId = JsonHelper.GetValueByKey(resultJson, "prepay_id");

                        if (prepayId == null)
                        {
                            string? errCode = JsonHelper.GetValueByKey(resultJson, "code");
                            string? errMessage = JsonHelper.GetValueByKey(resultJson, "message");

                            HttpContext.SetErrMsg($"{errCode}:{errMessage}");
                        }
                        else
                        {
                            var timeStamp = DateTimeOffset.Now.ToUnixTimeSeconds();
                            var nonceStr = Path.GetRandomFileName();
                            var message = $"{appId}\n{timeStamp}\n{nonceStr}\n{prepayId}\n";
                            var signature = CryptoHelper.GetSHA256withRSA(message, mchApiCertKey, "base64");

                            ret = new()
                            {
                                AppId = appId,
                                PartnerId = mchId,
                                PrepayId = prepayId,
                                NonceStr = nonceStr,
                                TimeStamp = timeStamp,
                                Sign = signature
                            };

                            distributedCache.Set(key, ret, TimeSpan.FromHours(1.8));
                        }
                    }
                }
            }

            return ret;
        }



        /// <summary>
        /// 微信支付-H5模式
        /// </summary>
        /// <param name="orderNo">订单号</param>
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

                    if (appId != null && mchId != null && order != null)
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

                        string wxURL = "https://api.mch.weixin.qq.com/v3/pay/transactions/h5";

                        var resultJson = payService.WeiXinPayHttp(mchId, wxURL, reqData);

                        h5URL = JsonHelper.GetValueByKey(resultJson, "h5_url");

                        if (h5URL == null)
                        {
                            string? errCode = JsonHelper.GetValueByKey(resultJson, "code");
                            string? errMessage = JsonHelper.GetValueByKey(resultJson, "message");

                            HttpContext.SetErrMsg($"{errCode}:{errMessage}");
                        }
                        else
                        {
                            distributedCache.Set(key, h5URL, TimeSpan.FromMinutes(4.5));
                        }
                    }
                }
            }

            return h5URL;
        }



        /// <summary>
        /// 微信支付-PC模式
        /// </summary>
        /// <param name="orderNo">订单号</param>
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

                    if (appId != null && mchId != null && order != null)
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

                        string wxURL = "https://api.mch.weixin.qq.com/v3/pay/transactions/native";

                        var resultJson = payService.WeiXinPayHttp(mchId, wxURL, reqData);

                        codeURL = JsonHelper.GetValueByKey(resultJson, "code_url");

                        if (codeURL != null)
                        {
                            distributedCache.Set(key, codeURL, TimeSpan.FromMinutes(15));
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
        public DtoWeiXinPayNotifyRet? WeiXinPayNotify(DtoWeiXinPayNotify weiXinPayNotify)
        {
            int statusCode = 501;

            try
            {
                //支付成功异步回调
                if (weiXinPayNotify.event_type == "TRANSACTION.SUCCESS")
                {
                    var mchApiV3Key = db.TAppSetting.Where(t => t.Module == "WeiXinPay" && t.Key == "MchApiV3Key").Select(t => t.Value).First();

                    var resourceJson = CryptoHelper.AesGcmDecrypt(weiXinPayNotify.resource.ciphertext, mchApiV3Key, weiXinPayNotify.resource.nonce, weiXinPayNotify.resource.associated_data, "base64");

                    var resource = JsonHelper.JsonToObject<DtoWeiXinPayNotifyTransaction>(resourceJson);

                    if (resource.trade_state == "SUCCESS")
                    {
                        var order = db.TOrder.Where(t => t.OrderNo == resource.out_trade_no).FirstOrDefault();

                        if (order != null)
                        {
                            if (order.PayState == false)
                            {
                                order.PayPrice = Convert.ToDecimal(resource.amount.total) / 100;
                                order.SerialNo = resource.transaction_id;
                                order.PayState = true;
                                order.PayTime = resource.success_time.ToUniversalTime();
                                order.PayType = "微信支付";
                                order.State = "已支付";
                                order.UpdateTime = DateTime.UtcNow;

                                db.SaveChanges();

                                if (order.Type == "")
                                {
                                    //执行业务处理逻辑

                                }
                            }

                            statusCode = 200;
                        }
                    }
                }

                //退款异步回调
                if (weiXinPayNotify.resource.original_type == "refund")
                {
                    var mchApiV3Key = db.TAppSetting.Where(t => t.Module == "WeiXinPay" && t.Key == "MchApiV3Key").Select(t => t.Value).First();

                    var resourceJson = CryptoHelper.AesGcmDecrypt(weiXinPayNotify.resource.ciphertext, mchApiV3Key, weiXinPayNotify.resource.nonce, weiXinPayNotify.resource.associated_data, "base64");

                    var resource = JsonHelper.JsonToObject<DtoWeiXinPayNotifyRefund>(resourceJson);

                    if (resource.refund_status == "SUCCESS")
                    {
                        //退款成功

                    }
                    else
                    {
                        //退款失败

                    }

                    statusCode = 200;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "WeiXinPayNotify");
            }


            if (statusCode != 200)
            {
                HttpContext.Response.StatusCode = statusCode;

                DtoWeiXinPayNotifyRet retValue = new()
                {
                    code = "FAIL",
                    message = "失败"
                };

                return retValue;
            }
            else
            {
                DtoWeiXinPayNotifyRet retValue = new()
                {
                    code = "SUCCESS"
                };

                return retValue;
            }
        }




        /// <summary>
        /// 微信支付退款状态查询
        /// </summary>
        private void WeiXinPayRefundSelect()
        {
            var settings = db.TAppSetting.AsNoTracking().Where(t => t.Module == "WeiXinPay").ToList();

            var mchId = settings.Where(t => t.Key == "MchId").Select(t => t.Value).FirstOrDefault();

            if (mchId != null)
            {

                string outRefundNo = "";    //商户退款单号

                string wxURL = "https://api.mch.weixin.qq.com/v3/refund/domestic/refunds/" + outRefundNo;

                var resultJson = payService.WeiXinPayHttp(mchId, wxURL);

                var result = JsonHelper.JsonToObject<DtoWeiXinPayRefundRet>(resultJson);

                if (result.status == "SUCCESS")
                {
                    //退款成功
                }
                else
                {
                    //退款失败
                }

            }
        }



        /// <summary>
        /// 微信支付退款
        /// </summary>
        private void WeiXinPayRefund()
        {
            var settings = db.TAppSetting.AsNoTracking().Where(t => t.Module == "WeiXinPay").ToList();

            var appId = settings.Where(t => t.Key == "AppId").Select(t => t.Value).FirstOrDefault();
            var mchId = settings.Where(t => t.Key == "MchId").Select(t => t.Value).FirstOrDefault();
            var mchApiCertId = settings.Where(t => t.Key == "MchApiCertId").Select(t => t.Value).FirstOrDefault();
            var mchApiCertKey = settings.Where(t => t.Key == "MchApiCertKey").Select(t => t.Value).FirstOrDefault();

            if (appId != null && mchId != null && mchApiCertId != null && mchApiCertKey != null)
            {

                var notifyURL = HttpContext.GetBaseURL() + "/Pay/WeiXinPayNotify";

                var reqData = new
                {
                    out_refund_no = "", //退款单号
                    transaction_id = "",    //微信交易流水号
                    notify_url = notifyURL,
                    amount = new
                    {
                        refund = 1, //退款金额
                        total = 2, //原订单金额
                        currency = "CNY"
                    }
                };

                var reqDataJson = JsonHelper.ObjectToJson(reqData);

                string wxURL = "https://api.mch.weixin.qq.com/v3/refund/domestic/refunds";

                string method = "POST";
                long timeStamp = DateTimeOffset.Now.ToUnixTimeSeconds();
                string nonceStr = Path.GetRandomFileName();
                string message = $"{method}\n{wxURL[29..]}\n{timeStamp}\n{nonceStr}\n{reqDataJson}\n";
                string signature = CryptoHelper.GetSHA256withRSA(message, mchApiCertKey, "base64");
                string authorization = $"WECHATPAY2-SHA256-RSA2048 mchid=\"{mchId}\",nonce_str=\"{nonceStr}\",timestamp=\"{timeStamp}\",serial_no=\"{mchApiCertId}\",signature=\"{signature}\"";

                Dictionary<string, string> headers = new()
                        {
                            { "Accept", "*/*" },
                            { "User-Agent", ".NET HttpClient" },
                            { "Authorization", authorization }
                        };

                var resultJson = httpClient.Post(wxURL, reqDataJson, "json", headers);


                var refundId = JsonHelper.GetValueByKey(resultJson, "refund_id");

                if (!string.IsNullOrEmpty(refundId))
                {
                    //refundId 有值则说明退款请求发起成功，至于退款结果交由异步通知接口或者主动查询退款方法处理

                    var result = JsonHelper.JsonToObject<DtoWeiXinPayRefundRet>(resultJson);

                }
                else
                {
                    string? errCode = JsonHelper.GetValueByKey(resultJson, "code");
                    string? errMessage = JsonHelper.GetValueByKey(resultJson, "message");

                    HttpContext.SetErrMsg($"{errCode}:{errMessage}");
                }

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


            if (response.IsError)
            {
                string errMsg = response.SubMsg;
            }
            else
            {
                if (response.FundChange == "Y")
                {
                    //则说明退款成功
                }
                else
                {
                    //为N的情况，退款不一定失败，同一个请求第一次是Y第二次是N，该标记只标识本次请求是否有金额变动，为N时请主动调用退款查询接口
                }
            }

        }

    }
}