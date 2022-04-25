using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Common.RedisLock.Core.Internal
{
    /// <summary>
    /// 用于监视/更新固定长度“租赁”锁的实用程序
    /// </summary>
    internal sealed class LeaseMonitor : IDisposable, IAsyncDisposable
    {
        private readonly CancellationTokenSource _disposalSource = new CancellationTokenSource(),
            _handleLostSource = new CancellationTokenSource();

        private readonly ILeaseHandle _leaseHandle;
        private readonly Task _monitoringTask;
        private Task? _cancellationTask;

        public LeaseMonitor(ILeaseHandle leaseHandle)
        {
            Invariant.Require(leaseHandle.LeaseDuration.CompareTo(leaseHandle.MonitoringCadence) >= 0);

            this._leaseHandle = leaseHandle;
            this._monitoringTask = CreateMonitoringLoopTask(new WeakReference<LeaseMonitor>(this), leaseHandle.MonitoringCadence, this._disposalSource.Token);
        }

        public CancellationToken HandleLostToken => this._handleLostSource.Token;

        public void Dispose() => this.DisposeSyncViaAsync();

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (!this._disposalSource.IsCancellationRequested) // 幂等的
                {
                    this._disposalSource.Cancel();
                }

                await this._monitoringTask.AwaitSyncOverAsync().ConfigureAwait(false);
            }
            finally
            {
                if (this._cancellationTask != null)
                {
                    _ = this._cancellationTask.ContinueWith((_, state) => ((CancellationTokenSource)state!).Dispose(), state: this._handleLostSource);
                }
                else
                {
                    this._handleLostSource.Dispose();
                }
                this._disposalSource.Dispose();
            }
        }

        private static Task CreateMonitoringLoopTask(WeakReference<LeaseMonitor> weakMonitor, TimeoutValue monitoringCadence, CancellationToken disposalToken)
        {
            return Task.Run(() => MonitoringLoop());

            async Task MonitoringLoop()
            {
                var leaseLifetime = Stopwatch.StartNew();
                do
                {
                    // 等到下一次监控检查
                    await Task.Delay(monitoringCadence.InMilliseconds, disposalToken).TryAwait();
                }
                while (!disposalToken.IsCancellationRequested && await RunMonitoringLoopIterationAsync(weakMonitor, leaseLifetime).ConfigureAwait(false));
            }
        }

        private static async Task<bool> RunMonitoringLoopIterationAsync(WeakReference<LeaseMonitor> weakMonitor, Stopwatch leaseLifetime)
        {
            // 如果监视器已被 GC，则退出
            if (!weakMonitor.TryGetTarget(out var monitor)) { return false; }

            // 租约到期
            if (monitor._leaseHandle.LeaseDuration.CompareTo(leaseLifetime.Elapsed) < 0)
            {
                OnHandleLost();
                return false;
            }

            var leaseState = await monitor.CheckLeaseAsync().ConfigureAwait(false);
            switch (leaseState)
            {
                case LeaseState.Lost:
                    OnHandleLost();
                    return false;

                case LeaseState.Renewed:
                    leaseLifetime.Restart();
                    return true;

                // 如果租约被持有但未续订，或者我们不知道（例如，由于暂时性故障），
                // 然后继续。 我们还不能说它丢失了，但它没有更新，所以我们不能重置
                // 生命周期。
                case LeaseState.Held:
                case LeaseState.Unknown:
                    return true;

                default:
                    throw new InvalidOperationException("should never get here");
            }

            // 将取消卸载到后台线程以避免挂起或错误
            void OnHandleLost() => monitor._cancellationTask = Task.Run(() => monitor._handleLostSource.Cancel());
        }

        private async Task<LeaseState> CheckLeaseAsync()
        {
            var renewOrValidateTask = Helpers.SafeCreateTask(state => state.leaseHandle.RenewOrValidateLeaseAsync(state.Token), (leaseHandle: this._leaseHandle, this._disposalSource.Token));
            await renewOrValidateTask.TryAwait();
            return this._disposalSource.IsCancellationRequested || renewOrValidateTask.Status != TaskStatus.RanToCompletion
                ? LeaseState.Unknown
                : renewOrValidateTask.Result;
        }

        public interface ILeaseHandle
        {
            TimeoutValue LeaseDuration { get; }
            TimeoutValue MonitoringCadence { get; }
            Task<LeaseState> RenewOrValidateLeaseAsync(CancellationToken cancellationToken);
        }

        public enum LeaseState
        {
            /// <summary>
            /// 已知租约仍持有但未续约
            /// </summary>
            Held,

            /// <summary>
            /// <see cref="ILeaseHandle.LeaseDuration"/> 的租约已更新
            /// </summary>
            Renewed,

            /// <summary>
            /// 已知租约不再持有
            /// </summary>
            Lost,

            /// <summary>
            /// 租约可能会或可能不再持有
            /// </summary>
            Unknown,
        }
    }
}
