namespace DistributedLock;

/// <summary>
/// 表示支持同步兼容释放和可等待异步释放的分布式锁句柄
/// </summary>
public interface IDistributedLockHandle : IDisposable, IAsyncDisposable
{
}
