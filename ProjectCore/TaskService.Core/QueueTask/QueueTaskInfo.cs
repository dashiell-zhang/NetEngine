using System.Reflection;

namespace TaskService.Core.QueueTask;

/// <summary>
/// 队列任务信息模型
/// </summary>
public class QueueTaskInfo
{

    /// <summary>
    /// 任务名称
    /// </summary>
    public string Name { get; set; }


    /// <summary>
    /// 并发值
    /// </summary>
    public int Semaphore { get; set; }


    /// <summary>
    /// 声明时的并发值，清空数据库覆盖配置时恢复使用
    /// </summary>
    internal int DefaultSemaphore { get; init; } = 1;


    /// <summary>
    /// 方法
    /// </summary>
    public MethodInfo Method { get; set; }


    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnable { get; set; }

}
