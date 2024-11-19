using Common;
using DistributedLock;
using IdentifierGenerator;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Repository.Database;
using System.Collections.Concurrent;
using System.Reflection;
using static TaskService.Core.QueueTask.QueueTaskBuilder;

namespace TaskService.Core.QueueTask
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

                                var queueTaskIdList = db.TQueueTask.Where(t => t.Name == item.Name && t.CreateTime < nowTime.AddSeconds(-1) && t.SuccessTime == null && (t.PlanTime == null || t.PlanTime <= nowTime) && runingTaskIdList.Contains(t.Id) == false && t.Count < 3 && (t.LastTime == null || t.LastTime < nowTime.AddMinutes(-5 * t.Count))).OrderBy(t => t.Count).ThenBy(t => t.LastTime).ThenBy(t => t.CreateTime).Skip(skipSize).Take(taskSize).Select(t => t.Id).ToList();

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


        private readonly MethodInfo jsonCloneObject = typeof(JsonHelper).GetMethod("JsonCloneObject", BindingFlags.Static | BindingFlags.Public)!;



        private void RunAction(QueueTaskInfo queueTaskInfo, long queueTaskId)
        {
            Task.Run(() =>
            {
                try
                {
                    using var scope = serviceProvider.CreateScope();

                    var queueTaskService = scope.ServiceProvider.GetRequiredService<QueueTaskService>();

                    queueTaskService.CurrentTaskId = queueTaskId;

                    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();

                    using var db = factory.CreateDbContext();

                    var idService = scope.ServiceProvider.GetRequiredService<IdService>();

                    using var lockActionState = distLock.TryLock(queueTaskInfo.Name, TimeSpan.FromMinutes(queueTaskInfo.Duration), queueTaskInfo.Semaphore);
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

                        Type serviceType = queueTaskInfo.Method.DeclaringType!;

                        object serviceInstance = scope.ServiceProvider.GetRequiredService(serviceType);

                        if (parameterType != null)
                        {
                            if (queueTask.Parameter != null)
                            {
                                var parameter = jsonCloneObject.MakeGenericMethod(parameterType).Invoke(null, [queueTask.Parameter])!;

                                returnObject = queueTaskInfo.Method.Invoke(serviceInstance, [parameter]);
                            }
                            else
                            {
                                logger.LogError(queueTaskInfo.Method + "方法要求有参数，但队列任务记录缺少参数");
                            }
                        }
                        else
                        {
                            returnObject = queueTaskInfo.Method.Invoke(serviceInstance, null);
                        }

                        queueTask.SuccessTime = DateTime.UtcNow;

                        var isHaveChild = db.TQueueTask.Where(t => t.ParentTaskId == queueTaskId).Any();

                        if (!isHaveChild)
                        {
                            queueTask.ChildSuccessTime = queueTask.SuccessTime;
                        }

                        if (queueTask.CallbackName != null)
                        {

                            if (queueTask.CallbackParameter == null && isReturnVoid == false && returnObject != null)
                            {
                                queueTask.CallbackParameter = JsonHelper.ObjectToJson(returnObject);
                            }

                            if (queueTask.ChildSuccessTime != null)
                            {
                                TQueueTask callbackTask = new()
                                {
                                    Id = idService.GetId(),
                                    Name = queueTask.CallbackName,
                                    Parameter = queueTask.CallbackParameter
                                };

                                db.TQueueTask.Add(callbackTask);
                            }
                        }

                        if (queueTask.ParentTaskId != null && queueTask.ChildSuccessTime != null)
                        {
                            UpdateParentState(queueTask.ParentTaskId.Value, queueTask.Id);
                        }

                        db.SaveChanges();
                    }


                    void UpdateParentState(long parentTaskId, long currentTaskId)
                    {
                        using (distLock.Lock(parentTaskId.ToString()))
                        {
                            //同级别是否全部执行完成
                            var isSameLevelHaveWait = db.TQueueTask.Where(t => t.ParentTaskId == parentTaskId && t.Id != currentTaskId && t.ChildSuccessTime == null).Any();

                            if (!isSameLevelHaveWait)
                            {
                                var parentTaskInfo = db.TQueueTask.Where(t => t.Id == parentTaskId).First();

                                parentTaskInfo.ChildSuccessTime = DateTimeOffset.UtcNow;

                                if (parentTaskInfo.CallbackName != null)
                                {
                                    TQueueTask callbackTask = new()
                                    {
                                        Id = idService.GetId(),
                                        Name = parentTaskInfo.CallbackName,
                                        Parameter = parentTaskInfo.CallbackParameter
                                    };

                                    db.TQueueTask.Add(callbackTask);
                                }

                                if (parentTaskInfo.ParentTaskId != null)
                                {
                                    UpdateParentState(parentTaskInfo.ParentTaskId.Value, parentTaskInfo.Id);
                                }
                            }
                        }


                    }

                }
                catch (Exception ex)
                {
                    logger.LogError($"RunAction-{queueTaskInfo.Name};{queueTaskId};{ex.Message}");
                }
                finally
                {
                    runingTaskList.TryRemove(queueTaskId, out _);
                }
            });
        }

    }
}
