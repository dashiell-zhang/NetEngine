namespace DistributedLock;

/// <summary>
/// 表示支持同步兼容释放和可等待异步释放的分布式锁句柄
/// 释放失败应由实现记录错误且不改变业务执行结果
/// </summary>
public interface IDistributedLockHandle : IDisposable, IAsyncDisposable
{
}
