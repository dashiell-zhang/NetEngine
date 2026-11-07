using TaskService.Core;
using TaskService.Core.QueueTask;
using TaskService.Core.ScheduleTask;

namespace TaskService.Tasks;

/// <summary>
/// 演示任务调度、入队与回调
/// </summary>
public class DemoTask(ILogger<DemoTask> logger, QueueTaskService queueTaskService) : TaskBase
{

    /// <summary>
    /// 定时创建演示队列任务
    /// </summary>
    [ScheduleTask(Name = "ShowTime", Cron = "0/1 * * * * ?")]
    public async Task ShowTime()
    {

        try
        {

            await queueTaskService.CreateSingleAsync("ShowName", "张晓栋" + DateTime.Now.ToString("ssfff"), null, "ShowNameSuccess", null);
            await queueTaskService.CreateSingleAsync("ShowName", "张晓栋" + DateTime.Now.ToString("ssfff"), null, "ShowNameSuccess", null);
            await queueTaskService.CreateSingleAsync("ShowName", "张晓栋" + DateTime.Now.ToString("ssfff"), null, "ShowNameSuccess", null);
            await queueTaskService.CreateSingleAsync("ShowName", "张晓栋" + DateTime.Now.ToString("ssfff"), null, "ShowNameSuccess", null);
            await queueTaskService.CreateSingleAsync("ShowName", "张晓栋" + DateTime.Now.ToString("ssfff"), null, "ShowNameSuccess", null);
            await queueTaskService.CreateSingleAsync("ShowName", "张晓栋" + DateTime.Now.ToString("ssfff"), null, "ShowNameSuccess", null);
            await queueTaskService.CreateSingleAsync("ShowName", "张晓栋" + DateTime.Now.ToString("ssfff"), null, "ShowNameSuccess", null);
            await queueTaskService.CreateSingleAsync("ShowName", "张晓栋" + DateTime.Now.ToString("ssfff"), null, "ShowNameSuccess", null);
            await queueTaskService.CreateSingleAsync("ShowName", "张晓栋" + DateTime.Now.ToString("ssfff"), null, "ShowNameSuccess", null);
            await queueTaskService.CreateSingleAsync("ShowName", "张晓栋" + DateTime.Now.ToString("ssfff"), null, "ShowNameSuccess", null);
            await queueTaskService.CreateSingleAsync("ShowName", "张晓栋" + DateTime.Now.ToString("ssfff"), null, "ShowNameSuccess", null);
            await queueTaskService.CreateSingleAsync("ShowName", "张晓栋" + DateTime.Now.ToString("ssfff"), null, "ShowNameSuccess", null);
            await queueTaskService.CreateSingleAsync("ShowName", "张晓栋" + DateTime.Now.ToString("ssfff"), null, "ShowNameSuccess", null);
            await queueTaskService.CreateSingleAsync("ShowName", "张晓栋" + DateTime.Now.ToString("ssfff"), null, "ShowNameSuccess", null);
            await queueTaskService.CreateSingleAsync("ShowName", "张晓栋" + DateTime.Now.ToString("ssfff"), null, "ShowNameSuccess", null);
            await queueTaskService.CreateSingleAsync("ShowName", "张晓栋" + DateTime.Now.ToString("ssfff"), null, "ShowNameSuccess", null);
            await queueTaskService.CreateSingleAsync("ShowName", "张晓栋" + DateTime.Now.ToString("ssfff"), null, "ShowNameSuccess", null);

        }
        catch (Exception ex)
        {
            logger.LogError(ex, "DemoTask.WriteHello");
        }

    }



    [QueueTask(Name = "ShowName", Semaphore = 5)]
    public async Task<string> ShowName(string name)
    {
        Console.WriteLine(DateTime.Now + "姓名：" + name);

        await queueTaskService.CreateSingleAsync("SendSMS", name, null, null, null, true);

        await queueTaskService.CreateSingleAsync("CallPhone", name);

        return name;
    }



    [QueueTask(Name = "SendSMS", Semaphore = 5)]
    public void SendSMS(string name)
    {
        Console.WriteLine(DateTime.Now + "姓名：" + name + ",短信发送成功");
    }


    [QueueTask(Name = "CallPhone", Semaphore = 5)]
    public void CallPhone(string name)
    {
        Console.WriteLine(DateTime.Now + "姓名：" + name + ",打电话执行成功");
    }


    [QueueTask(Name = "ShowNameSuccess", Semaphore = 5)]
    public void ShowNameSuccess(string name)
    {
        Console.WriteLine(DateTime.Now + "姓名：" + name + ",的所有任务都执行完成了");
    }

}
