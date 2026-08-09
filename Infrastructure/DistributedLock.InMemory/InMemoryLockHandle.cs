using DistributedLock.InMemory.Models;
using System.Diagnostics;

namespace DistributedLock.InMemory
{
    /// <summary>
    /// 内存锁句柄
    /// 释放时归还名额并触发分组清理
    /// </summary>
    internal sealed class InMemoryLockHandle : IDistributedLockHandle
    {

        /// <summary>
        /// 长租期过期检查的单次等待时长
        /// </summary>
        private static readonly TimeSpan ExpirationDelaySegment = TimeSpan.FromDays(1);

        /// <summary>
        /// 句柄状态同步对象
        /// </summary>
        private readonly object stateLock = new();


        /// <summary>
        /// 锁名称
        /// </summary>
        private readonly string key;

        /// <summary>
        /// 锁分组
        /// </summary>
        private readonly LockGroup group;

        /// <summary>
        /// 当前占用的信号量槽位
        /// </summary>
        private readonly SemaphoreSlim slot;

        /// <summary>
        /// 过期释放控制器
        /// </summary>
        private CancellationTokenSource? expiryCts;

        /// <summary>
        /// 是否已释放
        /// </summary>
        private bool disposed;


        /// <summary>
        /// 创建内存锁句柄并启动过期释放任务
        /// </summary>
        /// <param name="key">锁名称</param>
        /// <param name="group">锁分组</param>
        /// <param name="slot">占用的信号量槽位</param>
        /// <param name="expiry">失效时长</param>
        internal InMemoryLockHandle(string key, LockGroup group, SemaphoreSlim slot, TimeSpan expiry)
        {

            ValidateExpiry(expiry);

            this.key = key;
            this.group = group;
            this.slot = slot;

            expiryCts = new CancellationTokenSource();
            _ = ExpireAsync(expiry, expiryCts, expiryCts.Token);

        }


        /// <summary>
        /// 释放锁
        /// </summary>
        public void Dispose()
        {

            CancellationTokenSource? currentCts;

            lock (stateLock)
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                currentCts = expiryCts;
                expiryCts = null;
            }

            if (currentCts is not null)
            {
                currentCts.Cancel();
                currentCts.Dispose();
            }

            ReleaseLock();

        }


        /// <summary>
        /// 异步释放锁并立即返回已经完成的等待结果
        /// </summary>
        public ValueTask DisposeAsync()
        {

            Dispose();
            return ValueTask.CompletedTask;

        }


        /// <summary>
        /// 续期锁
        /// </summary>
        /// <param name="expiry">新的失效时长</param>
        /// <returns>续期是否成功</returns>
        internal bool Renew(TimeSpan expiry)
        {

            ValidateExpiry(expiry);

            var nextCts = new CancellationTokenSource();
            CancellationTokenSource? currentCts;

            lock (stateLock)
            {
                if (disposed)
                {
                    nextCts.Dispose();
                    return false;
                }

                currentCts = expiryCts;
                expiryCts = nextCts;
                _ = ExpireAsync(expiry, nextCts, nextCts.Token);
            }

            if (currentCts is not null)
            {
                currentCts.Cancel();
                currentCts.Dispose();
            }

            return true;

        }


        /// <summary>
        /// 到期后自动释放
        /// </summary>
        /// <param name="expiry">失效时长</param>
        /// <param name="expectedCts">当前过期释放控制器</param>
        /// <param name="cancellationToken">取消令牌</param>
        private async Task ExpireAsync(TimeSpan expiry, CancellationTokenSource expectedCts, CancellationToken cancellationToken)
        {

            var startTimestamp = Stopwatch.GetTimestamp();

            try
            {
                while (true)
                {
                    var remaining = expiry - Stopwatch.GetElapsedTime(startTimestamp);
                    if (remaining <= TimeSpan.Zero)
                    {
                        break;
                    }

                    await Task.Delay(remaining > ExpirationDelaySegment ? ExpirationDelaySegment : remaining, cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            lock (stateLock)
            {
                if (disposed || !ReferenceEquals(expiryCts, expectedCts))
                {
                    return;
                }

                disposed = true;
                expiryCts = null;
            }

            expectedCts.Dispose();
            ReleaseLock();

        }


        /// <summary>
        /// 归还信号量名额并释放锁分组引用
        /// </summary>
        private void ReleaseLock()
        {

            slot.Release();
            InMemoryLock.ReleaseGroup(key, group);

        }


        /// <summary>
        /// 验证锁失效时长
        /// </summary>
        /// <param name="expiry">锁失效时长</param>
        private static void ValidateExpiry(TimeSpan expiry)
        {

            if (expiry <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(expiry), "expiry 必须大于 0");
            }

        }

    }
}
