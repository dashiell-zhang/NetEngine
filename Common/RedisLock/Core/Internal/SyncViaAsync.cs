using System;
using System.Threading;
using System.Threading.Tasks;

namespace Common.RedisLock.Core.Internal
{
    /// <summary>
    /// 帮助跨同步和异步路径重用代码，利用异步代码将同步运行的事实
    /// 除非它真的遇到异步操作。 下游代码应该使用 <see cref="IsSynchronous"/>
    /// 在同步和异步操作之间进行选择。
    /// 
    /// 这个类不会产生sync-over-async反模式的开销； 唯一的开销是使用 <see cref="ValueTask"/>s
    /// 以同步方式。
    /// </summary>
    internal static class SyncViaAsync
    {
        [ThreadStatic]
        private static bool _isSynchronous;

        public static bool IsSynchronous => _isSynchronous;

        /// <summary>
        /// 同步运行 <paramref name="action"/>
        /// </summary>
        public static void Run<TState>(Func<TState, ValueTask> action, TState state)
        {
            Run(
                async s =>
                {
                    await s.action(s.state).ConfigureAwait(false);
                    return true;
                },
                (action, state)
            );
        }

        /// <summary>
        /// 同步运行 <paramref name="action"/>
        /// </summary>
        public static TResult Run<TState, TResult>(Func<TState, ValueTask<TResult>> action, TState state)
        {
            Invariant.Require(!_isSynchronous);

            try
            {
                _isSynchronous = true;

                var task = action(state);
                Invariant.Require(task.IsCompleted);

                // 这不应该发生（并且不能在调试版本中）。 但是，为了绝对确保我们将其作为
                // 发布构建的回退逻辑
                if (!task.IsCompleted)
                {
                    // 调用 AsTask()，因为 https://docs.microsoft.com/en-us/dotnet/api/system.threading.tasks.valuetask-1?view=netcore-3.1
                    // 表示我们不应该调用 GetAwaiter().GetResult()，除非在完成的 ValueTask 上
                    return task.AsTask().GetAwaiter().GetResult();
                }

                return task.GetAwaiter().GetResult();
            }
            finally
            {
                _isSynchronous = false;
            }
        }

        /// <summary>
        /// <see cref="SyncViaAsync"/> 兼容的 <see cref="Task.Delay(TimeSpan, CancellationToken)"/> 实现。
        /// </summary>
        public static ValueTask Delay(TimeoutValue timeout, CancellationToken cancellationToken)
        {
            if (!IsSynchronous)
            {
                return Task.Delay(timeout.InMilliseconds, cancellationToken).AsValueTask();
            }

            if (cancellationToken.CanBeCanceled)
            {
                if (cancellationToken.WaitHandle.WaitOne(timeout.InMilliseconds))
                {
                    throw new OperationCanceledException("delay was canceled", cancellationToken);
                }
            }
            else
            {
                Thread.Sleep(timeout.InMilliseconds);
            }

            return default;
        }

        /// <summary>
        /// 对于实现 <see cref="IAsyncDisposable"/> 和 <see cref="IDisposable"/> 的类型 <typeparamref name="TDisposable"/>，
        /// 使用 <see cref="IAsyncDisposable.DisposeAsync"/> 提供 <see cref="IDisposable.Dispose"/> 的实现。
        /// </summary>
        public static void DisposeSyncViaAsync<TDisposable>(this TDisposable disposable)
            where TDisposable : IAsyncDisposable, IDisposable =>
            Run(@this => @this.DisposeAsync(), disposable);

        /// <summary>
        /// 在同步模式下，对提供的 <paramref name="task"/> 执行阻塞等待。 在异步模式下，
        /// 将 <paramref name="task"/> 作为 <see cref="ValueTask{TResult}"/> 返回。
        /// </summary>
        public static ValueTask<TResult> AwaitSyncOverAsync<TResult>(this Task<TResult> task) =>
            IsSynchronous ? task.GetAwaiter().GetResult().AsValueTask() : task.AsValueTask();

        /// <summary>
        /// 在同步模式下，对提供的 <paramref name="task"/> 执行阻塞等待。 在异步模式下，
        /// 将 <paramref name="task"/> 作为 <see cref="ValueTask"/> 返回。
        /// </summary>
        public static ValueTask AwaitSyncOverAsync(this Task task)
        {
            if (IsSynchronous)
            {
                task.GetAwaiter().GetResult();
                return default;
            }

            return task.AsValueTask();
        }
    }
}
