using Common.RedisLock.Core.Internal;
using Common.RedisLock.RedLock;
using System;
using System.Threading;

namespace Common.RedisLock
{
    /// <summary>
    /// 用于配置基于 redis 的分布式同步算法的选项
    /// </summary>
    public sealed class RedisDistributedSynchronizationOptionsBuilder
    {
        internal static readonly TimeoutValue DefaultExpiry = TimeSpan.FromSeconds(30);
        /// <summary>
        /// 我们不想让到期时间过低，因为那样锁甚至不起作用（默认
        /// 观察到的最小到期时间最终会大于默认到期时间）
        /// </summary>
        internal static readonly TimeoutValue MinimumExpiry = TimeSpan.FromSeconds(.1);

        private TimeoutValue? _expiry,
            _extensionCadence,
            _minValidityTime,
            _minBusyWaitSleepTime,
            _maxBusyWaitSleepTime;

        internal RedisDistributedSynchronizationOptionsBuilder() { }

        /// <summary>
        /// 指定锁将持续多长时间，没有自动扩展。 因为存在自动扩展，
        /// 这个值一般对程序行为影响不大。 但是，延长到期时间意味着
        /// 自动扩展请求可以不那么频繁地发生，节省资源。 另一方面，当一个锁被放弃时
        /// 没有显式释放（例如，如果持有进程崩溃），到期决定其他进程多长时间
        /// 需要等待才能获得它。
        ///
        /// 默认为 30 秒。
        /// </summary>
        public RedisDistributedSynchronizationOptionsBuilder Expiry(TimeSpan expiry)
        {
            var expiryTimeoutValue = new TimeoutValue(expiry, nameof(expiry));
            if (expiryTimeoutValue.IsInfinite || expiryTimeoutValue.CompareTo(MinimumExpiry) < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(expiry), expiry, $"Must be >= {MinimumExpiry.TimeSpan} and < ∞");
            }
            this._expiry = expiryTimeoutValue;
            return this;
        }

        /// <summary>
        /// 确定锁在持有时扩展的频率。 更频繁的扩展意味着更多不必要的请求
        /// 但由于进程挂起或无法获取其扩展请求而丢失锁的可能性也较低
        /// 在锁到期之前。
        ///
        /// 默认为指定 <see cref="MinValidityTime(TimeSpan)"/> 的 1/3。
        /// </summary>
        public RedisDistributedSynchronizationOptionsBuilder ExtensionCadence(TimeSpan extensionCadence)
        {
            this._extensionCadence = new TimeoutValue(extensionCadence, nameof(extensionCadence));
            return this;
        }

        /// <summary>
        /// 锁到期决定了锁将被持有多长时间而不被延长。 但是，由于它需要一些
        /// 获取锁的时间，我们不会在获取时拥有所有可用的到期时间。
        ///
        /// 这个值设置了一个最小数量，一旦采集完成，我们将保证剩下的数量。
        ///
        /// 默认为指定锁到期的 90%。
        /// </summary>
        public RedisDistributedSynchronizationOptionsBuilder MinValidityTime(TimeSpan minValidityTime)
        {
            var minValidityTimeoutValue = new TimeoutValue(minValidityTime, nameof(minValidityTime));
            if (minValidityTimeoutValue.IsZero)
            {
                throw new ArgumentOutOfRangeException(nameof(minValidityTime), minValidityTime, "may not be zero");
            }
            this._minValidityTime = minValidityTimeoutValue;
            return this;
        }

