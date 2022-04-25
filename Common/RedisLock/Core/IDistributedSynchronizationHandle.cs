using System;
using System.Threading;

namespace Common.RedisLock.Core
{
    /// <summary>
    /// 分布式锁或其他同步原语的句柄。 要解锁/释放，
    /// 简单地处理句柄。
    /// </summary>
    public interface IDistributedSynchronizationHandle
        : IDisposable, IAsyncDisposable
    {
        /// <summary>
        /// 获取一个 <see cref="CancellationToken"/> 实例，该实例可用于
        /// 监控锁的句柄是否在句柄丢失之前丢失
        /// 已处理。
        ///
        /// 例如，如果锁由 a 支持，则可能发生这种情况
        /// 数据库和与数据库的连接中断。
        ///
        /// 不是所有的锁类型都支持这个； 那些不返回的将返回 <see cref="CancellationToken.None"/>
        /// 可以通过检查 <see cref="CancellationToken.CanBeCanceled"/> 来检测。
        ///
        /// 对于支持这个的锁类型，访问这个属性可能会产生额外的
        /// 成本，例如轮询以检测连接丢失。
        /// </summary>
        CancellationToken HandleLostToken { get; }
    }
}
