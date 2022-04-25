// 自动生成
namespace Common.RedisLock.Core
{
    /// <summary>
    /// 充当特定类型的 <see cref="IDistributedReaderWriterLock"/> 实例的工厂。 这个界面可能是
    /// 在依赖注入场景中比 <see cref="IDistributedReaderWriterLock"/> 更易于使用。
    /// </summary>
    public interface IDistributedReaderWriterLockProvider
    {
        /// <summary>
        /// 使用给定的 <paramref name="name"/> 构造一个 <see cref="IDistributedReaderWriterLock"/> 实例。
        /// </summary>
        IDistributedReaderWriterLock CreateReaderWriterLock(string name);
    }
}