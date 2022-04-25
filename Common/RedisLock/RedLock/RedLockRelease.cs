using Common.RedisLock.Core.Internal;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Common.RedisLock.RedLock
{
    internal interface IRedLockReleasableSynchronizationPrimitive
    {
        Task ReleaseAsync(IDatabaseAsync database, bool fireAndForget);
        void Release(IDatabase database, bool fireAndForget);
    }

    /// <summary>
    /// 实现 RedLock 算法中的释放操作。 见 https://redis.io/topics/distlock
    /// </summary>
    internal readonly struct RedLockRelease
    {
        private readonly IRedLockReleasableSynchronizationPrimitive _primitive;
        private readonly IReadOnlyDictionary<IDatabase, Task<bool>> _tryAcquireOrRenewTasks;

        public RedLockRelease(
            IRedLockReleasableSynchronizationPrimitive primitive,
            IReadOnlyDictionary<IDatabase, Task<bool>> tryAcquireOrRenewTasks)
        {
            this._primitive = primitive;
            this._tryAcquireOrRenewTasks = tryAcquireOrRenewTasks;
        }

        public async ValueTask ReleaseAsync()
        {
            var isSynchronous = SyncViaAsync.IsSynchronous;
            var unreleasedTryAcquireOrRenewTasks = this._tryAcquireOrRenewTasks.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            List<Exception>? releaseExceptions = null;
            var successCount = 0;
            var faultCount = 0;
            var databaseCount = unreleasedTryAcquireOrRenewTasks.Count;

            try
            {
                while (true)
                {
                    var releaseableDatabases = unreleasedTryAcquireOrRenewTasks.Where(kvp => kvp.Value.IsCompleted)
                        // 首先完成非错误任务
                        .OrderByDescending(kvp => kvp.Value.Status == TaskStatus.RanToCompletion)
                        // 然后从失败开始，因为不需要任何操作来释放这些
                        .ThenBy(kvp => kvp.Value.Status == TaskStatus.RanToCompletion && kvp.Value.Result)
                        .Select(kvp => kvp.Key)
                        .ToArray();
                    foreach (var db in releaseableDatabases)
                    {
                        var tryAcquireOrRenewTask = unreleasedTryAcquireOrRenewTasks[db];
                        unreleasedTryAcquireOrRenewTasks.Remove(db);

                        if (RedLockHelper.ReturnedFalse(tryAcquireOrRenewTask))
                        {
                            // 如果我们未能获取，我们不需要释放
                            ++successCount;
                        }
                        else
                        {
                            try
                            {
                                if (isSynchronous) { this._primitive.Release(db, fireAndForget: false); }
                                else { await this._primitive.ReleaseAsync(db, fireAndForget: false).ConfigureAwait(false); }
                                ++successCount;
                            }
                            catch (Exception ex)
                            {
                                (releaseExceptions ??= new List<Exception>()).Add(ex);
                                ++faultCount;
                                if (RedLockHelper.HasTooManyFailuresOrFaults(faultCount, databaseCount))
                                {
                                    throw new AggregateException(releaseExceptions!).Flatten();
                                }
                            }
                        }

                        if (RedLockHelper.HasSufficientSuccesses(successCount, databaseCount))
                        {
                            return;
                        }
                    }

                    // 如果我们还没有发布足够多的内容来完成或确定成功或失败，请等待另一个完成
                    if (isSynchronous) { Task.WaitAny(unreleasedTryAcquireOrRenewTasks.Values.ToArray()); }
                    else { await Task.WhenAny(unreleasedTryAcquireOrRenewTasks.Values).ConfigureAwait(false); }
                }
            }
            finally // 开火，忘记其余的
            {
                foreach (var kvp in unreleasedTryAcquireOrRenewTasks)
                {
                    RedLockHelper.FireAndForgetReleaseUponCompletion(this._primitive, kvp.Key, kvp.Value);
                }
            }
        }
    }
}
