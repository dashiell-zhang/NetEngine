using Common.RedisLock.Core.Internal;
using StackExchange.Redis;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Common.RedisLock.RedLock
{
    internal static class RedLockHelper
    {
        private static readonly string LockIdPrefix;

        static RedLockHelper()
        {
            using var currentProcess = Process.GetCurrentProcess();
            LockIdPrefix = $"{Environment.MachineName}_{currentProcess.Id}_";
        }

        public static bool HasSufficientSuccesses(int successCount, int databaseCount)
        {
            // 需要多数
            var threshold = (databaseCount / 2) + 1;
            // 虽然从理论上讲，如果我们有足够多的东西，这应该返回 true，但我们从不期望这是
            // 由于我们如何实现我们的方法，调用 except 只是足够或不够。
            Invariant.Require(successCount <= threshold);
            return successCount >= threshold;
        }

        public static bool HasTooManyFailuresOrFaults(int failureOrFaultCount, int databaseCount)
        {
            // 对于奇数个数据库，我们需要多数才能使成功变得不可能。 为
            // 偶数，然而，达到 50% 的失败/故障足以排除得到
            // 大部分成功。
            var threshold = (databaseCount / 2) + (databaseCount % 2);
            // 虽然理论上如果我们有足够多的情况这应该返回 true，但我们从不期望这是
            // 由于我们如何实现我们的方法，调用 except 只是足够或不够。
            Invariant.Require(failureOrFaultCount <= threshold);
            return failureOrFaultCount >= threshold;
        }

        public static RedisValue CreateLockId() => LockIdPrefix + Guid.NewGuid().ToString("n");

        public static bool ReturnedFalse(Task<bool> task) => task.Status == TaskStatus.RanToCompletion && !task.Result;

        public static void FireAndForgetReleaseUponCompletion(IRedLockReleasableSynchronizationPrimitive primitive, IDatabase database, Task<bool> acquireOrRenewTask)
        {
            if (ReturnedFalse(acquireOrRenewTask)) { return; }

            acquireOrRenewTask.ContinueWith(async (t, state) =>
                {
                    // 如果我们知道我们失败了就不清理
                    if (!ReturnedFalse(t))
                    {
                        await primitive.ReleaseAsync((IDatabase)state!, fireAndForget: true).ConfigureAwait(false);
                    }
                },
                state: database
            );
        }

        public static CommandFlags GetCommandFlags(bool fireAndForget) =>
            CommandFlags.DemandMaster | (fireAndForget ? CommandFlags.FireAndForget : CommandFlags.None);

        public static async Task<bool> AsBooleanTask(this Task<RedisResult> redisResultTask) => (bool)await redisResultTask.ConfigureAwait(false);
    }
}
