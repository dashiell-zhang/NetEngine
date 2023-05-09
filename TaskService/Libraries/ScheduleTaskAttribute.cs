namespace TaskService.Libraries
{
    [AttributeUsage(AttributeTargets.Method)]
    public class ScheduleTaskAttribute : Attribute
    {
        public string Cron { get; set; }
    }
}
