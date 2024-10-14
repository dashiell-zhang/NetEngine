namespace TaskService.Core.QueueTask
{

    [AttributeUsage(AttributeTargets.Method)]
    public class QueueTaskAttribute : Attribute
    {


        /// <summary>
        /// 任务名称
        /// </summary>
        public string Name { get; set; }



        /// <summary>
        /// 并发值
        /// </summary>
        public int Semaphore { get; set; }




        /// <summary>
        /// 预期持续时间(单位：分)
        /// </summary>
        public int Duration { get; set; }

    }
}
