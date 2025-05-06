using Repository.Database;
using TaskService.Core;
using TaskService.Core.QueueTask;
using TaskService.Core.ScheduleTask;

namespace TaskService.Tasks
{

    public class DemoTask(ILogger<DemoTask> logger, DatabaseContext db, QueueTaskService queueTaskService) : TaskBase
    {

        [ScheduleTask(Name = "ShowTime", Cron = "0/3 * * * * ?")]
        public async Task ShowTime()
        {
            try
            {

                await queueTaskService.CreateSingleAsync("ShowName", "张晓栋" + DateTime.Now.ToString("ssfff"), null, "ShowNameSuccess", null);

                var firstUser = db.TUser.FirstOrDefault();

            }
            catch (Exception ex)
            {
                logger.LogError(ex, "DemoTask.WriteHello");
            }
        }



        [QueueTask(Name = "ShowName", Semaphore = 5, Duration = 5)]
        public async Task<string> ShowName(string name)
        {
            Console.WriteLine(DateTime.Now + "姓名：" + name);

            await queueTaskService.CreateSingleAsync("SendEmail", name, null, null, null, true);
            await queueTaskService.CreateSingleAsync("SendSMS", name, null, null, null, true);

            await queueTaskService.CreateSingleAsync("CallPhone", name);

            return name;
        }


        [QueueTask(Name = "SendEmail", Semaphore = 5, Duration = 5)]
        public async Task SendEmail(string name)
        {
            Console.WriteLine(DateTime.Now + "姓名：" + name + ",邮件发送成功");

            await queueTaskService.CreateSingleAsync("ClearEmail", name, null, null, null, true);

        }


        [QueueTask(Name = "ClearEmail", Semaphore = 5, Duration = 5)]
        public void ClearEmail(string name)
        {

            Thread.Sleep(5000);

            Console.WriteLine(DateTime.Now + "姓名：" + name + ",邮件清理成功");
        }


        [QueueTask(Name = "SendSMS", Semaphore = 5, Duration = 5)]
        public void SendSMS(string name)
        {
            Console.WriteLine(DateTime.Now + "姓名：" + name + ",短信发送成功");
        }


        [QueueTask(Name = "CallPhone", Semaphore = 5, Duration = 5)]
        public void CallPhone(string name)
        {
            Console.WriteLine(DateTime.Now + "姓名：" + name + ",打电话执行成功");
        }


        [QueueTask(Name = "ShowNameSuccess", Semaphore = 5, Duration = 5)]
        public void ShowNameSuccess(string name)
        {
            Console.WriteLine(DateTime.Now + "姓名：" + name + ",的所有任务都执行完成了");
        }

    }
}
