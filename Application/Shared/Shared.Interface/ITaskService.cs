namespace Shared.Interface
{
    public interface ITaskService
    {


        /// <summary>
        /// 创建队列
        /// </summary>
        /// <param name="name"></param>
        /// <param name="parameter"></param>
        /// <param name="planTime"></param>
        /// <param name="callbackName"></param>
        /// <param name="callbackParameter"></param>
        /// <remarks>需要外部开启事务</remarks>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public bool Create(string name, object? parameter, DateTimeOffset? planTime = null, string? callbackName = null, object? callbackParameter = null);



        /// <summary>
        /// 单独创建队列
        /// </summary>
        /// <param name="name"></param>
        /// <param name="parameter"></param>
        /// <param name="planTime"></param>
        /// <param name="callbackName"></param>
        /// <param name="callbackParameter"></param>
        /// <returns></returns>
        public bool CreateSingle(string name, object? parameter, DateTimeOffset? planTime = null, string? callbackName = null, object? callbackParameter = null);

    }
}
