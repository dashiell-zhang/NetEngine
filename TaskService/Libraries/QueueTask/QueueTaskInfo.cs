using System.Reflection;

namespace TaskService.Libraries.QueueTask
{

    /// <summary>
    /// 队列任务信息模型
    /// </summary>
    public class QueueTaskInfo
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
        /// 方法
        /// </summary>
        public MethodInfo Method { get; set; }



        /// <summary>
        /// 方法执行的上下文环境
        /// </summary>
        public object Context { get; set; }
    }
}
