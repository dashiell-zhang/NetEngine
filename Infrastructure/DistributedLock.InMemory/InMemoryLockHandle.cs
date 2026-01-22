using DistributedLock.InMemory.Models;

namespace DistributedLock.InMemory
{
    /// <summary>
    /// 内存锁句柄
    /// 释放时归还名额并触发分组清理
    /// </summary>
    public sealed class InMemoryLockHandle : IDisposable
    {

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
        private readonly CancellationTokenSource expiryCts;

        /// <summary>
        /// 是否已释放
        /// </summary>
        private int disposed;


        /// <summary>
        /// 创建内存锁句柄并启动过期释放任务
        /// </summary>
        /// <param name="key">锁名称</param>
        /// <param name="group">锁分组</param>
        /// <param name="slot">占用的信号量槽位</param>
        /// <param name="expiry">失效时长</param>
        public InMemoryLockHandle(string key, LockGroup group, SemaphoreSlim slot, TimeSpan expiry)
        {
            this.key = key;
            this.group = group;
            this.slot = slot;

            expiryCts = new CancellationTokenSource();
            _ = ExpireAsync(expiry, expiryCts.Token);
        }


        /// <summary>
        /// 释放锁
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0)
            {
                return;
            }

            try
            {
                expiryCts.Cancel();
            }
            catch
            {
            }
            finally
            {
                expiryCts.Dispose();
            }

            try
            {
                slot.Release();
            }
            catch
            {
            }

            if (Interlocked.Decrement(ref group.ActiveHandles) == 0)
            {
                InMemoryLock.TryRemoveGroup(key, group);
            }
        }


        /// <summary>
        /// 到期后自动释放
        /// </summary>
        /// <param name="expiry">失效时长</param>
        /// <param name="cancellationToken">取消令牌</param>
        private async Task ExpireAsync(TimeSpan expiry, CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(expiry, cancellationToken);
            }
            catch (TaskCanceledException)
            {
                return;
            }
            catch
            {
                return;
            }

            Dispose();
        }
    }
}
