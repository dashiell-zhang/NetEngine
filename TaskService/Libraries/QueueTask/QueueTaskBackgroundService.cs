using Common;
using DistributedLock;
using Repository.Database;
using System.Collections.Concurrent;
using System.Reflection;
using static TaskService.Libraries.QueueTask.QueueTaskBuilder;

namespace TaskService.Libraries.QueueTask
{
    public class QueueTaskBackgroundService : BackgroundService
    {

        private readonly IServiceProvider serviceProvider;
        private readonly ILogger logger;
        private readonly IDistributedLock distLock;


        private readonly ConcurrentDictionary<long, string> runingTaskList = new();


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

            if (queueMethodList.Any())
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        foreach (var item in queueMethodList.Values)
                        {

                            var runingTaskIdList = runingTaskList.Where(t => t.Value == item.Name).Select(t => t.Key).ToList();

                            int skipSize = runingTaskIdList.Count;

                            int taskSize = item.Semaphore - runingTaskIdList.Count;

                            if (taskSize > 0)
                            {
                                var nowTime = DateTime.UtcNow;

                                var queueTaskIdList = db.TQueueTask.Where(t => t.Name == item.Name && t.SuccessTime == null && runingTaskIdList.Contains(t.Id) == false && t.Count < 3 && (t.LastTime == null || (t.LastTime < nowTime.AddMinutes(-5 * t.Count)))).OrderBy(t => t.Count).ThenBy(t => t.LastTime).ThenBy(t => t.CreateTime).Skip(skipSize).Take(taskSize).Select(t => t.Id).ToList();

                                foreach (var queueTaskId in queueTaskIdList)
                                {
                                    if (runingTaskList.TryAdd(queueTaskId, item.Name))
                                    {
                                        RunAction(item, queueTaskId);
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError($"ExecuteAsync：{ex.Message}");
                    }

                    await Task.Delay(1000, stoppingToken);
                }
            }
        }


        private readonly MethodInfo jsonToParameter = typeof(JsonHelper).GetMethod("JsonToObject", BindingFlags.Static | BindingFlags.Public)!;



        private void RunAction(QueueInfo queueInfo, long queueTaskId)
        {
            Task.Run(() =>
            {
                try
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

                            var parameterType = queueInfo.Method.GetParameters().FirstOrDefault()?.ParameterType;
                            if (parameterType != null)
                            {
                                if (queueTask.Parameter != null)
                                {
                                    var parameter = jsonToParameter.MakeGenericMethod(parameterType).Invoke(null, new object[] { queueTask.Parameter })!;

                                    queueInfo.Method.Invoke(queueInfo.Context, new object[] { parameter });
                                }
                                else
                                {
                                    logger.LogError(queueInfo.Method + "方法要求有参数，但队列任务记录缺少参数");
                                }
                            }
                            else
                            {
                                queueInfo.Method.Invoke(queueInfo.Context, null)?.ToString();
                            }

                            queueTask.SuccessTime = DateTime.UtcNow;

                            db.SaveChanges();

                            runingTaskList.TryRemove(queueTaskId, out _);
                        }
                    }

                }
                catch (Exception ex)
                {
                    logger.LogError($"RunAction-{queueInfo.Name};{queueTaskId};{ex.Message}");
                    runingTaskList.TryRemove(queueTaskId, out _);
                }
            });
        }

    }
}
