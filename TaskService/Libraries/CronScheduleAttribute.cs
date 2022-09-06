namespace TaskService.Libraries
{
    [AttributeUsage(AttributeTargets.Method)]
    public class CronScheduleAttribute : Attribute
    {
        public string Cron { get; set; }
    }
}
