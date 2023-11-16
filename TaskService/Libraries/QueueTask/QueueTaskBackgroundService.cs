using Common;
using DistributedLock;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Repository.Database;
using System.Collections.Concurrent;
using System.Reflection;
using static TaskService.Libraries.QueueTask.QueueTaskBuilder;

namespace TaskService.Libraries.QueueTask
{
    public class QueueTaskBackgroundService(IServiceProvider serviceProvider, ILogger<QueueTaskBackgroundService> logger, IDistributedLock distLock) : BackgroundService
    {
        private readonly ILogger logger = logger;
        private readonly ConcurrentDictionary<long, string> runingTaskList = new();

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
#if DEBUG
            await Task.Delay(5000, stoppingToken);
#else
            await Task.Delay(10000, stoppingToken);
#endif

            using var scope = serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

            if (queueMethodList.Count != 0)
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        foreach (var item in queueMethodList.Values.Where(t => t.IsEnable).ToList())
                        {

                            var runingTaskIdList = runingTaskList.Where(t => t.Value == item.Name).Select(t => t.Key).ToList();

                            int skipSize = runingTaskIdList.Count;

                            int taskSize = item.Semaphore - runingTaskIdList.Count;

                            if (taskSize > 0)
                            {
                                var nowTime = DateTime.UtcNow;

                                var queueTaskIdList = db.TQueueTask.Where(t => t.Name == item.Name && t.SuccessTime == null && (t.PlanTime == null || t.PlanTime <= nowTime) && runingTaskIdList.Contains(t.Id) == false && t.Count < 3 && (t.LastTime == null || (t.LastTime < nowTime.AddMinutes(-5 * t.Count)))).OrderBy(t => t.Count).ThenBy(t => t.LastTime).ThenBy(t => t.CreateTime).Skip(skipSize).Take(taskSize).Select(t => t.Id).ToList();

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



        private void RunAction(QueueTaskInfo queueTaskInfo, long queueTaskId)
        {
            Task.Run(() =>
            {
                try
                {
                    using var scope = serviceProvider.CreateScope();

                    var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
                    var idHelper = scope.ServiceProvider.GetRequiredService<IDHelper>();

                    using var lockActionState = distLock.TryLock(queueTaskInfo.Name, TimeSpan.FromMinutes(5), queueTaskInfo.Semaphore);
                    if (lockActionState != null)
                    {
                        var queueTask = db.TQueueTask.FirstOrDefault(t => t.Id == queueTaskId)!;

                        if (queueTask.FirstTime == null)
                        {
                            queueTask.FirstTime = DateTime.UtcNow;
                        }

                        queueTask.LastTime = DateTime.UtcNow;

                        queueTask.Count++;

                        db.SaveChanges();

                        var isReturnVoid = queueTaskInfo.Method.ReturnType.FullName == "System.Void";

                        var parameterType = queueTaskInfo.Method.GetParameters().FirstOrDefault()?.ParameterType;

                        object? returnObject = null;

                        if (parameterType != null)
                        {
                            if (queueTask.Parameter != null)
                            {
                                var parameter = jsonToParameter.MakeGenericMethod(parameterType).Invoke(null, new object[] { queueTask.Parameter })!;

                                returnObject = queueTaskInfo.Method.Invoke(queueTaskInfo.Context, new object[] { parameter });
                            }
                            else
                            {
                                logger.LogError(queueTaskInfo.Method + "方法要求有参数，但队列任务记录缺少参数");
                            }
                        }
                        else
                        {
                            returnObject = queueTaskInfo.Method.Invoke(queueTaskInfo.Context, null);
                        }

                        if (queueTask.CallbackName != null)
                        {
                            TQueueTask callbackTask = new()
                            {
                                Id = idHelper.GetId(),
                                Name = queueTask.CallbackName
                            };

                            if (queueTask.CallbackParameter != null)
                            {
                                callbackTask.Parameter = queueTask.CallbackParameter;
                            }
                            else if (isReturnVoid == false && returnObject != null)
                            {
                                callbackTask.Parameter = JsonHelper.ObjectToJson(returnObject);
                            }

                            db.TQueueTask.Add(callbackTask);
                        }

                        queueTask.SuccessTime = DateTime.UtcNow;

                        db.SaveChanges();

                        runingTaskList.TryRemove(queueTaskId, out _);
                    }

                }
                catch (Exception ex)
                {
                    logger.LogError($"RunAction-{queueTaskInfo.Name};{queueTaskId};{ex.Message}");
                    runingTaskList.TryRemove(queueTaskId, out _);
                }
            });
        }

    }
}
