namespace TaskService.Libraries
{

    [AttributeUsage(AttributeTargets.Method)]
    public class QueueTaskAttribute : Attribute
    {

        public string Action { get; set; }

    }
}
