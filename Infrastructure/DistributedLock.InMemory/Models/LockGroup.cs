using System.Collections.Concurrent;

namespace DistributedLock.InMemory.Models
{
    /// <summary>
    /// 锁分组
    /// </summary>
    public sealed class LockGroup
    {
        /// <summary>
        /// 信号量槽位表
        /// key 为槽位序号
        /// </summary>
        public ConcurrentDictionary<int, SemaphoreSlim> Slots { get; } = new();


        /// <summary>
        /// 活跃句柄数量
        /// </summary>
        public int ActiveHandles;
    }
}
