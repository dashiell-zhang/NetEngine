namespace TaskService.Libraries.ScheduleTask
{
    [AttributeUsage(AttributeTargets.Method)]
    public class ScheduleTaskAttribute : Attribute
    {

        /// <summary>
        /// Cron 表达式
        /// </summary>
        public string Cron { get; set; }
    }
}
