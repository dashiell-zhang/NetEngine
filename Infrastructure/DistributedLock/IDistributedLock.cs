namespace DistributedLock
{
    public interface IDistributedLock
    {



        /// <summary>
        /// 获取锁
        /// </summary>
        /// <param name="key">锁的名称，不可重复</param>
        /// <param name="expiry">失效时长</param>
        /// <param name="semaphore">信号量</param>
        /// <returns></returns>
        public IDisposable Lock(string key, TimeSpan expiry = default, int semaphore = 1);



        /// <summary>
        /// 尝试获取锁
        /// </summary>
        /// <param name="key">锁的名称，不可重复</param>
        /// <param name="expiry">失效时长</param>
        /// <param name="semaphore">信号量</param>
        /// <returns></returns>
        public IDisposable? TryLock(string key, TimeSpan expiry = default, int semaphore = 1);




    }
}
