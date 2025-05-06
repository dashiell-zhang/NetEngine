namespace Application.Core.Interfaces.Basic
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
        bool Create(string name, object? parameter, DateTimeOffset? planTime = null, string? callbackName = null, object? callbackParameter = null);



        /// <summary>
        /// 单独创建队列
        /// </summary>
        /// <param name="name"></param>
        /// <param name="parameter"></param>
        /// <param name="planTime"></param>
        /// <param name="callbackName"></param>
        /// <param name="callbackParameter"></param>
        /// <returns></returns>
        Task<bool> CreateSingleAsync(string name, object? parameter, DateTimeOffset? planTime = null, string? callbackName = null, object? callbackParameter = null);

    }
}
