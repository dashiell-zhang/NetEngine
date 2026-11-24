using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace EventBus;

/// <summary>
/// 为有监听的宿主建立消费起点并读取事件，定期清理广播历史
/// </summary>
internal sealed class EventBusHostedService(EventListenerRegistry registry, IServiceProvider services, IOptions<EventBusOptions> options, TimeProvider timeProvider, ILogger<EventBusHostedService> logger, IHostApplicationLifetime applicationLifetime) : IHostedLifecycleService, IDisposable
{

    /// <summary>
    /// 当前实例的运行选项
    /// </summary>
    private readonly EventBusOptions settings = options.Value;


    /// <summary>
    /// 在执行后端操作时解析共享传输服务，连接失败后可在下一轮重新解析
    /// </summary>
    private IEventTransport Transport => services.GetRequiredService<IEventTransport>();

    /// <summary>
    /// 控制当前实例的后台任务退出
    /// </summary>
    private readonly CancellationTokenSource stopping = new();

    /// <summary>
    /// 当前实例已经读取并安排派发的后端位置
    /// </summary>
    private string position = "";

    /// <summary>
    /// 跟踪尚未结束的监听任务，供读取循环退出时等待
    /// </summary>
    private readonly ConcurrentDictionary<Task, byte> activeDispatches = new();

    /// <summary>
    /// 后台任务的完成状态
    /// </summary>
    private Task execution = Task.CompletedTask;

    /// <summary>
    /// 防止重复释放停止令牌
    /// </summary>
    private int disposed;


    /// <summary>
    /// 有监听时在所有托管服务启动前建立消费起点，失败后重试
    /// </summary>
    public async Task StartingAsync(CancellationToken cancellationToken)
    {

        cancellationToken.ThrowIfCancellationRequested();
        if (registry.Listeners.Count == 0)
        {
            return;
        }

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                position = await Transport.GetPositionAsync(cancellationToken);
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
    /// 所有托管服务启动后调度历史清理，仅在存在监听时启动读取循环
    /// </summary>
    public Task StartedAsync(CancellationToken cancellationToken)
    {

        cancellationToken.ThrowIfCancellationRequested();
        var stoppingToken = stopping.Token;
        var cleanup = Task.Run(() => ObserveAsync(CleanupAsync, "清理", stoppingToken), stoppingToken);
        execution = registry.Listeners.Count == 0
            ? cleanup
            : Task.WhenAll(cleanup, Task.Run(() => ObserveAsync(RunAsync, "读取", stoppingToken), stoppingToken));
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
    /// 使用调度前保存的停止令牌监督后台循环，及时报告意外退出并停止宿主
    /// </summary>
    private async Task ObserveAsync(Func<CancellationToken, Task> run, string operation, CancellationToken cancellationToken)
    {

        try
        {
            await run(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "事件广播后台循环意外退出，将停止宿主 Scope={Scope} Operation={Operation}", settings.Scope, operation);
            applicationLifetime.StopApplication();
            await stopping.CancelAsync();
            throw;
        }

    }


    /// <summary>
    /// 持续读取事件并在停止读取后等待已派发的监听退出
    /// </summary>
    private async Task RunAsync(CancellationToken cancellationToken)
    {

        try
        {
            await ReadAsync(cancellationToken);
        }
        finally
        {
            await Task.WhenAll(activeDispatches.Keys);
        }

    }


    /// <summary>
    /// 按后端位置读取事件，安排派发后立即推进游标
    /// </summary>
    private async Task ReadAsync(CancellationToken cancellationToken)
    {

        var lastReadTime = timeProvider.GetUtcNow();
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var records = await Transport.ReadAsync(position, settings.BatchSize, cancellationToken);
                var now = timeProvider.GetUtcNow();
                if (now - lastReadTime > settings.Retention)
                {
                    logger.LogWarning("广播读取间隔超过历史保留时间，部分事件可能已清理 Scope={Scope} Position={Position}", settings.Scope, position);
                }

                lastReadTime = now;
                foreach (var record in records)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Dispatch(record, cancellationToken);
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
    /// 跳过派发前已到期的事件，独立调度每个匹配的监听
    /// </summary>
    private void Dispatch(EventRecord record, CancellationToken cancellationToken)
    {

        if (record.Message is not { } message || !registry.Listeners.TryGetValue(message.EventName, out var listeners))
        {
            return;
        }

        if (timeProvider.GetUtcNow() - record.PublishedAt >= settings.Retention)
        {
            logger.LogWarning("广播事件在派发前已到期，跳过监听 Scope={Scope} EventId={EventId} EventName={EventName}", settings.Scope, message.EventId, message.EventName);
            return;
        }

        foreach (var listener in listeners)
        {
            // 独立调度也隔离监听返回 Task 之前的同步执行
            var dispatch = Task.Run(() => InvokeListenerAsync(listener, message, cancellationToken), cancellationToken);
            activeDispatches.TryAdd(dispatch, 0);
            _ = dispatch.ContinueWith(RemoveCompletedDispatch, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }

    }


    /// <summary>
    /// 监听任务结束后移除跟踪记录并观察意外异常
    /// </summary>
    private void RemoveCompletedDispatch(Task dispatch)
    {

        activeDispatches.TryRemove(dispatch, out _);
        if (dispatch.IsFaulted)
        {
            logger.LogError(dispatch.Exception, "广播派发任务异常结束 Scope={Scope}", settings.Scope);
        }

    }


    /// <summary>
    /// 执行一次监听并单独记录该调用的异常
    /// </summary>
    private async Task InvokeListenerAsync(EventListener listener, EventEnvelope message, CancellationToken cancellationToken)
    {

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await listener.InvokeAsync(services, message.Payload, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "事件监听失败 Scope={Scope} EventId={EventId} EventName={EventName} Handler={HandlerType}.{HandlerMethod}", settings.Scope, message.EventId, message.EventName, listener.Method.DeclaringType, listener.Method.Name);
        }

    }


    /// <summary>
    /// 独立于事件派发循环定期清理历史记录
    /// </summary>
    private async Task CleanupAsync(CancellationToken cancellationToken)
    {

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Transport.TrimAsync(GetRetentionCutoff(), cancellationToken);
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


    /// <summary>
    /// 计算统一的历史截止时间，超出日期范围时使用最早 UTC 时间
    /// </summary>
    private DateTimeOffset GetRetentionCutoff()
    {

        var now = timeProvider.GetUtcNow().ToUniversalTime();
        return settings.Retention >= now - DateTimeOffset.MinValue
            ? DateTimeOffset.MinValue
            : now - settings.Retention;

    }

}
