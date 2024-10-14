namespace TaskService.Core.ScheduleTask
{
    [AttributeUsage(AttributeTargets.Method)]
    public class ScheduleTaskAttribute : Attribute
    {

        /// <summary>
        /// 任务名称
        /// </summary>
        public string Name { get; set; }


        /// <summary>
        /// Cron 表达式
        /// </summary>
        public string Cron { get; set; }
    }
}
