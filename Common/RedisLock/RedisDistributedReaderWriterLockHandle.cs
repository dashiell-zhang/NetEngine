using Common.RedisLock.Core;
using Common.RedisLock.Core.Internal;
using Common.RedisLock.RedLock;
using System.Threading;
using System.Threading.Tasks;

namespace Common.RedisLock
{
    /// <summary>
    /// 为 <see cref="RedisDistributedReaderWriterLock"/> 实现 <see cref="IDistributedSynchronizationHandle"/>
    /// </summary>
    public sealed class RedisDistributedReaderWriterLockHandle : IDistributedSynchronizationHandle
    {
        private RedLockHandle? _innerHandle;

        internal RedisDistributedReaderWriterLockHandle(RedLockHandle innerHandle)
        {
            this._innerHandle = innerHandle;
        }

        /// <summary>
        /// 实现 <see cref="IDistributedSynchronizationHandle.HandleLostToken"/>
        /// </summary>
        public CancellationToken HandleLostToken => Volatile.Read(ref this._innerHandle)?.HandleLostToken ?? throw this.ObjectDisposed();

        /// <summary>
        /// 释放锁
        /// </summary>
        public void Dispose() => Interlocked.Exchange(ref this._innerHandle, null)?.Dispose();

        /// <summary>
        /// 异步释放锁
        /// </summary>
        /// <returns></returns>
        public ValueTask DisposeAsync() => Interlocked.Exchange(ref this._innerHandle, null)?.DisposeAsync() ?? default;
    }
}
