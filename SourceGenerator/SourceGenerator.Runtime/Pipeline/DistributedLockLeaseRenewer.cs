using DistributedLock;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace SourceGenerator.Runtime.Pipeline;

/// <summary>
/// 在锁句柄存活期间定时续期分布式锁租约
/// </summary>
internal sealed class DistributedLockLeaseRenewer
{

    /// <summary>
    /// 单次续期间隔上限
    /// </summary>
    private static readonly TimeSpan MaximumRenewalInterval = TimeSpan.FromSeconds(20);


    /// <summary>
    /// 续期失败后的最大重试间隔
    /// </summary>
    private static readonly TimeSpan MaximumRetryInterval = TimeSpan.FromSeconds(1);


    /// <summary>
    /// 分布式锁服务
    /// </summary>
    private readonly IDistributedLock distributedLock;


    /// <summary>
    /// 当前持有的锁句柄
    /// </summary>
    private readonly IDistributedLockHandle lockHandle;


    /// <summary>
    /// 每次续期设置的租约时长
    /// </summary>
    private readonly TimeSpan expiry;


    /// <summary>
    /// 用于错误日志的锁键
    /// </summary>
    private readonly string lockKey;


    /// <summary>
    /// 用于错误日志的业务方法信息
    /// </summary>
    private readonly string method;


    /// <summary>
    /// 当前调用使用的日志记录器
    /// </summary>
    private readonly ILogger? logger;


    /// <summary>
    /// 控制续期循环停止的取消源
    /// </summary>
    private readonly CancellationTokenSource stopSource = new();


    /// <summary>
    /// 正在运行的续期任务
    /// </summary>
    private readonly Task renewalTask;


    /// <summary>
    /// 最近一次续期失败时捕获的异常
    /// </summary>
    private Exception? lastRenewalException;


    /// <summary>
    /// 标记续期循环是否已经停止
    /// </summary>
    private int stopped;


    /// <summary>
    /// 创建并启动分布式锁租约续期循环
    /// </summary>
    /// <param name="distributedLock">分布式锁服务</param>
    /// <param name="lockHandle">当前持有的锁句柄</param>
    /// <param name="expiry">每次续期设置的租约时长</param>
    /// <param name="lockKey">用于错误日志的锁键</param>
    /// <param name="method">用于错误日志的业务方法信息</param>
    /// <param name="logger">当前调用使用的日志记录器</param>
    public DistributedLockLeaseRenewer(IDistributedLock distributedLock, IDistributedLockHandle lockHandle, TimeSpan expiry, string lockKey, string method, ILogger? logger)
    {

        if (expiry <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(expiry), "expiry 必须大于 0");
        }

        this.distributedLock = distributedLock;
        this.lockHandle = lockHandle;
        this.expiry = expiry;
        this.lockKey = lockKey;
        this.method = method;
        this.logger = logger;
        renewalTask = RenewUntilStoppedAsync();

    }


    /// <summary>
    /// 停止并等待续期循环退出
    /// </summary>
    public async Task StopAsync()
    {

        if (Interlocked.Exchange(ref stopped, 1) != 0)
        {
            return;
        }

        stopSource.Cancel();

        try
        {
            await renewalTask;
        }
        catch (OperationCanceledException) when (stopSource.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            LogLeaseLost(ex);
        }
        finally
        {
            stopSource.Dispose();
        }

    }


    /// <summary>
    /// 按租约周期持续续期直到停止或租约丢失
    /// </summary>
    private async Task RenewUntilStoppedAsync()
    {

        var renewalInterval = CalculateRenewalInterval(expiry);
        var renewalDeadline = expiry - renewalInterval;
        var lastSuccessfulRenewalTimestamp = Stopwatch.GetTimestamp();

        while (true)
        {
            await Task.Delay(renewalInterval, stopSource.Token);

            var renewed = await TryRenewAsync();
            if (renewed)
            {
                lastSuccessfulRenewalTimestamp = Stopwatch.GetTimestamp();
                continue;
            }

            var retryInterval = CalculateRetryInterval(renewalInterval);

            while (Stopwatch.GetElapsedTime(lastSuccessfulRenewalTimestamp) < renewalDeadline)
            {
                var remaining = renewalDeadline - Stopwatch.GetElapsedTime(lastSuccessfulRenewalTimestamp);
                if (remaining <= TimeSpan.Zero)
                {
                    break;
                }

                await Task.Delay(remaining < retryInterval ? remaining : retryInterval, stopSource.Token);

                renewed = await TryRenewAsync();
                if (renewed)
                {
                    lastSuccessfulRenewalTimestamp = Stopwatch.GetTimestamp();
                    break;
                }
            }

            if (!renewed)
            {
                LogLeaseLost(lastRenewalException);
                return;
            }
        }

    }


    /// <summary>
    /// 尝试续期当前锁并将异常视为本次续期失败
    /// </summary>
    /// <returns>本次续期是否成功</returns>
    private async Task<bool> TryRenewAsync()
    {

        try
        {
            lastRenewalException = null;
            var renewed = await distributedLock.RenewAsync(lockHandle, expiry, stopSource.Token);
            return renewed;
        }
        catch (OperationCanceledException) when (stopSource.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            lastRenewalException = ex;
            return false;
        }

    }


    /// <summary>
    /// 根据租约时长计算正常续期间隔
    /// </summary>
    /// <param name="leaseExpiry">租约时长</param>
    /// <returns>不晚于租约三分之一的续期间隔</returns>
    private static TimeSpan CalculateRenewalInterval(TimeSpan leaseExpiry)
    {

        var interval = TimeSpan.FromTicks(Math.Max(1, leaseExpiry.Ticks / 3));
        return interval < MaximumRenewalInterval ? interval : MaximumRenewalInterval;

    }


    /// <summary>
    /// 根据正常续期间隔计算失败重试间隔
    /// </summary>
    /// <param name="renewalInterval">正常续期间隔</param>
    /// <returns>失败后的重试间隔</returns>
    private static TimeSpan CalculateRetryInterval(TimeSpan renewalInterval)
    {

        var interval = TimeSpan.FromTicks(Math.Max(1, renewalInterval.Ticks / 4));
        return interval < MaximumRetryInterval ? interval : MaximumRetryInterval;

    }


    /// <summary>
    /// 记录租约丢失错误且不影响业务执行
    /// </summary>
    /// <param name="exception">续期时捕获的异常</param>
    private void LogLeaseLost(Exception? exception = null)
    {

        if (exception is null)
        {
            logger?.LogError("Distributed lock lease lost method={Method} key={LockKey} expirySeconds={ExpirySeconds} Business execution will continue", method, lockKey, expiry.TotalSeconds);
            return;
        }

        logger?.LogError(exception, "Distributed lock lease renewal error method={Method} key={LockKey} expirySeconds={ExpirySeconds} Business execution will continue", method, lockKey, expiry.TotalSeconds);

    }

}
