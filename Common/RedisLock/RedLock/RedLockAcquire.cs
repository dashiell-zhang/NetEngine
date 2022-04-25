using Common.RedisLock.Core.Internal;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Common.RedisLock.RedLock
{
    internal interface IRedLockAcquirableSynchronizationPrimitive : IRedLockReleasableSynchronizationPrimitive
    {
        TimeoutValue AcquireTimeout { get; }
        Task<bool> TryAcquireAsync(IDatabaseAsync database);
        bool TryAcquire(IDatabase database);
    }

    /// <summary>
    /// 实现 RedLock 算法中的获取操作。 见 https://redis.io/topics/distlock
    /// </summary>
    internal readonly struct RedLockAcquire
    {
        private readonly IRedLockAcquirableSynchronizationPrimitive _primitive;
        private readonly IReadOnlyList<IDatabase> _databases;
        private readonly CancellationToken _cancellationToken;

        public RedLockAcquire(
            IRedLockAcquirableSynchronizationPrimitive primitive,
            IReadOnlyList<IDatabase> databases,
            CancellationToken cancellationToken)
        {
            this._primitive = primitive;
            this._databases = databases;
            this._cancellationToken = cancellationToken;
        }

        public async ValueTask<Dictionary<IDatabase, Task<bool>>?> TryAcquireAsync()
        {
            this._cancellationToken.ThrowIfCancellationRequested();

            var isSynchronous = SyncViaAsync.IsSynchronous;
            if (isSynchronous && this._databases.Count == 1)
            {
                return this.TrySingleFullySynchronousAcquire();
            }

            var primitive = this._primitive;
            var tryAcquireTasks = this._databases.ToDictionary(
                db => db,
                db => Helpers.SafeCreateTask(state => state.primitive.TryAcquireAsync(state.db), (primitive, db))
            );

            var waitForAcquireTask = this.WaitForAcquireAsync(tryAcquireTasks);

            var succeeded = false;
            try
            {
                succeeded = await waitForAcquireTask.AwaitSyncOverAsync().ConfigureAwait(false);
            }
            finally
            {
                // 清理
                if (!succeeded)
                {
                    List<Task>? releaseTasks = null;
                    foreach (var kvp in tryAcquireTasks)
                    {
                        // 如果任务还没有完成，我们现在不想做任何释放； 只是
                        // 在任务最终完成时排队一个要运行的释放命令
                        if (!kvp.Value.IsCompleted)
                        {
                            RedLockHelper.FireAndForgetReleaseUponCompletion(primitive, kvp.Key, kvp.Value);
                        }
                        // 否则，除非我们知道获取失败，否则释放
                        else if (!RedLockHelper.ReturnedFalse(kvp.Value))
                        {
                            if (isSynchronous)
                            {
                                try { primitive.Release(kvp.Key, fireAndForget: true); }
                                catch { }
                            }
                            else
                            {
                                (releaseTasks ??= new List<Task>())
                                    .Add(Helpers.SafeCreateTask(state => state.primitive.ReleaseAsync(state.Key, fireAndForget: true), (primitive, kvp.Key)));
                            }
                        }
                    }

                    if (releaseTasks != null)
                    {
                        await Task.WhenAll(releaseTasks).ConfigureAwait(false);
                    }
                }
            }

            return succeeded ? tryAcquireTasks : null;
        }

        private async Task<bool> WaitForAcquireAsync(IReadOnlyDictionary<IDatabase, Task<bool>> tryAcquireTasks)
        {
            using var timeout = new RedLockTimeoutTask(this._primitive.AcquireTimeout, this._cancellationToken);
            var incompleteTasks = new HashSet<Task>(tryAcquireTasks.Values) { timeout.Task };

            var successCount = 0;
            var failCount = 0;
            var faultCount = 0;
            while (true)
            {
                var completed = await Task.WhenAny(incompleteTasks).ConfigureAwait(false);

                if (completed == timeout.Task)
                {
                    await completed.ConfigureAwait(false); // 传播取消
                    return false; // 真正的超时
                }

                if (completed.Status == TaskStatus.RanToCompletion)
                {
                    var result = await ((Task<bool>)completed).ConfigureAwait(false);
                    if (result)
                    {
                        ++successCount;
                        if (RedLockHelper.HasSufficientSuccesses(successCount, this._databases.Count)) { return true; }
                    }
                    else
                    {
                        ++failCount;
                        if (RedLockHelper.HasTooManyFailuresOrFaults(failCount, this._databases.Count)) { return false; }
                    }
                }
                else // 故障或取消
                {
                    // 如果我们得到太多错误，锁是不可能获取的，所以我们应该抛出
                    ++faultCount;
                    if (RedLockHelper.HasTooManyFailuresOrFaults(faultCount, this._databases.Count))
                    {
                        var faultingTasks = tryAcquireTasks.Values.Where(t => t.IsCanceled || t.IsFaulted)
                            .ToArray();
                        if (faultingTasks.Length == 1)
                        {
                            await faultingTasks[0].ConfigureAwait(false); // 传播错误
                        }

                        throw new AggregateException(faultingTasks.Select(t => t.Exception ?? new TaskCanceledException(t).As<Exception>()))
                            .Flatten();
                    }

                    ++failCount;
                    if (RedLockHelper.HasTooManyFailuresOrFaults(failCount, this._databases.Count)) { return false; }
                }

                incompleteTasks.Remove(completed);
                Invariant.Require(incompleteTasks.Count > 1, "should be more than just timeout left");
            }
        }

        /// <summary>
        /// 我们只允许对单个数据库进行同步获取，因为 StackExchange.Redis 目前不允许
        /// 单次操作超时/取消。 因此，一个缓慢的反应将危及我们声称
        /// 锁定时间。 对于单个数据库，一个操作就是最重要的，所以如果我们需要等待它就可以了。
        /// </summary>
        private Dictionary<IDatabase, Task<bool>>? TrySingleFullySynchronousAcquire()
        {
            var database = this._databases.Single();

            bool success;
            var stopwatch = Stopwatch.StartNew();
            try { success = this._primitive.TryAcquire(database); }
            catch
            {
                // 失败时，仍然尝试释放以防万一
                try { this._primitive.Release(database, fireAndForget: true); }
                catch { } // 没做什么; 我们无论如何都要扔，失败的原因可能是一样的

                throw;
            }

            if (success)
            {
                // 确保我们没有超时
                if (this._primitive.AcquireTimeout.CompareTo(stopwatch.Elapsed) >= 0)
                {
                    return new Dictionary<IDatabase, Task<bool>> { [database] = Task.FromResult(success) };
                }

                this._primitive.Release(database, fireAndForget: true); // 超时，所以释放
            }

            return null;
        }
    }
}
