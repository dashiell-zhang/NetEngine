// 自动生成
namespace Common.RedisLock.Core
{
    /// <summary>
    /// 充当特定类型的 <see cref="IDistributedLock"/> 实例的工厂。 这个界面可能是
    /// 在依赖注入场景中比 <see cref="IDistributedLock"/> 更易于使用。
    /// </summary>
    public interface IDistributedLockProvider
    {
        /// <summary>
        /// 使用给定的 <paramref name="name"/> 构造一个 <see cref="IDistributedLock"/> 实例。
        /// </summary>
        IDistributedLock CreateLock(string name);
    }
}