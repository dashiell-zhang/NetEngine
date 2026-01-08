using Aop.Api;
using Aop.Api.Domain;
using Aop.Api.Request;
using Aop.Api.Response;
using Aop.Api.Util;
using Application.Model.Pay;
using Common;
using DistributedLock;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Repository.Database;
using SourceGenerator.Runtime.Attributes;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Application.Service;
[RegisterService(Lifetime = ServiceLifetime.Scoped)]
public class PayService(ILogger<PayService> logger, IHttpClientFactory httpClientFactory, DatabaseContext db, IDistributedCache distributedCache, IDistributedLock distributedLock)
{

    private readonly HttpClient httpClient = httpClientFactory.CreateClient();


    /// <summary>
    /// 微信支付-JSAPI模式
    /// </summary>
    /// <param name="orderNo">订单号</param>
    /// <param name="openId">用户OpenId</param>
    /// <param name="notifyUrl">异步回调Url</param>
    /// <returns></returns>
    public async Task<DtoCreateWeiXinPayJSAPIRet?> CreateWeiXinPayJSAPIAsync(string orderNo, string openId, string notifyUrl)
    {
        string key = "wxpayJSAPI" + orderNo;

        var ret = await distributedCache.GetAsync<DtoCreateWeiXinPayJSAPIRet>(key);

        if (ret == null)
        {
            var order = await db.Order.Where(t => t.OrderNo == orderNo).Select(t => new { t.Id, t.OrderNo, t.Price }).FirstOrDefaultAsync();

            if (order != null)
            {
                var settings = await db.AppSetting.AsNoTracking().Where(t => t.Module == "WeiXinPay").ToListAsync();

                var appId = settings.Where(t => t.Key == "AppId").Select(t => t.Value).FirstOrDefault();
                var mchId = settings.Where(t => t.Key == "MchId").Select(t => t.Value).FirstOrDefault();
                var mchApiCertKey = settings.Where(t => t.Key == "MchApiCertKey").Select(t => t.Value).FirstOrDefault();

                if (appId != null && mchId != null && order != null && mchApiCertKey != null)
                {
                    int price = Convert.ToInt32(order.Price * 100);

                    notifyUrl += mchId;

                    var reqData = new
                    {
                        mchid = mchId,
                        out_trade_no = order.OrderNo,
                        appid = appId,
                        description = DateTime.UtcNow.ToString("yyyyMMddHHmm") + "交易",
                        notify_url = notifyUrl,
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

                    string wxUrl = "https://api.mch.weixin.qq.com/v3/pay/transactions/jsapi";

                    var resultJson = await WeiXinPayHttpAsync(mchId, wxUrl, reqData);

                    var prepayId = JsonHelper.GetValueByKey(resultJson, "prepay_id");

                    if (prepayId == null)
                    {
                        string? errCode = JsonHelper.GetValueByKey(resultJson, "code");
                        string? errMessage = JsonHelper.GetValueByKey(resultJson, "message");

                        throw new CustomException($"{errCode}:{errMessage}");
                    }
                    else
                    {

                        string package = "prepay_id=" + prepayId;

                        var timeStamp = DateTimeOffset.Now.ToUnixTimeSeconds();
                        var nonceStr = Path.GetRandomFileName();
                        var message = $"{appId}\n{timeStamp}\n{nonceStr}\n{package}\n";
                        var signature = CryptoHelper.RSASignData(mchApiCertKey, message, "base64", HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

                        ret = new()
                        {
                            AppId = appId,
                            Package = package,
                            NonceStr = nonceStr,
                            TimeStamp = timeStamp,
                            Sign = signature
                        };

                        await distributedCache.SetAsync(key, ret, TimeSpan.FromHours(1.8));
                    }
                }
            }
        }

        return ret;
    }


    /// <summary>
    /// 微信支付-App模式
    /// </summary>
    /// <param name="orderNo">订单号</param>
    /// <param name="notifyUrl">异步回调Url</param>
    /// <returns></returns>
    public async Task<DtoCreateWeiXinPayAppRet?> CreateWeiXinPayAppAsync(string orderNo, string notifyUrl)
    {
        string key = "wxpayApp" + orderNo;

        var ret = await distributedCache.GetAsync<DtoCreateWeiXinPayAppRet>(key);

        if (ret == null)
        {
            var order = await db.Order.AsNoTracking().Where(t => t.OrderNo == orderNo).Select(t => new { t.Id, t.OrderNo, t.Price }).FirstOrDefaultAsync();

            if (order != null)
            {
                var settings = await db.AppSetting.AsNoTracking().Where(t => t.Module == "WeiXinPay").ToListAsync();

                var appId = settings.Where(t => t.Key == "AppId").Select(t => t.Value).FirstOrDefault();
                var mchId = settings.Where(t => t.Key == "MchId").Select(t => t.Value).FirstOrDefault();
                var mchApiCertKey = settings.Where(t => t.Key == "MchApiCertKey").Select(t => t.Value).FirstOrDefault();

                if (appId != null && mchId != null && mchApiCertKey != null && order != null)
                {
                    int price = Convert.ToInt32(order.Price * 100);

                    notifyUrl += mchId;

                    var reqData = new
                    {
                        mchid = mchId,
                        out_trade_no = order.OrderNo,
                        appid = appId,
                        description = DateTime.UtcNow.ToString("yyyyMMddHHmm") + "交易",
                        notify_url = notifyUrl,
                        amount = new
                        {
                            total = price,
                            currency = "CNY"
                        }
                    };


                    string wxUrl = "https://api.mch.weixin.qq.com/v3/pay/transactions/app";

                    var resultJson = await WeiXinPayHttpAsync(mchId, wxUrl, reqData);

                    var prepayId = JsonHelper.GetValueByKey(resultJson, "prepay_id");

                    if (prepayId == null)
                    {
                        string? errCode = JsonHelper.GetValueByKey(resultJson, "code");
                        string? errMessage = JsonHelper.GetValueByKey(resultJson, "message");

                        throw new CustomException($"{errCode}:{errMessage}");
                    }
                    else
                    {
                        var timeStamp = DateTimeOffset.Now.ToUnixTimeSeconds();
                        var nonceStr = Path.GetRandomFileName();
                        var message = $"{appId}\n{timeStamp}\n{nonceStr}\n{prepayId}\n";
                        var signature = CryptoHelper.RSASignData(mchApiCertKey, message, "base64", HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

                        ret = new()
                        {
                            AppId = appId,
                            PartnerId = mchId,
                            PrepayId = prepayId,
                            NonceStr = nonceStr,
                            TimeStamp = timeStamp,
                            Sign = signature
                        };

                        await distributedCache.SetAsync(key, ret, TimeSpan.FromHours(1.8));
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
    /// <param name="notifyUrl">异步回调Url</param>
    /// <param name="clientIP">客户端ip</param>
    /// <returns></returns>
    public async Task<string?> CreateWeiXinPayH5Async(string orderNo, string notifyUrl, string clientIP)
    {
        string key = "wxpayH5Url" + orderNo;

        string? h5Url = await distributedCache.GetStringAsync(key);

        if (string.IsNullOrEmpty(h5Url))
        {
            var order = await db.Order.AsNoTracking().Where(t => t.OrderNo == orderNo).Select(t => new { t.Id, t.OrderNo, t.Price }).FirstOrDefaultAsync();

            if (order != null)
            {
                var settings = await db.AppSetting.AsNoTracking().Where(t => t.Module == "WeiXinPay").ToListAsync();

                var appId = settings.Where(t => t.Key == "AppId").Select(t => t.Value).FirstOrDefault();
                var mchId = settings.Where(t => t.Key == "MchId").Select(t => t.Value).FirstOrDefault();

                if (appId != null && mchId != null && order != null)
                {
                    int price = Convert.ToInt32(order.Price * 100);

                    notifyUrl += mchId;

                    var reqData = new
                    {
                        mchid = mchId,
                        out_trade_no = order.OrderNo,
                        appid = appId,
                        description = DateTime.UtcNow.ToString("yyyyMMddHHmm") + "交易",
                        notify_url = notifyUrl,
                        amount = new
                        {
                            total = price,
                            currency = "CNY"
                        },
                        scene_info = new
                        {
                            payer_client_ip = clientIP,
                            h5_info = new
                            {
                                type = "Wap"
                            }
                        }
                    };

                    string wxUrl = "https://api.mch.weixin.qq.com/v3/pay/transactions/h5";

                    var resultJson = await WeiXinPayHttpAsync(mchId, wxUrl, reqData);

                    h5Url = JsonHelper.GetValueByKey(resultJson, "h5_url");

                    if (h5Url == null)
                    {
                        string? errCode = JsonHelper.GetValueByKey(resultJson, "code");
                        string? errMessage = JsonHelper.GetValueByKey(resultJson, "message");

                        throw new CustomException($"{errCode}:{errMessage}");
                    }
                    else
                    {
                        await distributedCache.SetAsync(key, h5Url, TimeSpan.FromMinutes(4.5));
                    }
                }
            }
        }

        return h5Url;
    }


    /// <summary>
    /// 微信支付-PC模式
    /// </summary>
    /// <param name="orderNo">订单号</param>
    /// <param name="notifyUrl">异步回调Url</param>
    /// <returns></returns>
    public async Task<string?> CreateWeiXinPayPCAsync(string orderNo, string notifyUrl)
    {
        string key = "wxpayPCUrl" + orderNo;

        string? codeUrl = await distributedCache.GetStringAsync(key);

        if (string.IsNullOrEmpty(codeUrl))
        {
            var order = await db.Order.Where(t => t.OrderNo == orderNo).Select(t => new { t.Id, t.OrderNo, t.Price }).FirstOrDefaultAsync();

            if (order != null)
            {
                var settings = await db.AppSetting.AsNoTracking().Where(t => t.Module == "WeiXinPay").ToListAsync();

                var appId = settings.Where(t => t.Key == "AppId").Select(t => t.Value).FirstOrDefault();
                var mchId = settings.Where(t => t.Key == "MchId").Select(t => t.Value).FirstOrDefault();

                if (appId != null && mchId != null && order != null)
                {
                    int price = Convert.ToInt32(order.Price * 100);

                    notifyUrl += mchId;

                    var reqData = new
                    {
                        mchid = mchId,
                        out_trade_no = order.OrderNo,
                        appid = appId,
                        description = DateTime.UtcNow.ToString("yyyyMMddHHmm") + "交易",
                        notify_url = notifyUrl,
                        amount = new
                        {
                            total = price,
                            currency = "CNY"
                        }
                    };

                    string wxUrl = "https://api.mch.weixin.qq.com/v3/pay/transactions/native";

                    var resultJson = await WeiXinPayHttpAsync(mchId, wxUrl, reqData);

                    codeUrl = JsonHelper.GetValueByKey(resultJson, "code_url");

                    if (codeUrl != null)
                    {
                        await distributedCache.SetAsync(key, codeUrl, TimeSpan.FromMinutes(15));
                    }
                    else
                    {
                        string? errCode = JsonHelper.GetValueByKey(resultJson, "code");
                        string? errMessage = JsonHelper.GetValueByKey(resultJson, "message");

                        throw new CustomException($"{errCode}:{errMessage}");
                    }
                }
            }
        }

        return codeUrl;
    }


    /// <summary>
    /// 微信支付异步通知接口
    /// </summary>
    /// <param name="mchId">商户Id</param>
    /// <param name="weiXinPayNotify"></param>
    /// <param name="headers"HttpHeader></param>
    /// <param name="requestBody">bodyjson数据</param>
    /// <returns></returns>
    public async Task<DtoWeiXinPayNotifyRet?> WeiXinPayNotifyAsync(string mchId, DtoWeiXinPayNotify weiXinPayNotify, Dictionary<string, string> headers, string requestBody)
    {
        bool isSuccess = false;

        try
        {

            var weiXinPayCertificates = await GetWeiXinPayCertificatesAsync(mchId);

            if (VerifySign(headers, requestBody, weiXinPayCertificates))
            {

                //支付成功异步回调
                if (weiXinPayNotify.event_type == "TRANSACTION.SUCCESS")
                {
                    var mchApiV3Key = await db.AppSetting.Where(t => t.Module == "WeiXinPay" && t.Key == "MchApiV3Key").Select(t => t.Value).FirstAsync();

                    var resourceJson = CryptoHelper.AesGcmDecrypt(weiXinPayNotify.resource.ciphertext, mchApiV3Key, weiXinPayNotify.resource.nonce, weiXinPayNotify.resource.associated_data, "base64");

                    var resource = JsonHelper.JsonToObject<DtoWeiXinPayNotifyTransaction>(resourceJson);

                    if (resource.trade_state == "SUCCESS")
                    {
                        var order = await db.Order.Where(t => t.OrderNo == resource.out_trade_no).FirstOrDefaultAsync();

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

                                await db.SaveChangesAsync();

                                if (order.Type == "")
                                {
                                    //执行业务处理逻辑

                                }
                            }

                            isSuccess = true;
                        }
                    }
                }


                //退款异步回调
                if (weiXinPayNotify.resource.original_type == "refund")
                {
                    var mchApiV3Key = await db.AppSetting.Where(t => t.Module == "WeiXinPay" && t.Key == "MchApiV3Key").Select(t => t.Value).FirstAsync();

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

                    isSuccess = true;
                }

            }
            else
            {
                throw new Exception("签名计算失败");
            }
        }
        catch (Exception ex)
        {
            var content = new
            {
                mchId,
                requestBody,
                error = new
                {
                    ex?.Source,
                    ex?.Message,
                    ex?.StackTrace
                }
            };

            logger.LogError("WeiXinPayNotify：{content}", JsonHelper.ObjectToJson(content));
        }

        DtoWeiXinPayNotifyRet retValue = new();

        if (!isSuccess)
        {
            retValue.code = "FAIL";
            retValue.message = "失败";
        }
        else
        {
            retValue.code = "SUCCESS";
        }

        return retValue;
    }


    /// <summary>
    /// 微信支付退款
    /// </summary>
    public async Task WeiXinPayRefundAsync()
    {
        var settings = await db.AppSetting.AsNoTracking().Where(t => t.Module == "WeiXinPay").ToListAsync();

        var appId = settings.Where(t => t.Key == "AppId").Select(t => t.Value).FirstOrDefault();
        var mchId = settings.Where(t => t.Key == "MchId").Select(t => t.Value).FirstOrDefault();
        var mchApiCertId = settings.Where(t => t.Key == "MchApiCertId").Select(t => t.Value).FirstOrDefault();
        var mchApiCertKey = settings.Where(t => t.Key == "MchApiCertKey").Select(t => t.Value).FirstOrDefault();

        if (appId != null && mchId != null && mchApiCertId != null && mchApiCertKey != null)
        {

            //var notifyUrl = httpContextAccessor.HttpContext!.GetBaseUrl() + "/Pay/WeiXinPayNotify/" + mchId;

            string notifyUrl = "";

            var reqData = new
            {
                out_refund_no = "", //退款单号
                transaction_id = "",    //微信交易流水号
                notify_url = notifyUrl,
                amount = new
                {
                    refund = 1, //退款金额
                    total = 1, //原订单金额
                    currency = "CNY"
                }
            };

            string wxUrl = "https://api.mch.weixin.qq.com/v3/refund/domestic/refunds";

            var resultJson = await WeiXinPayHttpAsync(mchId, wxUrl, reqData);

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

                throw new CustomException($"{errCode}:{errMessage}");
            }

        }
    }


    /// <summary>
    /// 微信支付退款状态查询
    /// </summary>
    public async Task WeiXinPayRefundSelectAsync()
    {
        var settings = await db.AppSetting.AsNoTracking().Where(t => t.Module == "WeiXinPay").ToListAsync();

        var mchId = settings.Where(t => t.Key == "MchId").Select(t => t.Value).FirstOrDefault();

        if (mchId != null)
        {

            string outRefundNo = "";    //商户退款单号

            string wxUrl = "https://api.mch.weixin.qq.com/v3/refund/domestic/refunds/" + outRefundNo;

            var resultJson = await WeiXinPayHttpAsync(mchId, wxUrl);

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
    /// 支付宝支付-小程序模式
    /// </summary>
    /// <param name="orderNo">订单号</param>
    /// <returns>TradeNo</returns>
    public async Task<string?> CreateAliPayMiniAppAsync(string orderNo, string notifyUrl)
    {
        var settings = await db.AppSetting.AsNoTracking().Where(t => t.Module == "AliPayMiniApp").ToListAsync();

        var appId = settings.Where(t => t.Key == "AppId").Select(t => t.Value).FirstOrDefault();
        var appPrivateKey = settings.Where(t => t.Key == "AppPrivateKey").Select(t => t.Value).FirstOrDefault();
        var aliPayPublicKey = settings.Where(t => t.Key == "AliPayPublicKey").Select(t => t.Value).FirstOrDefault();

        if (appId != null && appPrivateKey != null && aliPayPublicKey != null)
        {
            var order = await db.Order.Where(t => t.OrderNo == orderNo).Select(t => new
            {
                t.OrderNo,
                t.Price,
                AliPayUserId = db.UserBindExternal.Where(a => a.UserId == t.CreateUserId && a.AppName == "AliPayMiniApp" && a.AppId == appId).Select(a => a.OpenId).FirstOrDefault(),
                t.CreateTime
            }).FirstOrDefaultAsync();

            if (order != null && order.AliPayUserId != null)
            {

                string price = Convert.ToString(order.Price);

                string gatewayUrl = "https://openapi.alipay.com/gateway.do";

                DefaultAopClient client = new(gatewayUrl, appId, appPrivateKey, "json", "1.0", "RSA2", aliPayPublicKey, "utf-8", false);

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

                request.SetNotifyUrl(notifyUrl);

                AlipayTradeCreateResponse response = client.Execute(request);

                string tradeNo = response.TradeNo;

                if (!string.IsNullOrEmpty(tradeNo))
                {
                    throw new CustomException("支付宝交易订单创建失败");
                }
                else
                {
                    return tradeNo;
                }
            }

        }

        return null;
    }


    /// <summary>
    /// 支付宝支付-PC模式
    /// </summary>
    /// <param name="orderNo">订单号</param>
    /// <param name="notifyUrl">异步返回Url</param>
    /// <param name="returnUrl">同步返回Url(二维码模式可为null)</param>
    /// <returns>支付宝支付Url</returns>
    public async Task<string?> CreateAliPayPCAsync(string orderNo, string notifyUrl, string? returnUrl)
    {
        var settings = await db.AppSetting.AsNoTracking().Where(t => t.Module == "AliPayWeb").ToListAsync();

        var appId = settings.Where(t => t.Key == "AppId").Select(t => t.Value).FirstOrDefault();
        var appPrivateKey = settings.Where(t => t.Key == "AppPrivateKey").Select(t => t.Value).FirstOrDefault();
        var aliPayPublicKey = settings.Where(t => t.Key == "AliPayPublicKey").Select(t => t.Value).FirstOrDefault();

        if (appId != null && appPrivateKey != null && aliPayPublicKey != null)
        {
            var order = await db.Order.Where(t => t.OrderNo == orderNo).Select(t => new
            {
                t.OrderNo,
                t.Price,
                t.State,
                t.CreateTime
            }).FirstOrDefaultAsync();

            if (order != null && order.State == "待支付")
            {

                string price = order.Price.ToString();

                string gatewayUrl = "https://openapi.alipay.com/gateway.do";

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

                request.SetReturnUrl(returnUrl);// 设置支付完成同步回调地址

                request.SetNotifyUrl(notifyUrl);// 设置支付完成异步通知接收地址

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
    /// <param name="notifyUrl"></param>
    /// <param name="returnUrl"></param>
    /// <param name="quitUrl"></param>
    /// <returns>支付宝支付Url</returns>
    public async Task<string?> CreateAliPayH5Async(string orderNo, string notifyUrl, string returnUrl, string quitUrl)
    {
        var settings = await db.AppSetting.AsNoTracking().Where(t => t.Module == "AliPayWeb").ToListAsync();

        var appId = settings.Where(t => t.Key == "AppId").Select(t => t.Value).FirstOrDefault();
        var appPrivateKey = settings.Where(t => t.Key == "AppPrivateKey").Select(t => t.Value).FirstOrDefault();
        var aliPayPublicKey = settings.Where(t => t.Key == "AliPayPublicKey").Select(t => t.Value).FirstOrDefault();

        if (appId != null && appPrivateKey != null && aliPayPublicKey != null)
        {
            var order = await db.Order.Where(t => t.OrderNo == orderNo).Select(t => new { t.OrderNo, t.Price, t.State, t.CreateTime }).FirstOrDefaultAsync();

            if (order != null && order.State == "待支付")
            {

                string price = order.Price.ToString();

                string gatewayUrl = "https://openapi.alipay.com/gateway.do";

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
                    QuitUrl = quitUrl
                };

                AlipayTradeWapPayRequest request = new();

                request.SetReturnUrl(returnUrl);// 设置支付完成同步回调地址

                request.SetNotifyUrl(notifyUrl);// 设置支付完成异步通知接收地址

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
    /// <param name="parameters">支付宝回调表单参数</param>
    /// <returns></returns>
    public async Task<string?> AliPayNotifyAsync(Dictionary<string, string> parameters)
    {

        if (parameters.Count > 0)
        {
            var appId = parameters.GetValueOrDefault("app_id");

            var appIdSettingGroupId = await db.AppSetting.Where(t => t.Module.StartsWith("AliPay") && t.Key == "AppId" && t.Value == appId).Select(t => t.GroupId).FirstOrDefaultAsync();

            var settings = await db.AppSetting.AsNoTracking().Where(t => t.GroupId == appIdSettingGroupId).ToListAsync();

            var appPrivateKey = settings.Where(t => t.Key == "AppPrivateKey").Select(t => t.Value).FirstOrDefault();
            var aliPayPublicKey = settings.Where(t => t.Key == "AliPayPublicKey").Select(t => t.Value).FirstOrDefault();


            bool flag = AlipaySignature.RSACheckV1(parameters, aliPayPublicKey, "utf-8", "RSA2", false);

            if (flag)
            {
                var orderno = parameters.GetValueOrDefault("out_trade_no");

                var order = await db.Order.Where(t => t.OrderNo == orderno).FirstOrDefaultAsync();

                if (order != null)
                {
                    order.PayPrice = decimal.Parse(parameters.GetValueOrDefault("total_amount")!);
                    order.SerialNo = parameters.GetValueOrDefault("trade_no");
                    order.PayState = true;
                    order.PayTime = Convert.ToDateTime(parameters.GetValueOrDefault("gmt_payment")).ToUniversalTime();
                    order.PayType = "支付宝";
                    order.State = "已支付";

                    await db.SaveChangesAsync();

                    switch (order.Type)
                    {
                        case "业务逻辑":
                            {

                                return "success";

                            }
                    }
                }

            }

        }

        return null;
    }


    /// <summary>
    /// 支付宝退款
    /// </summary>
    /// <returns></returns>
    public async Task AliPayRefundAsync()
    {
        var settings = await db.AppSetting.AsNoTracking().Where(t => t.Module == "AliPayWeb").ToListAsync();

        var appId = settings.Where(t => t.Key == "AppId").Select(t => t.Value).FirstOrDefault();
        var appPrivateKey = settings.Where(t => t.Key == "AppPrivateKey").Select(t => t.Value).FirstOrDefault();
        var aliPayPublicKey = settings.Where(t => t.Key == "AliPayPublicKey").Select(t => t.Value).FirstOrDefault();

        string gatewayUrl = "https://openapi.alipay.com/gateway.do";

        DefaultAopClient client = new(gatewayUrl, appId, appPrivateKey, "json", "1.0", "RSA2", aliPayPublicKey, "UTF-8", false);
        AlipayTradeRefundRequest request = new();

        AlipayTradeRefundModel model = new()
        {
            TradeNo = "",//支付宝交易流水号
            RefundAmount = "",//退款金额如 0.01
            OutRequestNo = ""//退款单识别号
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
                //为N的情况，退款不一定失败，同一个请求第一次是Y第二次是N，该标记只标识本次请求是否有金额变动，为N时请主动调用退款查询 AliPayRefundSelect 接口
            }
        }
    }


    /// <summary>
    /// 支付宝退款查询接口
    /// </summary>
    /// <returns></returns>
    public async Task AliPayRefundSelectAsync()
    {
        var settings = await db.AppSetting.AsNoTracking().Where(t => t.Module == "AliPayWeb").ToListAsync();

        var appId = settings.Where(t => t.Key == "AppId").Select(t => t.Value).FirstOrDefault();
        var appPrivateKey = settings.Where(t => t.Key == "AppPrivateKey").Select(t => t.Value).FirstOrDefault();
        var aliPayPublicKey = settings.Where(t => t.Key == "AliPayPublicKey").Select(t => t.Value).FirstOrDefault();

        string gatewayUrl = "https://openapi.alipay.com/gateway.do";

        DefaultAopClient client = new(gatewayUrl, appId, appPrivateKey, "json", "1.0", "RSA2", aliPayPublicKey, "UTF-8", false);
        AlipayTradeFastpayRefundQueryRequest request = new();

        AlipayTradeFastpayRefundQueryModel model = new()
        {
            TradeNo = "",//支付宝交易流水号
            OutRequestNo = ""//退款单识别号
        };

        request.SetBizModel(model);

        AlipayTradeFastpayRefundQueryResponse response = client.Execute(request);

        if (response.IsError)
        {
            string errMsg = response.SubMsg;
        }
        else
        {
            if (response.RefundStatus == "REFUND_SUCCESS")
            {
                //则说明退款成功
            }
            else
            {

            }
        }
    }


    /// <summary>
    /// 微信支付Http请求
    /// </summary>
    /// <param name="mchId">商户Id</param>
    /// <param name="url">接口地址</param>
    /// <param name="data">请求数据，数据为空则认为是get请求</param>
    /// <returns></returns>
    public async Task<string> WeiXinPayHttpAsync(string mchId, string url, object? data = null)
    {
        var weiXinPayGroupId = await db.AppSetting.Where(t => t.Module == "WeiXinPay" && t.Key == "MchId" && t.Value == mchId).Select(t => t.GroupId).FirstOrDefaultAsync();

        var settings = await db.AppSetting.Where(t => t.Module == "WeiXinPay" && t.GroupId == weiXinPayGroupId).ToListAsync();

        string mchApiCertId = settings.Where(t => t.Key == "MchApiCertId").Select(t => t.Value).First();
        string mchApiCertKey = settings.Where(t => t.Key == "MchApiCertKey").Select(t => t.Value).First();
        string mchApiV3Key = settings.Where(t => t.Key == "MchApiV3Key").Select(t => t.Value).First();

        string dataJson = data == null ? "" : JsonHelper.ObjectToJson(data);

        string method = data == null ? "GET" : "POST";

        long timeStamp = DateTimeOffset.Now.ToUnixTimeSeconds();
        string nonceStr = Path.GetRandomFileName();
        string message = $"{method}\n{url[29..]}\n{timeStamp}\n{nonceStr}\n{dataJson}\n";
        string signature = CryptoHelper.RSASignData(mchApiCertKey, message, "base64", HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        string authorization = $"WECHATPAY2-SHA256-RSA2048 mchid=\"{mchId}\",nonce_str=\"{nonceStr}\",timestamp=\"{timeStamp}\",serial_no=\"{mchApiCertId}\",signature=\"{signature}\"";


        HttpRequestMessage requestMessage = new()
        {
            Method = data == null ? HttpMethod.Get : HttpMethod.Post,
            RequestUri = new Uri(url),
            Version = httpClient.DefaultRequestVersion,
            VersionPolicy = httpClient.DefaultVersionPolicy
        };


        if (method == "POST")
        {
            requestMessage.Content = new StringContent(dataJson);
            requestMessage.Content.Headers.ContentType = new("application/json")
            {
                CharSet = "utf-8"
            };

        }

        requestMessage.Headers.Add("Accept", "*/*");
        requestMessage.Headers.Add("User-Agent", ".NET HttpClient");
        requestMessage.Headers.Add("Authorization", authorization);


        using var responseMessage = await httpClient.SendAsync(requestMessage);

        string responseBody = await responseMessage.Content.ReadAsStringAsync();


        DtoWeiXinPayCertificates weiXinPayCertificates = new();

        if (url == "https://api.mch.weixin.qq.com/v3/certificates")
        {
            weiXinPayCertificates = JsonHelper.JsonToObject<DtoWeiXinPayCertificates>(responseBody);

            foreach (var weiXinPayCert in weiXinPayCertificates.data)
            {
                weiXinPayCert.certificate = CryptoHelper.AesGcmDecrypt(weiXinPayCert.encrypt_certificate.ciphertext, mchApiV3Key, weiXinPayCert.encrypt_certificate.nonce, weiXinPayCert.encrypt_certificate.associated_data, "base64");
            }
        }
        else
        {
            weiXinPayCertificates = await GetWeiXinPayCertificatesAsync(mchId);
        }


        var headers = responseMessage.Headers.ToDictionary(t => t.Key, t => t.Value.First());

        if (VerifySign(headers, responseBody, weiXinPayCertificates))
        {
            if (url == "https://api.mch.weixin.qq.com/v3/certificates")
            {
                await distributedCache.SetAsync(mchId + "GetWeiXinPayCertificates", weiXinPayCertificates, TimeSpan.FromHours(1));
            }

            return responseBody;
        }
        else
        {
            throw new Exception("签名验证异常");
        }
    }


    public async Task<DtoWeiXinPayCertificates> GetWeiXinPayCertificatesAsync(string mchId)
    {
        var cacheKey = mchId + "GetWeiXinPayCertificates";

        var weiXinPayCertificates = await distributedCache.GetAsync<DtoWeiXinPayCertificates>(cacheKey);

        if (weiXinPayCertificates != null)
        {
            return weiXinPayCertificates;
        }
        else
        {
            using (await distributedLock.LockAsync(mchId + "GetWeiXinPayCertificates" + "lock"))
            {
                weiXinPayCertificates = await distributedCache.GetAsync<DtoWeiXinPayCertificates>(cacheKey);

                if (weiXinPayCertificates != null)
                {

                    return weiXinPayCertificates;
                }
                else
                {
                    var certificatesRetData = await WeiXinPayHttpAsync(mchId, "https://api.mch.weixin.qq.com/v3/certificates");

                    if (certificatesRetData != null)
                    {
                        weiXinPayCertificates = await distributedCache.GetAsync<DtoWeiXinPayCertificates>(cacheKey);
                    }

                    if (weiXinPayCertificates != null)
                    {
                        return weiXinPayCertificates;
                    }
                    else
                    {
                        throw new Exception("证书获取失败");
                    }
                }
            }
        }
    }


    public bool VerifySign(Dictionary<string, string> headers, string body, DtoWeiXinPayCertificates weiXinPayCertificates)
    {
        string wechatPayNonce = headers.First(t => t.Key == "Wechatpay-Nonce").Value.ToString();
        string wechatpaySignature = headers.First(t => t.Key == "Wechatpay-Signature").Value.ToString();
        string wechatpaySerial = headers.First(t => t.Key == "Wechatpay-Serial").Value.ToString();
        string wechatpayTimestamp = headers.First(t => t.Key == "Wechatpay-Timestamp").Value.ToString();

        var certificate = weiXinPayCertificates.data.Where(t => t.serial_no == wechatpaySerial).Select(t => t.certificate).First();

        if (certificate != null)
        {
            string message = $"{wechatpayTimestamp}\n{wechatPayNonce}\n{body}\n";

            byte[] certificateBytes = Encoding.UTF8.GetBytes(certificate);

            using X509Certificate2 x509Certificate2 = X509CertificateLoader.LoadCertificate(certificateBytes);

            using var rsa = x509Certificate2.GetRSAPublicKey();

            if (rsa != null)
            {
                var publicKey = rsa.ExportRSAPublicKeyPem();

                return CryptoHelper.RSAVerifyData(publicKey, message, wechatpaySignature, "base64", HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            }
        }

        return false;
    }
}
