using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace SourceGenerator.Runtime;

/// <summary>
/// 记录调用生命周期：开始、耗时及返回值（如果有）。
/// 日志详细程度由 Logger 配置控制。
/// </summary>
public sealed class LoggingBehavior : IInvocationBehavior
{
    public async ValueTask<T> InvokeAsync<T>(InvocationContext ctx, Func<ValueTask<T>> next)
    {
        var logger = ctx.Logger;

        // 如果启用了耗时测量，则开始计时
        Stopwatch? sw = ctx.Measure ? Stopwatch.StartNew() : null;

        // 调用开始：如果启用日志，则记录方法名称与参数
        if (ctx.Log)
        {
            var hasArgs = !string.IsNullOrEmpty(ctx.ArgsJson);
            var startMsg = hasArgs
                ? $"Executing {ctx.Method} args: {ctx.ArgsJson}"
                : $"Executing {ctx.Method}";
            logger?.LogInformation(startMsg);
        }

        // (耗时测量已在上方按需开始)

        var result = await next();

        // 调用结束：如果启用日志和耗时统计，则记录执行时间
        if (ctx.Log && ctx.Measure && sw is not null)
        {
            sw.Stop();
            logger?.LogInformation($"Executed {ctx.Method} in {sw.ElapsedMilliseconds}ms");
        }

        // 返回结果：如果启用日志且返回类型非 Unit，则序列化输出
        if (ctx.Log && typeof(T) != typeof(Unit))
        {
            var json = JsonUtil.ToJson(result);
            logger?.LogInformation($"Return {ctx.Method} = {json}");
        }

        return result;
    }
}
