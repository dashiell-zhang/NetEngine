using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EventBus;

/// <summary>
/// 在宿主启动前建立起点并运行进程独立的广播读取和清理
/// </summary>
internal sealed class EventBusHostedService(IEventTransport transport, EventListenerRegistry registry, IServiceProvider services, IOptions<EventBusOptions> options, TimeProvider timeProvider, ILogger<EventBusHostedService> logger) : IHostedLifecycleService, IDisposable
{

    /// <summary>
    /// 当前实例的运行选项
    /// </summary>
    private readonly EventBusOptions settings = options.Value;

    /// <summary>
    /// 控制当前实例的后台任务退出
    /// </summary>
    private readonly CancellationTokenSource stopping = new();

    /// <summary>
    /// 当前实例已经处理到的后端位置
    /// </summary>
    private string position = "";

    /// <summary>
    /// 后台任务的完成状态
    /// </summary>
    private Task execution = Task.CompletedTask;

    /// <summary>
    /// 防止重复释放停止令牌
    /// </summary>
    private int disposed;


    /// <summary>
    /// 在所有托管服务启动前建立消费起点，失败时保持初始化并重试
    /// </summary>
    public async Task StartingAsync(CancellationToken cancellationToken)
    {

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                position = await transport.GetPositionAsync(cancellationToken);
                logger.LogInformation("事件广播已建立启动位置 Scope={Scope} Position={Position}", settings.Scope, position);
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "事件广播初始化失败，宿主等待后端恢复 Scope={Scope}", settings.Scope);
                await Task.Delay(settings.PollInterval, timeProvider, cancellationToken);
            }
        }

    }


    /// <summary>
    /// 等待其他托管服务完成启动初始化
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;


    /// <summary>
    /// 所有托管服务启动完成后开始消费此前起点之后的事件
    /// </summary>
    public Task StartedAsync(CancellationToken cancellationToken)
    {

        cancellationToken.ThrowIfCancellationRequested();
        execution = Task.WhenAll(RunAsync(stopping.Token), CleanupAsync(stopping.Token));
        return Task.CompletedTask;

    }


    /// <summary>
    /// 在其他托管服务停止前通知监听函数退出
    /// </summary>
    public Task StoppingAsync(CancellationToken cancellationToken)
    {

        stopping.Cancel();
        return Task.CompletedTask;

    }


    /// <summary>
    /// 取消并等待本实例的后台任务
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {

        await stopping.CancelAsync();
        try
        {
            await execution.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
        }

    }


    /// <summary>
    /// 完成托管服务停止阶段
    /// </summary>
    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;


    /// <summary>
    /// 取消后台执行并释放当前实例的停止令牌
    /// </summary>
    public void Dispose()
    {

        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        try
        {
            stopping.Cancel();
        }
        finally
        {
            stopping.Dispose();
        }

    }


    /// <summary>
    /// 从原位置持续读取，连接恢复时继续补收
    /// </summary>
    private async Task RunAsync(CancellationToken cancellationToken)
    {

        var lastReadTime = timeProvider.GetUtcNow();
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var records = await transport.ReadAsync(position, settings.BatchSize, cancellationToken);
                var now = timeProvider.GetUtcNow();
                if (now - lastReadTime > settings.Retention)
                {
                    logger.LogWarning("广播读取间隔超过历史保留时间，部分事件可能已清理 Scope={Scope} Position={Position}", settings.Scope, position);
                }

                lastReadTime = now;
                foreach (var record in records)
                {
                    await DispatchAsync(record, cancellationToken);
                    position = record.Position;
                }

                if (records.Count == settings.BatchSize)
                {
                    continue;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "广播读取失败，将保留位置并重试 Scope={Scope} Position={Position}", settings.Scope, position);
            }

            await Task.Delay(settings.PollInterval, timeProvider, cancellationToken);
        }

    }


    /// <summary>
    /// 重试当前事件的失败监听函数，保留本事件中已经成功的方法进度
    /// </summary>
    private async Task DispatchAsync(EventRecord record, CancellationToken cancellationToken)
    {

        if (!registry.Listeners.TryGetValue(record.Message.EventName, out var listeners))
        {
            return;
        }

        var pending = listeners.ToList();
        while (pending.Count != 0)
        {
            if (record.PublishedAt < timeProvider.GetUtcNow() - settings.Retention)
            {
                logger.LogWarning("广播事件超过保留时间，跳过尚未完成的监听 Scope={Scope} EventId={EventId} EventName={EventName}", settings.Scope, record.Message.EventId, record.Message.EventName);
                return;
            }

            foreach (var listener in pending.ToArray())
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    await listener.InvokeAsync(services, record.Message.Payload, cancellationToken);
                    pending.Remove(listener);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "事件监听失败，将重试 Scope={Scope} EventId={EventId} Handler={Handler}", settings.Scope, record.Message.EventId, listener.Method);
                }
            }

            if (pending.Count != 0)
            {
                await Task.Delay(settings.RetryInterval, timeProvider, cancellationToken);
            }
        }

    }


    /// <summary>
    /// 不受监听方法重试阻塞地清理历史记录
    /// </summary>
    private async Task CleanupAsync(CancellationToken cancellationToken)
    {

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await transport.TrimAsync(timeProvider.GetUtcNow() - settings.Retention, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "广播历史清理失败，将在下一轮重试 Scope={Scope}", settings.Scope);
            }

            await Task.Delay(settings.CleanupInterval, timeProvider, cancellationToken);
        }

    }

}
