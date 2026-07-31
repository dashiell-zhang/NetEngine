using DistributedLock;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SourceGenerator.Runtime.Options;

namespace SourceGenerator.Runtime.Pipeline.Behaviors;

/// <summary>
/// 为标注的方法提供基于分布式锁的并发数限制（按方法入参作为 key）
/// </summary>
public sealed class ConcurrencyLimitBehavior : IInvocationAsyncBehavior
{

    /// <summary>
    /// 根据配置获取分布式锁并在锁范围内执行后续调用
    /// </summary>
    /// <typeparam name="T">调用返回值类型</typeparam>
    /// <param name="ctx">当前调用上下文</param>
    /// <param name="next">后续行为或目标方法</param>
    /// <returns>后续调用返回值</returns>
    public async ValueTask<T> InvokeAsync<T>(InvocationContext ctx, Func<ValueTask<T>> next)
    {

        var opt = ctx.GetFeature<ConcurrencyLimitOptions>();
        if (opt is null) return await next();

        var lockSvc = ctx.ServiceProvider?.GetService<IDistributedLock>();

        if (lockSvc is null) return await next();

        var semaphore = opt.Semaphore <= 0 ? 1 : opt.Semaphore;
        var expiry = opt.ExpirySeconds <= 0 ? default : TimeSpan.FromSeconds(opt.ExpirySeconds);

        if (opt.IsUseParameter && (!ctx.IsArgumentsKeyComplete || ctx.ArgumentsKey is null))
        {
            throw new InvalidOperationException($"方法 {ctx.Method} 的参数无法生成稳定的并发限制键");
        }

        var methodForLog = ctx.Method + " traceId=" + ctx.TraceId.ToString();
        var key = "ConcurrencyLimit_" + InvocationKey.ComposeHash(ctx, opt.IsUseParameter);

        IDisposable? handle = null;
        try
        {
            if (opt.IsBlock)
            {
                handle = await lockSvc.TryLockAsync(key, expiry, semaphore);
                if (handle is null)
                {
                    if (ctx.Log) ctx.Logger?.LogInformation($"ConcurrencyLimit blocked {methodForLog}");
                    throw new InvalidOperationException("请勿频繁操作");
                }
            }
            else
            {
                handle = await lockSvc.LockAsync(key, expiry, semaphore);
            }

            if (ctx.Log) ctx.Logger?.LogInformation($"ConcurrencyLimit acquired {methodForLog} semaphore={semaphore} expirySeconds={opt.ExpirySeconds}");
            return await next();
        }
        finally
        {
            try
            {
                handle?.Dispose();
                if (ctx.Log) ctx.Logger?.LogInformation($"ConcurrencyLimit released {methodForLog}");
            }
            catch (Exception ex)
            {
                if (ctx.Log) ctx.Logger?.LogInformation($"ConcurrencyLimit release error {methodForLog}: {ex.Message}");
            }
        }

    }


}
