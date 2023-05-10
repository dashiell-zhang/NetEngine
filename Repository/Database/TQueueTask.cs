using Repository.Bases;

namespace Repository.Database
{

    /// <summary>
    /// 队列任务表
    /// </summary>
    public class TQueueTask : CD
    {

        /// <summary>
        /// 任务名称
        /// </summary>
        public string Name { get; set; }



        /// <summary>
        /// 参数
        /// </summary>
        public string? Parameter { get; set; }



        /// <summary>
        /// 执行次数
        /// </summary>
        public int Count { get; set; }



        /// <summary>
        /// 首次执行时间
        /// </summary>
        public DateTimeOffset? FirstTime { get; set; }



        /// <summary>
        /// 最后一次执行时间
        /// </summary>
        public DateTimeOffset? LastTime { get; set; }



        /// <summary>
        /// 成功执行时间
        /// </summary>
        public DateTimeOffset? SuccessTime { get; set; }

    }
}
