using Common.RedisLock.Core;
using Common.RedisLock.Core.Internal;
using StackExchange.Redis;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Common.RedisLock.RedLock
{
    internal sealed class RedLockHandle : IDistributedSynchronizationHandle, LeaseMonitor.ILeaseHandle
    {
        private readonly IRedLockExtensibleSynchronizationPrimitive _primitive;
        private Dictionary<IDatabase, Task<bool>>? _tryAcquireTasks;
        private readonly TimeoutValue _extensionCadence, _expiry;
        private readonly LeaseMonitor _monitor;

        public RedLockHandle(
            IRedLockExtensibleSynchronizationPrimitive primitive,
            Dictionary<IDatabase, Task<bool>> tryAcquireTasks,
            TimeoutValue extensionCadence,
            TimeoutValue expiry)
        {
            this._primitive = primitive;
            this._tryAcquireTasks = tryAcquireTasks;
            this._extensionCadence = extensionCadence;
            this._expiry = expiry;
            // 最后设置它很重要，因为监视器构造函数将读取 this 的其他字段
            this._monitor = new LeaseMonitor(this);
        }

        /// <summary>
        /// 实现 <see cref="IDistributedSynchronizationHandle.HandleLostToken"/>
        /// </summary>
        public CancellationToken HandleLostToken => this._monitor.HandleLostToken;

        /// <summary>
        /// 释放锁
        /// </summary>
        public void Dispose() => this.DisposeSyncViaAsync();

        /// <summary>
        /// 异步释放锁
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            await this._monitor.DisposeAsync().ConfigureAwait(false);
            var tryAcquireTasks = Interlocked.Exchange(ref this._tryAcquireTasks, null);
            if (tryAcquireTasks != null)
            {
                await new RedLockRelease(this._primitive, tryAcquireTasks).ReleaseAsync().ConfigureAwait(false);
            }
        }

        TimeoutValue LeaseMonitor.ILeaseHandle.LeaseDuration => this._expiry;

        TimeoutValue LeaseMonitor.ILeaseHandle.MonitoringCadence => this._extensionCadence;

        async Task<LeaseMonitor.LeaseState> LeaseMonitor.ILeaseHandle.RenewOrValidateLeaseAsync(CancellationToken cancellationToken)
        {
            var extendResult = await new RedLockExtend(this._primitive, this._tryAcquireTasks!, cancellationToken).TryExtendAsync().ConfigureAwait(false);
            return extendResult switch
            {
                null => LeaseMonitor.LeaseState.Unknown,
                false => LeaseMonitor.LeaseState.Lost,
                true => LeaseMonitor.LeaseState.Renewed,
            };
        }
    }
}
