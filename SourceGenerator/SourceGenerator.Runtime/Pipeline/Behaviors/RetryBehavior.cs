using Microsoft.Extensions.Logging;
using SourceGenerator.Runtime.Options;

namespace SourceGenerator.Runtime.Pipeline.Behaviors;

/// <summary>
/// 为标注的方法提供自动重试能力，执行出现异常时按配置次数重试
/// </summary>
public sealed class RetryBehavior : IInvocationAsyncBehavior
{

    /// <summary>
    /// 对后续调用的非取消异常按照配置执行重试
    /// </summary>
    /// <typeparam name="T">调用返回值类型</typeparam>
    /// <param name="ctx">当前调用上下文</param>
    /// <param name="next">后续行为或目标方法</param>
    /// <returns>最终成功调用的返回值</returns>
    public async ValueTask<T> InvokeAsync<T>(InvocationContext ctx, Func<ValueTask<T>> next)
    {

        var opt = ctx.GetFeature<RetryOptions>();
        var maxRetries = opt?.MaxRetries ?? 3;
        var delaySeconds = opt?.DelaySeconds ?? 0;

        if (maxRetries < 0)
            throw new ArgumentOutOfRangeException(nameof(RetryOptions.MaxRetries), maxRetries, "MaxRetries 不能小于 0");

        if (delaySeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(RetryOptions.DelaySeconds), delaySeconds, "DelaySeconds 不能小于 0");

        if (maxRetries == 0) return await next();

        var methodForLog = ctx.Method + " traceId=" + ctx.TraceId.ToString();
        var maxExecutions = (long)maxRetries + 1;
        long execution = 1;

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
            catch (Exception ex)
            {
                if (execution >= maxExecutions)
                {
                    if (ctx.Log) ctx.Logger?.LogError($"Retry exhausted executions={execution}/{maxExecutions} {methodForLog}: {ex.Message}");
                    throw;
                }

                ctx.CancellationToken.ThrowIfCancellationRequested();

                if (ctx.Log) ctx.Logger?.LogWarning($"Retry scheduled retry={execution}/{maxRetries} nextExecution={execution + 1}/{maxExecutions} {methodForLog}: {ex.Message}");

                if (delaySeconds > 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds), ctx.CancellationToken);
                }

                execution++;
            }
        }

    }

}
