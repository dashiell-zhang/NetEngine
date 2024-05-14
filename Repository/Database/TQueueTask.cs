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
        /// 计划执行时间
        /// </summary>
        public DateTimeOffset? PlanTime { set; get; }



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



        /// <summary>
        /// 回调任务名称
        /// </summary>
        public string? CallbackName { get; set; }



        /// <summary>
        /// 回调参数
        /// </summary>
        public string? CallbackParameter { get; set; }


        //public string? Sign { get; set; }


        //public string CallbackSign { get; set; }



        //public string Sort { get; set; }


        //public bool IsActivating { get; set; }


        /// <summary>
        /// 父级任务Id
        /// </summary>
        public long? ParentTaskId { get; set; }
        public virtual TQueueTask ParentTask { get; set; }


        /// <summary>
        /// 子集全部执行成功时间
        /// </summary>
        public DateTimeOffset? ChildSuccessTime { get; set; }

    }
}
