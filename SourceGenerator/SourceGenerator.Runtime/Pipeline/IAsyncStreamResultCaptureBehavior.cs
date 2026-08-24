namespace SourceGenerator.Runtime.Pipeline;

/// <summary>
/// 定义需要获取异步流元素快照的同步行为
/// </summary>
public interface IAsyncStreamResultCaptureBehavior
{

    /// <summary>
    /// 判断当前调用是否需要捕获异步流元素
    /// </summary>
    /// <param name="context">当前调用上下文</param>
    /// <returns>需要捕获元素时返回 true</returns>
    bool ShouldCaptureAsyncStreamItems(InvocationContext context);

}
