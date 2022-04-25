using Common.RedisLock.Core.Internal;
using StackExchange.Redis;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Common.RedisLock.RedLock
{
    internal interface IRedLockExtensibleSynchronizationPrimitive : IRedLockReleasableSynchronizationPrimitive
    {
        TimeoutValue AcquireTimeout { get; }
        Task<bool> TryExtendAsync(IDatabaseAsync database);
    }

    /// <summary>
    /// 实现RedLock算法中的扩展操作。 见 https://redis.io/topics/distlock
    /// </summary>
    internal readonly struct RedLockExtend
    {
        private readonly IRedLockExtensibleSynchronizationPrimitive _primitive;
        private readonly Dictionary<IDatabase, Task<bool>> _tryAcquireOrRenewTasks;
        private readonly CancellationToken _cancellationToken;

        public RedLockExtend(
            IRedLockExtensibleSynchronizationPrimitive primitive,
            Dictionary<IDatabase, Task<bool>> tryAcquireOrRenewTasks,
            CancellationToken cancellationToken)
        {
            this._primitive = primitive;
            this._tryAcquireOrRenewTasks = tryAcquireOrRenewTasks;
            this._cancellationToken = cancellationToken;
        }

        public async Task<bool?> TryExtendAsync()
        {
            Invariant.Require(!SyncViaAsync.IsSynchronous, "should only be called from a background renewal thread which is async");

            var incompleteTasks = new HashSet<Task>();
            foreach (var kvp in this._tryAcquireOrRenewTasks.ToArray())
            {
                if (kvp.Value.IsCompleted)
                {
                    incompleteTasks.Add(
                        this._tryAcquireOrRenewTasks[kvp.Key] = Helpers.SafeCreateTask(
                            state => state.primitive.TryExtendAsync(state.database),
                            (primitive: this._primitive, database: kvp.Key)
                        )
                    );
                }
                else
                {
                    // 如果之前的获取/续订仍在进行，请继续等待
                    incompleteTasks.Add(kvp.Value);
                }
            }

            // 对于扩展，我们使用与获取相同的超时。 这确保了相同的最小有效时间，这应该是
            // 足以继续扩展
            using var timeout = new RedLockTimeoutTask(this._primitive.AcquireTimeout, this._cancellationToken);
            incompleteTasks.Add(timeout.Task);

            var databaseCount = this._tryAcquireOrRenewTasks.Count;
            var successCount = 0;
            var failCount = 0;
            while (true)
            {
                var completed = await Task.WhenAny(incompleteTasks).ConfigureAwait(false);

                if (completed == timeout.Task)
                {
                    await completed.ConfigureAwait(false); // 传播抵消
                    return null; // 不确定的
                }

                if (completed.Status == TaskStatus.RanToCompletion && ((Task<bool>)completed).Result)
                {
                    ++successCount;
                    if (RedLockHelper.HasSufficientSuccesses(successCount, databaseCount)) { return true; }
                }
                else
                {
                    // 请注意，我们在扩展中同样对待错误和失败。 没有理由扔，因为
                    // 这只是由扩展循环调用。 虽然从理论上讲，故障可能表明某种后期成功
                    // 失败，很可能意味着数据库无法访问，因此将其视为失败是最安全的
                    ++failCount;
                    if (RedLockHelper.HasTooManyFailuresOrFaults(failCount, databaseCount)) { return false; }
                }

                incompleteTasks.Remove(completed);
            }
        }
    }
}
