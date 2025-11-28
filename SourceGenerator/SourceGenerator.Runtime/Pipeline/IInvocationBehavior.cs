namespace SourceGenerator.Runtime.Pipeline;

/// <summary>
/// 定义同步调用管道中的一个行为节点
/// </summary>
public interface IInvocationBehavior
{

    /// <summary>
    /// 在目标方法执行前被调用
    /// </summary>
    /// <param name="ctx">当前调用上下文</param>
    void OnBefore(InvocationContext ctx);


    /// <summary>
    /// 在目标方法成功执行后被调用
    /// </summary>
    /// <param name="ctx">当前调用上下文</param>
    /// <param name="result">目标方法的返回结果</param>
    void OnAfter(InvocationContext ctx, object? result);


    /// <summary>
    /// 在目标方法或后续行为抛出异常时被调用
    /// </summary>
    /// <param name="ctx">当前调用上下文</param>
    /// <param name="ex">捕获到的异常</param>
    void OnException(InvocationContext ctx, Exception ex);
}

