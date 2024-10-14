using System.Reflection;

namespace TaskService.Core.ScheduleTask
{


    /// <summary>
    /// 周期性定时任务信息模型
    /// </summary>
    public class ScheduleTaskInfo
    {


        /// <summary>
        /// 任务名称
        /// </summary>
        public string Name { get; set; }



        /// <summary>
        /// Cron 表达式
        /// </summary>
        public string Cron { get; set; }



        /// <summary>
        /// 方法
        /// </summary>
        public MethodInfo Method { get; set; }



        /// <summary>
        /// 最后一次执行时间
        /// </summary>
        public DateTimeOffset? LastTime { get; set; }



        /// <summary>
        /// 是否启用
        /// </summary>
        public bool IsEnable { get; set; }

    }
}
