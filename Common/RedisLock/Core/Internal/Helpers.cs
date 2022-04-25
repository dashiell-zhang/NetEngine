using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Common.RedisLock.Core.Internal
{
    internal static class Helpers
    {
        /// <summary>
        /// 执行类型安全的强制转换
        /// </summary>
        public static T As<T>(this T @this) => @this;

        /// <summary>
        /// 执行 <see cref="ValueTask{TResult}"/> 的类型安全“强制转换”
        /// </summary>
        public static async ValueTask<TBase> Convert<TDerived, TBase>(this ValueTask<TDerived> task, To<TBase>.ValueTaskConversion _)
            where TDerived : TBase =>
            await task.ConfigureAwait(false);

        public readonly struct TaskConversion
        {
            public TaskConversion<TTo> To<TTo>() => throw new InvalidOperationException();
        }

        public readonly struct TaskConversion<TTo> { }

        internal static async ValueTask ConvertToVoid<TResult>(this ValueTask<TResult> task) => await task.ConfigureAwait(false);

        public static ValueTask<T> AsValueTask<T>(this Task<T> task) => new ValueTask<T>(task);
        public static ValueTask AsValueTask(this Task task) => new ValueTask(task);
        public static ValueTask<T> AsValueTask<T>(this T value) => new ValueTask<T>(value);

        public static Task<TResult> SafeCreateTask<TState, TResult>(Func<TState, Task<TResult>> taskFactory, TState state) =>
            InternalSafeCreateTask<TState, Task<TResult>, TResult>(taskFactory, state);

        public static Task SafeCreateTask<TState>(Func<TState, Task> taskFactory, TState state) =>
            InternalSafeCreateTask<TState, Task, bool>(taskFactory, state);

        private static TTask InternalSafeCreateTask<TState, TTask, TResult>(Func<TState, TTask> taskFactory, TState state)
            where TTask : Task
        {
            try { return taskFactory(state); }
            catch (OperationCanceledException)
            {
                // 不要在这里使用 Task.FromCanceled 因为 oce.CancellationToken 不能保证
                // 有 FromCanceled 需要的 IsCancellationRequested
                var canceledTaskBuilder = new TaskCompletionSource<TResult>();
                canceledTaskBuilder.SetCanceled();
                return (TTask)canceledTaskBuilder.Task.As<object>();
            }
            catch (Exception ex) { return (TTask)Task.FromException<TResult>(ex).As<object>(); }
        }

        public static ObjectDisposedException ObjectDisposed<T>(this T _) where T : IAsyncDisposable =>
            throw new ObjectDisposedException(typeof(T).ToString());

        public static NonThrowingAwaitable<TTask> TryAwait<TTask>(this TTask task) where TTask : Task =>
            new NonThrowingAwaitable<TTask>(task);

        /// <summary>
        /// 抛出异常很慢，我们的工作流程让我们在常见情况下取消任务。 使用这个特殊的等待
        /// 允许我们等待这些任务而不会引发异常
        /// </summary>
        public readonly struct NonThrowingAwaitable<TTask> : ICriticalNotifyCompletion
            where TTask : Task
        {
            private readonly TTask _task;
            private readonly ConfiguredTaskAwaitable.ConfiguredTaskAwaiter _taskAwaiter;

            public NonThrowingAwaitable(TTask task)
            {
                this._task = task;
                this._taskAwaiter = task.ConfigureAwait(false).GetAwaiter();
            }

            public NonThrowingAwaitable<TTask> GetAwaiter() => this;

            public bool IsCompleted => this._taskAwaiter.IsCompleted;

            public TTask GetResult()
            {
                // 不调用 _taskAwaiter.GetResult() 因为这可能会抛出！

                Invariant.Require(this._task.IsCompleted);
                return this._task;
            }

            public void OnCompleted(Action continuation) => this._taskAwaiter.OnCompleted(continuation);
            public void UnsafeOnCompleted(Action continuation) => this._taskAwaiter.UnsafeOnCompleted(continuation);
        }

        public static bool TryGetValue<T>(this T? nullable, out T value)
            where T : struct
        {
            value = nullable.GetValueOrDefault();
            return nullable.HasValue;
        }
    }

    /// <summary>
    /// 协助价值任务转换的类型推断
    /// </summary>
    internal static class To<TTo>
    {
        public static ValueTaskConversion ValueTask => default;

        public readonly struct ValueTaskConversion { }
    }
}
