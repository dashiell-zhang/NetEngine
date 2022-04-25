using Common.RedisLock.Core;
using StackExchange.Redis;
using System;
using System.Collections.Generic;

namespace Common.RedisLock
{
    /// <summary>
    /// 实现 <see cref="IDistributedLockProvider"/> for <see cref="RedisDistributedLock"/>,
    /// <see cref="IDistributedReaderWriterLockProvider"/> for <see cref="RedisDistributedReaderWriterLock"/>,
    /// 和 <see cref="IDistributedSemaphoreProvider"/> 对于 <see cref="RedisDistributedSemaphore"/>。
    /// </summary>
    public sealed class RedisDistributedSynchronizationProvider : IDistributedLockProvider, IDistributedReaderWriterLockProvider, IDistributedSemaphoreProvider
    {
        private readonly IReadOnlyList<IDatabase> _databases;
        private readonly Action<RedisDistributedSynchronizationOptionsBuilder>? _options;

        /// <summary>
        /// 构造一个连接到提供的 <paramref name="database"/> 的 <see cref="RedisDistributedSynchronizationProvider"/>
        /// 并使用提供的 <paramref name="options"/>。
        /// </summary>
        public RedisDistributedSynchronizationProvider(IDatabase database, Action<RedisDistributedSynchronizationOptionsBuilder>? options = null)
            : this(new[] { database ?? throw new ArgumentNullException(nameof(database)) }, options)
        {
        }

        /// <summary>
        /// 构造一个连接到提供的 <paramref name="databases"/> 的 <see cref="RedisDistributedSynchronizationProvider"/>
        /// 并使用提供的 <paramref name="options"/>。
        ///
        /// 注意如果提供了多个<see cref="IDatabase"/>，<see cref="CreateSemaphore(RedisKey, int)"/> 将只使用第一个
        /// <see cref="IDatabase"/>。
        /// </summary>
        public RedisDistributedSynchronizationProvider(IEnumerable<IDatabase> databases, Action<RedisDistributedSynchronizationOptionsBuilder>? options = null)
        {
            this._databases = RedisDistributedLock.ValidateDatabases(databases);
            this._options = options;
        }

        /// <summary>
        /// 使用给定的 <paramref name="key"/> 创建一个 <see cref="RedisDistributedLock"/>。
        /// </summary>
        public RedisDistributedLock CreateLock(RedisKey key) => new RedisDistributedLock(key, this._databases, this._options);

        IDistributedLock IDistributedLockProvider.CreateLock(string name) => this.CreateLock(name);

        /// <summary>
        /// 使用给定的 <paramref name="name"/> 创建一个 <see cref="RedisDistributedReaderWriterLock"/>。
        /// </summary>
        public RedisDistributedReaderWriterLock CreateReaderWriterLock(string name) =>
            new RedisDistributedReaderWriterLock(name, this._databases, this._options);

        IDistributedReaderWriterLock IDistributedReaderWriterLockProvider.CreateReaderWriterLock(string name) =>
            this.CreateReaderWriterLock(name);

        /// <summary>
        /// 使用提供的 <paramref name="key"/> 和 <paramref name="maxCount"/> 创建一个 <see cref="RedisDistributedSemaphore"/>。
        /// </summary>
        public RedisDistributedSemaphore CreateSemaphore(RedisKey key, int maxCount) => new RedisDistributedSemaphore(key, maxCount, this._databases[0], this._options);

        IDistributedSemaphore IDistributedSemaphoreProvider.CreateSemaphore(string name, int maxCount) =>
            this.CreateSemaphore(name, maxCount);
    }
}
