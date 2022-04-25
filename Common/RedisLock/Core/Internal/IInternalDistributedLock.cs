using System;
using System.Threading;
using System.Threading.Tasks;

namespace Common.RedisLock.Core.Internal
{
    internal interface IInternalDistributedLock<THandle> : IDistributedLock
        where THandle : class, IDistributedSynchronizationHandle
    {
        new THandle? TryAcquire(TimeSpan timeout = default, CancellationToken cancellationToken = default);
        new THandle Acquire(TimeSpan? timeout = null, CancellationToken cancellationToken = default);
        new ValueTask<THandle?> TryAcquireAsync(TimeSpan timeout = default, CancellationToken cancellationToken = default);
        new ValueTask<THandle> AcquireAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default);

        // 内部结构
        ValueTask<THandle?> InternalTryAcquireAsync(TimeoutValue timeout, CancellationToken cancellationToken);
    }
}
