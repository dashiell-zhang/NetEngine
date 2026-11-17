using Application.Interface;
using Application.Model.Pay;
using Application.Service.Payment.Clients;
using Common;
using DistributedLock;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Repository;
using Repository.Database;
using SourceGenerator.Runtime.Attributes;
using System.Globalization;

namespace Application.Service.Payment;

/// <summary>
/// 编排订单支付、配置选择及支付成功通知处理
/// </summary>
[RegisterService(typeof(IPayService), Lifetime = ServiceLifetime.Scoped)]
public class PayService(ILogger<PayService> logger, IHttpClientFactory httpClientFactory, DatabaseContext db, IDistributedCache cache, IDistributedLock distributedLock) : IPayService
{

    /// <summary>
    /// 模块内部的支付宝协议客户端
    /// </summary>
    private readonly AliPayClient aliPayClient = new();


    /// <summary>
    /// 模块内部的微信支付协议客户端
    /// </summary>
    private readonly WeiXinPayClient weiXinPayClient = new(httpClientFactory.CreateClient(), cache, distributedLock);


    /// <summary>
    /// 获取微信JSAPI支付参数
    /// </summary>
    public async Task<CreateWeiXinPayJSAPIRetDto?> CreateWeiXinPayJSAPIAsync(string orderNo, string openId, string notifyUrl)
    {

        ArgumentException.ThrowIfNullOrWhiteSpace(openId);
        var order = await GetPayableOrderAsync(orderNo);
        if (order is null)
        {
            return null;
        }
        var options = await GetWeiXinOptionsAsync();
        string key = GetWeiXinCacheKey("JSAPI", options, order) + ":" + CryptoHelper.MD5HashData(openId);
        var result = await cache.GetAsync<CreateWeiXinPayJSAPIRetDto>(key);
        if (result is null)
        {
            result = await weiXinPayClient.CreateJSAPIAsync(options, order.OrderNo, order.Price, GetSubject(order), openId, notifyUrl + options.MerchantId);
            await cache.SetAsync(key, result, TimeSpan.FromHours(1.8));
        }
        return result;

    }


    /// <summary>
    /// 获取微信App支付参数
    /// </summary>
    public async Task<CreateWeiXinPayAppRetDto?> CreateWeiXinPayAppAsync(string orderNo, string notifyUrl)
    {

        var order = await GetPayableOrderAsync(orderNo);
        if (order is null)
        {
            return null;
        }
        var options = await GetWeiXinOptionsAsync();
        string key = GetWeiXinCacheKey("App", options, order);
        var result = await cache.GetAsync<CreateWeiXinPayAppRetDto>(key);
        if (result is null)
        {
            result = await weiXinPayClient.CreateAppAsync(options, order.OrderNo, order.Price, GetSubject(order), notifyUrl + options.MerchantId);
            await cache.SetAsync(key, result, TimeSpan.FromHours(1.8));
        }
        return result;

    }


