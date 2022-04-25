using Common.RedisLock.Core.Internal;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Common.RedisLock.RedLock
{
    /// <summary>
    /// 充当 <see cref="Task.Delay(TimeSpan, CancellationToken)"/> ，当它被清理时
    /// <see cref="RedLockTimeoutTask"/> 被释放
    /// </summary>
    internal readonly struct RedLockTimeoutTask : IDisposable
    {
        private readonly CancellationTokenSource _cleanupTokenSource;
        private readonly CancellationTokenSource? _linkedTokenSource;

        public RedLockTimeoutTask(TimeoutValue timeout, CancellationToken cancellationToken)
        {
            this._cleanupTokenSource = new CancellationTokenSource();
            this._linkedTokenSource = cancellationToken.CanBeCanceled
                ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, this._cleanupTokenSource.Token)
                : null;
            this.Task = Task.Delay(timeout.TimeSpan, this._linkedTokenSource?.Token ?? this._cleanupTokenSource.Token);
        }

        public Task Task { get; }

        public void Dispose()
        {
            try { this._cleanupTokenSource.Cancel(); }
            finally
            {
                this._linkedTokenSource?.Dispose();
                this._cleanupTokenSource.Dispose();
            }
        }
    }
}