        /// <summary>
        /// 等待获取锁需要一个繁忙的等待，交替获取尝试和休眠。
        /// 这决定了两次尝试之间的睡眠时间。 较低的值将提高
        /// 竞争中获取请求的数量，但也会提高响应能力（多长时间
        /// 等待服务员注意到竞争锁已可用）。
        ///
        /// 指定一个值范围允许实现在该范围内选择一个实际值
        /// 每次睡眠随机。 这有助于避免两个客户端“同步”的情况
        /// 导致一个客户端独占锁。
        ///
        /// 默认是[10ms, 800ms]
        /// </summary>
        public RedisDistributedSynchronizationOptionsBuilder BusyWaitSleepTime(TimeSpan min, TimeSpan max)
        {
            var minTimeoutValue = new TimeoutValue(min, nameof(min));
            var maxTimeoutValue = new TimeoutValue(max, nameof(max));

            if (minTimeoutValue.IsInfinite) { throw new ArgumentOutOfRangeException(nameof(min), "may not be infinite"); }
            if (maxTimeoutValue.IsInfinite || maxTimeoutValue.CompareTo(min) < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(max), max, "must be non-infinite and greater than " + nameof(min));
            }

            this._minBusyWaitSleepTime = minTimeoutValue;
            this._maxBusyWaitSleepTime = maxTimeoutValue;
            return this;
        }

        internal static RedisDistributedLockOptions GetOptions(Action<RedisDistributedSynchronizationOptionsBuilder>? optionsBuilder)
        {
            RedisDistributedSynchronizationOptionsBuilder? options;
            if (optionsBuilder != null)
            {
                options = new RedisDistributedSynchronizationOptionsBuilder();
                optionsBuilder(options);
            }
            else
            {
                options = null;
            }

            var expiry = options?._expiry ?? DefaultExpiry;

            TimeoutValue minValidityTime;
            if (options?._minValidityTime is { } specifiedMinValidityTime)
            {
                if (specifiedMinValidityTime.CompareTo(expiry) >= 0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(minValidityTime),
                        specifiedMinValidityTime.TimeSpan,
                        $"{nameof(minValidityTime)} must be less than {nameof(expiry)} ({expiry.TimeSpan})"
                    );
                }
                minValidityTime = specifiedMinValidityTime;
            }
            else
            {
                minValidityTime = TimeSpan.FromMilliseconds(Math.Max(0.9 * expiry.InMilliseconds, 1));
            }

            TimeoutValue extensionCadence;
            if (options?._extensionCadence is { } specifiedExtensionCadence)
            {
                if (specifiedExtensionCadence.CompareTo(minValidityTime) >= 0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(extensionCadence),
                        specifiedExtensionCadence.TimeSpan,
                        $"{nameof(extensionCadence)} must be less than {nameof(expiry)} ({expiry.TimeSpan}). To disable auto-extension, specify {nameof(Timeout)}.{nameof(Timeout.InfiniteTimeSpan)}"
                    );
                }
                extensionCadence = specifiedExtensionCadence;
            }
            else
            {
                extensionCadence = TimeSpan.FromMilliseconds(minValidityTime.InMilliseconds / 3.0);
            }

            return new RedisDistributedLockOptions(
                redLockTimeouts: new RedLockTimeouts(expiry: expiry, minValidityTime: minValidityTime),
                extensionCadence: extensionCadence,
                minBusyWaitSleepTime: options?._minBusyWaitSleepTime ?? TimeSpan.FromMilliseconds(10),
                maxBusyWaitSleepTime: options?._maxBusyWaitSleepTime ?? TimeSpan.FromSeconds(0.8)
            );
        }
    }

    internal readonly struct RedisDistributedLockOptions
    {
        public RedisDistributedLockOptions(
            RedLockTimeouts redLockTimeouts,
            TimeoutValue extensionCadence,
            TimeoutValue minBusyWaitSleepTime,
            TimeoutValue maxBusyWaitSleepTime)
        {
            this.RedLockTimeouts = redLockTimeouts;
            this.ExtensionCadence = extensionCadence;
            this.MinBusyWaitSleepTime = minBusyWaitSleepTime;
            this.MaxBusyWaitSleepTime = maxBusyWaitSleepTime;
        }

        public RedLockTimeouts RedLockTimeouts { get; }
        public TimeoutValue ExtensionCadence { get; }
        public TimeoutValue MinBusyWaitSleepTime { get; }
        public TimeoutValue MaxBusyWaitSleepTime { get; }
    }
}
