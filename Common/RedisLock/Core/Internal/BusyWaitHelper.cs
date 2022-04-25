using System;
using System.Threading;
using System.Threading.Tasks;

namespace Common.RedisLock.Core.Internal
{
    internal static class BusyWaitHelper
    {
        public static async ValueTask<TResult?> WaitAsync<TState, TResult>(
            TState state,
            Func<TState, CancellationToken, ValueTask<TResult?>> tryGetValue,
            TimeoutValue timeout,
            TimeoutValue minSleepTime,
            TimeoutValue maxSleepTime,
            CancellationToken cancellationToken)
            where TResult : class
        {
            Invariant.Require(minSleepTime.CompareTo(maxSleepTime) <= 0);
            Invariant.Require(!maxSleepTime.IsInfinite);

            var initialResult = await tryGetValue(state, cancellationToken).ConfigureAwait(false);
            if (initialResult != null || timeout.IsZero)
            {
                return initialResult;
            }

            using var _ = CreateMergedCancellationTokenSourceSource(timeout, cancellationToken, out var mergedCancellationToken);

            var random = new Random(Guid.NewGuid().GetHashCode());
            var sleepRangeMillis = maxSleepTime.InMilliseconds - minSleepTime.InMilliseconds;
            while (true)
            {
                var sleepTime = minSleepTime.TimeSpan + TimeSpan.FromMilliseconds(random.NextDouble() * sleepRangeMillis);
                try
                {
                    await SyncViaAsync.Delay(sleepTime, mergedCancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (IsTimedOut())
                {
                    // 如果我们在睡觉时超时，请始终仅使用常规令牌再尝试一次
                    return await tryGetValue(state, cancellationToken).ConfigureAwait(false);
                }

                try
                {
                    var result = await tryGetValue(state, mergedCancellationToken).ConfigureAwait(false);
                    if (result != null) { return result; }
                }
                catch (OperationCanceledException) when (IsTimedOut())
                {
                    return null;
                }
            }

            bool IsTimedOut() =>
                mergedCancellationToken.IsCancellationRequested && !cancellationToken.IsCancellationRequested;
        }

        private static IDisposable? CreateMergedCancellationTokenSourceSource(TimeoutValue timeout, CancellationToken cancellationToken, out CancellationToken mergedCancellationToken)
        {
            if (timeout.IsInfinite)
            {
                mergedCancellationToken = cancellationToken;
                return null;
            }

            if (!cancellationToken.CanBeCanceled)
            {
                var timeoutSource = new CancellationTokenSource(millisecondsDelay: timeout.InMilliseconds);
                mergedCancellationToken = timeoutSource.Token;
                return timeoutSource;
            }

            var mergedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            mergedSource.CancelAfter(timeout.InMilliseconds);
            mergedCancellationToken = mergedSource.Token;
            return mergedSource;
        }
    }
}
