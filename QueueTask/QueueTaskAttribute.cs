namespace QueueTask
{

    [AttributeUsage(AttributeTargets.Method)]
    public class QueueTaskAttribute : Attribute
    {


        /// <summary>
        /// 方法名称
        /// </summary>
        public string Action { get; set; }



        /// <summary>
        /// 并发值
        /// </summary>
        public int Semaphore { get; set; } = 1;

    }
}
