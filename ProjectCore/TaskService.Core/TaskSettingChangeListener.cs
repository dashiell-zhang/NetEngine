using Application.Model.Events;
using EventBus;

namespace TaskService.Core;

/// <summary>
/// 合并本实例待处理的配置变更通知并唤醒同步服务
/// </summary>
internal sealed class TaskSettingChangeListener : IDisposable
{

    private readonly SemaphoreSlim pendingChange = new(0, 1);


    /// <summary>
    /// 保留一次待处理通知，包括同步过程中收到的变更
    /// </summary>
    [EventSubscribe]
    public Task OnSettingChangedAsync(TaskSettingChangedEvent message)
    {

        lock (pendingChange)
        {
            if (pendingChange.CurrentCount == 0)
            {
                pendingChange.Release();
            }
        }
        return Task.CompletedTask;

    }


    /// <summary>
    /// 等到配置变更或周期同步时间到达
    /// </summary>
    internal Task WaitForChangeAsync(TimeSpan timeout, CancellationToken cancellationToken) => pendingChange.WaitAsync(timeout, cancellationToken);


    /// <summary>
    /// 释放同步信号
    /// </summary>
    public void Dispose() => pendingChange.Dispose();

}
