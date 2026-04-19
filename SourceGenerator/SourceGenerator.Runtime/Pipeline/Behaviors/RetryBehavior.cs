using Microsoft.Extensions.Logging;
using SourceGenerator.Runtime.Options;

namespace SourceGenerator.Runtime.Pipeline.Behaviors;

/// <summary>
/// 为标注的方法提供自动重试能力，执行出现异常时按配置次数重试
/// </summary>
public sealed class RetryBehavior : IInvocationAsyncBehavior
{

    public async ValueTask<T> InvokeAsync<T>(InvocationContext ctx, Func<ValueTask<T>> next)
    {
        var opt = ctx.GetFeature<RetryOptions>();
        var maxRetries = opt?.MaxRetries ?? 3;

        if (maxRetries <= 0) return await next();

        var methodForLog = ctx.Method + " traceId=" + ctx.TraceId.ToString();
        var delaySeconds = opt?.DelaySeconds ?? 0;
        var attempt = 0;

        while (true)
        {
            try
            {
                return await next();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                attempt++;
                if (ctx.Log) ctx.Logger?.LogWarning($"Retry {attempt}/{maxRetries} {methodForLog}: {ex.Message}");
                if (delaySeconds > 0) await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
            }
        }
    }

}
