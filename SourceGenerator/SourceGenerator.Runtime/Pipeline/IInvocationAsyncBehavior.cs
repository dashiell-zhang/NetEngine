namespace SourceGenerator.Runtime.Pipeline;
/// <summary>
/// 定义异步调用管道中的一个行为节点
/// </summary>
public interface IInvocationAsyncBehavior
{

    /// <summary>
    /// 执行当前行为并调用下一个行为节点
    /// </summary>
    /// <typeparam name="T">返回值类型</typeparam>
    /// <param name="ctx">当前调用上下文</param>
    /// <param name="next">下一个行为的委托</param>
    /// <returns>最终或链式调用后的返回值</returns>
    ValueTask<T> InvokeAsync<T>(InvocationContext ctx, Func<ValueTask<T>> next);

}