    /// <summary>
    /// 获取微信H5支付地址
    /// </summary>
    public async Task<string?> CreateWeiXinPayH5Async(string orderNo, string notifyUrl, string clientIP)
    {

        ArgumentException.ThrowIfNullOrWhiteSpace(clientIP);
        var order = await GetPayableOrderAsync(orderNo);
        if (order is null)
        {
            return null;
        }
        var options = await GetWeiXinOptionsAsync();
        string key = GetWeiXinCacheKey("H5", options, order) + ":" + CryptoHelper.MD5HashData(clientIP);
        string? result = await cache.GetStringAsync(key);
        if (result is null)
        {
            result = await weiXinPayClient.CreateH5Async(options, order.OrderNo, order.Price, GetSubject(order), clientIP, notifyUrl + options.MerchantId);
            await cache.SetStringAsync(key, result, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(4.5) });
        }
        return result;

    }


    /// <summary>
    /// 获取微信Native支付二维码地址
    /// </summary>
    public async Task<string?> CreateWeiXinPayPCAsync(string orderNo, string notifyUrl)
    {

        var order = await GetPayableOrderAsync(orderNo);
        if (order is null)
        {
            return null;
        }
        var options = await GetWeiXinOptionsAsync();
        string key = GetWeiXinCacheKey("PC", options, order);
        string? result = await cache.GetStringAsync(key);
        if (result is null)
        {
            result = await weiXinPayClient.CreatePCAsync(options, order.OrderNo, order.Price, GetSubject(order), notifyUrl + options.MerchantId);
            await cache.SetStringAsync(key, result, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15) });
        }
        return result;

    }


    /// <summary>
    /// 创建支付宝小程序交易
    /// </summary>
    public async Task<string?> CreateAliPayMiniAppAsync(string orderNo, string notifyUrl)
    {

        var order = await GetPayableOrderAsync(orderNo);
        if (order is null)
        {
            return null;
        }
        var options = await GetAliPayOptionsAsync("AliPayMiniApp");
        string? buyerId = await db.UserBindExternal.AsNoTracking().Where(item => item.UserId == order.CreateUserId && item.AppName == "AliPayMiniApp" && item.AppId == options.AppId).Select(item => item.OpenId).SingleOrDefaultAsync();
        if (string.IsNullOrWhiteSpace(buyerId))
        {
            return null;
        }
        return aliPayClient.CreateMiniApp(options, order.OrderNo, order.Price, GetSubject(order), buyerId, notifyUrl);

    }


    /// <summary>
    /// 创建支付宝PC支付地址
    /// </summary>
    public async Task<string?> CreateAliPayPCAsync(string orderNo, string notifyUrl, string? returnUrl)
    {

        var order = await GetPayableOrderAsync(orderNo);
        if (order is null)
        {
            return null;
        }
        var options = await GetAliPayOptionsAsync("AliPayWeb");
        return aliPayClient.CreatePC(options, order.OrderNo, order.Price, GetSubject(order), notifyUrl, returnUrl);

    }


    /// <summary>
    /// 创建支付宝H5支付地址
    /// </summary>
    public async Task<string?> CreateAliPayH5Async(string orderNo, string notifyUrl, string returnUrl, string quitUrl)
    {

        var order = await GetPayableOrderAsync(orderNo);
        if (order is null)
        {
            return null;
        }
        var options = await GetAliPayOptionsAsync("AliPayWeb");
        return aliPayClient.CreateH5(options, order.OrderNo, order.Price, GetSubject(order), notifyUrl, returnUrl, quitUrl);

    }


    /// <summary>
    /// 验证微信支付通知并幂等更新订单
    /// </summary>
    public async Task<bool> WeiXinPayNotifyAsync(string mchId, Dictionary<string, string> headers, string requestBody)
    {

        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(mchId);
            var options = await GetWeiXinOptionsAsync(mchId);
            var notification = await weiXinPayClient.ReadNotificationAsync(options, headers, requestBody);
            await ApplyPaymentAsync(notification, "微信支付");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "微信支付通知处理失败");
            return false;
        }

    }


    /// <summary>
    /// 验证支付宝通知并幂等更新订单
    /// </summary>
    public async Task<bool> AliPayNotifyAsync(Dictionary<string, string> parameters)
    {

        try
        {
            string? appId = parameters.GetValueOrDefault("app_id");
            ArgumentException.ThrowIfNullOrWhiteSpace(appId);
            var options = await GetAliPayOptionsAsync(appId: appId);
            var notification = aliPayClient.ReadNotification(options, parameters);
            await ApplyPaymentAsync(notification, "支付宝");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "支付宝通知处理失败");
            return false;
        }

    }


    /// <summary>
    /// 校验订单及金额后完成支付状态变更，相同成功通知可重复确认
    /// </summary>
    private async Task ApplyPaymentAsync(PaymentNotification notification, string payType)
    {

        await using var handle = await distributedLock.LockAsync("Payment:Order:" + notification.OrderNo);
        var order = await db.Order.SingleOrDefaultAsync(item => item.OrderNo == notification.OrderNo) ?? throw new CustomException("支付通知对应订单不存在");
        if (order.Price != notification.Amount)
        {
            throw new CustomException("支付通知金额与订单金额不匹配");
        }
        if (order.PayState)
        {
            if (!IsSamePayment(order, notification, payType))
            {
                throw new CustomException("订单已有不同的支付记录");
            }
            return;
        }
        if (order.State != "待支付")
        {
            throw new CustomException("当前订单状态不允许确认支付");
        }

        order.PayPrice = notification.Amount;
        order.SerialNo = notification.TransactionId;
        order.PayState = true;
        order.PayTime = notification.PaidAt;
        order.PayType = payType;
        order.State = "已支付";
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            await db.Entry(order).ReloadAsync();
            if (db.Entry(order).State == EntityState.Detached || !IsSamePayment(order, notification, payType))
            {
                throw new CustomException("订单状态已变化，请重试支付通知");
            }
        }

    }


    /// <summary>
    /// 判断订单是否已记录同一渠道的同一笔支付
    /// </summary>
    private static bool IsSamePayment(Order order, PaymentNotification notification, string payType)
    {

        return order.PayState && order.PayType == payType && order.SerialNo == notification.TransactionId && order.PayPrice == notification.Amount && order.Price == notification.Amount;

    }


    /// <summary>
    /// 查询可支付订单并校验人民币金额精度
    /// </summary>
    private async Task<Order?> GetPayableOrderAsync(string orderNo)
    {

        ArgumentException.ThrowIfNullOrWhiteSpace(orderNo);
        var order = await db.Order.AsNoTracking().SingleOrDefaultAsync(item => item.OrderNo == orderNo);
        if (order is null || order.PayState || order.State != "待支付")
        {
            return null;
        }
        if (order.Price <= 0 || decimal.Round(order.Price, 2) != order.Price)
        {
            throw new CustomException("订单支付金额无效");
        }
        return order;

    }


    /// <summary>
    /// 按应用或模块选择唯一的支付宝配置组
    /// </summary>
    private async Task<AliPayOptions> GetAliPayOptionsAsync(string? module = null, string? appId = null)
    {

        var settings = await db.AppSetting.AsNoTracking().Where(item => item.Module == "AliPayWeb" || item.Module == "AliPayMiniApp").ToListAsync();
        var groups = settings.Where(item => item.Key == "AppId" && (module is null || item.Module == module) && (appId is null || item.Value == appId)).ToList();
        if (groups.Count != 1)
        {
            throw new CustomException("支付宝配置缺失或存在多个匹配的配置组");
        }
        var selected = groups[0];
        var values = settings.Where(item => item.Module == selected.Module && item.GroupId == selected.GroupId).ToList();
        return new AliPayOptions(GetSetting(values, "AppId"), GetSetting(values, "AppPrivateKey"), GetSetting(values, "AliPayPublicKey"));

    }


    /// <summary>
    /// 按商户号选择唯一微信配置组，通知解密与验签使用同组配置
    /// </summary>
    private async Task<WeiXinPayOptions> GetWeiXinOptionsAsync(string? merchantId = null)
    {

        var settings = await db.AppSetting.AsNoTracking().Where(item => item.Module == "WeiXinPay").ToListAsync();
        var groups = settings.Where(item => item.Key == "MchId" && (merchantId is null || item.Value == merchantId)).ToList();
        if (groups.Count != 1)
        {
            throw new CustomException("微信支付配置缺失或存在多个匹配的配置组");
        }
        var values = settings.Where(item => item.GroupId == groups[0].GroupId).ToList();
        return new WeiXinPayOptions(GetSetting(values, "AppId"), GetSetting(values, "MchId"), GetSetting(values, "MchApiCertId"), GetSetting(values, "MchApiCertKey"), GetSetting(values, "MchApiV3Key"));

    }


    /// <summary>
    /// 读取配置组内唯一且非空的配置值
    /// </summary>
    private static string GetSetting(List<AppSetting> settings, string key)
    {

        var matches = settings.Where(item => item.Key == key).ToList();
        if (matches.Count != 1 || string.IsNullOrWhiteSpace(matches[0].Value))
        {
            throw new CustomException($"支付配置项 {key} 缺失或重复");
        }
        return matches[0].Value;

    }


    /// <summary>
    /// 缓存按场景、商户、应用、订单及金额区分
    /// </summary>
    private static string GetWeiXinCacheKey(string scene, WeiXinPayOptions options, Order order)
    {

        return $"Payment:WeiXin:{scene}:{options.MerchantId}:{options.AppId}:{order.Id}:{order.Price.ToString(CultureInfo.InvariantCulture)}";

    }


    /// <summary>
    /// 生成订单支付标题
    /// </summary>
    private static string GetSubject(Order order)
    {

        return order.CreateTime.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture) + "交易";

    }

}
