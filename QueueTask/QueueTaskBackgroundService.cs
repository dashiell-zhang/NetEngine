using DistributedLock;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Repository.Database;
using static QueueTask.QueueTaskBuilder;

namespace QueueTask
{
    public class QueueTaskBackgroundService : BackgroundService
    {

        private readonly IServiceProvider serviceProvider;
        private readonly ILogger logger;
        private readonly IDistributedLock distLock;


        private readonly Dictionary<long, string> runingTaskList = new();


        public QueueTaskBackgroundService(IServiceProvider serviceProvider, ILogger<QueueTaskBackgroundService> logger, IDistributedLock distLock)
        {
            this.serviceProvider = serviceProvider;
            this.logger = logger;
            this.distLock = distLock;
        }


        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var scope = serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

            if (QueueTaskBuilder.queueActionList.Any())
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        foreach (var item in QueueTaskBuilder.queueActionList.Values)
                        {

                            var runingTaskIdList = runingTaskList.Where(t => t.Value == item.Name).Select(t => t.Key).ToList();

                            int skipSize = runingTaskIdList.Count;

                            int taskSize = item.Semaphore - runingTaskIdList.Count;

                            if (taskSize > 0)
                            {
                                var queueTaskIdList = db.TQueueTask.Where(t => t.Action == item.Name && t.SuccessTime == null && runingTaskIdList.Contains(t.Id) == false && t.Count < 3).OrderBy(t => t.CreateTime).Skip(skipSize).Take(taskSize).Select(t => t.Id).ToList();

                                foreach (var queueTaskId in queueTaskIdList)
                                {
                                    if (runingTaskIdList.Contains(queueTaskId) == false)
                                    {
                                        runingTaskList.Add(queueTaskId, item.Name);
                                        RunAction(item, queueTaskId);
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                    }

                    await Task.Delay(1000, stoppingToken);
                }
            }
        }




        private void RunAction(QueueInfo queueInfo, long queueTaskId)
        {
            try
            {
                Task.Run(() =>
                {
                    using var scope = serviceProvider.CreateScope();

                    var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

                    using (var lockActionState = distLock.TryLock(queueInfo.Name, TimeSpan.FromMinutes(5), queueInfo.Semaphore))
                    {
                        if (lockActionState != null)
                        {
                            var queueTask = db.TQueueTask.FirstOrDefault(t => t.Id == queueTaskId)!;

                            if (queueTask.FirstTime == null)
                            {
                                queueTask.FirstTime = DateTime.UtcNow;
                            }

                            queueTask.LastTime = DateTime.UtcNow;

                            db.SaveChanges();

                            queueTask.Count++;

                            try
                            {

                                var parameterType = queueInfo.Action.GetParameters().FirstOrDefault()?.ParameterType;
                                if (parameterType != null)
                                {
                                    if (queueTask.Parameter != null)
                                    {

                                        var parameter = QueueTaskBuilder.jsonToParameter.MakeGenericMethod(parameterType).Invoke(null, new object[] { queueTask.Parameter })!;

                                        queueInfo.Action.Invoke(queueInfo.Context, new object[] { parameter });
                                    }
                                    else
                                    {
                                        logger.LogError(queueInfo.Action + "方法要求有参数，但队列任务记录缺少参数");
                                    }
                                }
                                else
                                {
                                    queueInfo.Action.Invoke(queueInfo.Context, null)?.ToString();
                                }

                                queueTask.SuccessTime = DateTime.UtcNow;
                            }
                            catch
                            {
                            }

                            runingTaskList.Remove(queueTaskId);

                            db.SaveChanges();
                        }
                    }
                });

            }
            catch (Exception ex)
            {
                logger.LogError($"Action:{queueInfo.Name};Error: {ex.Message}");

                runingTaskList.Remove(queueTaskId);
            }
        }

    }
}
