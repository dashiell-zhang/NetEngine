using System.Collections.Concurrent;

namespace DistributedLock.InMemory.Models
{
    /// <summary>
    /// 锁分组
    /// </summary>
    internal sealed class LockGroup
    {

        /// <summary>
        /// 分组生命周期同步对象
        /// </summary>
        internal object SyncRoot { get; } = new();


        /// <summary>
        /// 信号量槽位表
        /// key 为槽位序号
        /// </summary>
        internal ConcurrentDictionary<int, SemaphoreSlim> Slots { get; } = new();


        /// <summary>
        /// 正在获取或持有当前分组的引用数量
        /// </summary>
        internal int ReferenceCount;


        /// <summary>
        /// 当前分组是否已从分组表移除
        /// </summary>
        internal bool Removed;

    }
}
