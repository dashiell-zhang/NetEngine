namespace TaskService.Libraries.ScheduleTask
{
    [AttributeUsage(AttributeTargets.Method)]
    public class ScheduleTaskAttribute : Attribute
    {
        public string Cron { get; set; }
    }
}
