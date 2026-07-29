using Microsoft.Extensions.Caching.Hybrid;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Common;

/// <summary>
/// 提供HybridCache常用缓存扩展能力
/// </summary>
public static class HybridCacheExtension
{

    /// <summary>
    /// 获取或创建同步方法返回值（委托版本）
    /// </summary>
    /// <typeparam name="T">返回类型</typeparam>
    /// <param name="cache">HybridCache</param>
    /// <param name="cacheKey">缓存键</param>
    /// <param name="factory">执行委托</param>
    /// <param name="ttl">缓存有效期</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    /// <remarks>委托版本需要自己确保key的唯一性，首选表达式版本</remarks>
    public static ValueTask<T> GetOrCreateAsync<T>(this HybridCache cache, string cacheKey, Func<T> factory, int ttl = 300, CancellationToken cancellationToken = default)
    {
        return cache.GetOrCreateAsync(cacheKey, token =>
        {
            token.ThrowIfCancellationRequested();
            return new ValueTask<T>(factory());
        }, GetHybridCacheEntryOptions(ttl), cancellationToken: cancellationToken);
    }


    /// <summary>
    /// 获取或创建异步方法Task返回值（委托版本）
    /// </summary>
    /// <typeparam name="T">返回类型</typeparam>
    /// <param name="cache">HybridCache</param>
    /// <param name="cacheKey">缓存键</param>
    /// <param name="factory">执行委托</param>
    /// <param name="ttl">缓存有效期</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    /// <remarks>委托版本需要自己确保key的唯一性，首选表达式版本</remarks>
    public static ValueTask<T> GetOrCreateAsync<T>(this HybridCache cache, string cacheKey, Func<Task<T>> factory, int ttl = 300, CancellationToken cancellationToken = default)
    {
        return cache.GetOrCreateAsync(cacheKey, async token => await factory().WaitAsync(token), GetHybridCacheEntryOptions(ttl), cancellationToken: cancellationToken);
    }


    /// <summary>
    /// 获取或创建异步方法ValueTask返回值（委托版本）
    /// </summary>
    /// <typeparam name="T">返回类型</typeparam>
    /// <param name="cache">HybridCache</param>
    /// <param name="cacheKey">缓存键</param>
    /// <param name="factory">执行委托</param>
    /// <param name="ttl">缓存有效期</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    /// <remarks>委托版本需要自己确保key的唯一性，首选表达式版本</remarks>
    public static ValueTask<T> GetOrCreateAsync<T>(this HybridCache cache, string cacheKey, Func<ValueTask<T>> factory, int ttl = 300, CancellationToken cancellationToken = default)
    {
        return cache.GetOrCreateAsync(cacheKey, async token => await factory().AsTask().WaitAsync(token), GetHybridCacheEntryOptions(ttl), cancellationToken: cancellationToken);
    }


    /// <summary>
    /// 获取或创建同步方法返回值（表达式版本）
    /// </summary>
    /// <typeparam name="T">返回类型</typeparam>
    /// <param name="cache">HybridCache</param>
    /// <param name="factory">执行表达式</param>
    /// <param name="ttl">缓存有效期</param>
    /// <param name="keyPrefix">表达式前缀标记</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    /// <remarks>表达式前缀标记不传的情况下默认通过反射计算</remarks>
    public static ValueTask<T> GetOrCreateAsync<T>(this HybridCache cache, Expression<Func<T>> factory, int ttl = 300, string? keyPrefix = null, CancellationToken cancellationToken = default)
    {
        var prepared = PrepareMethodCall(factory, keyPrefix);

        return cache.GetOrCreateAsync(prepared.Key, token =>
        {
            token.ThrowIfCancellationRequested();
            return new ValueTask<T>((T)InvokePrepared(prepared)!);
        }, GetHybridCacheEntryOptions(ttl), cancellationToken: cancellationToken);
    }


    /// <summary>
    /// 获取或创建异步方法Task返回值（表达式版本）
    /// </summary>
    /// <typeparam name="T">返回类型</typeparam>
    /// <param name="cache">HybridCache</param>
    /// <param name="factory">执行表达式</param>
    /// <param name="ttl">缓存有效期</param>
    /// <param name="keyPrefix">表达式前缀标记</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    /// <remarks>表达式前缀标记不传的情况下默认通过反射计算</remarks>
    public static ValueTask<T> GetOrCreateAsync<T>(this HybridCache cache, Expression<Func<Task<T>>> factory, int ttl = 300, string? keyPrefix = null, CancellationToken cancellationToken = default)
    {
        var prepared = PrepareMethodCall(factory, keyPrefix);

        return cache.GetOrCreateAsync(prepared.Key, async token => await ((Task<T>)InvokePrepared(prepared)!).WaitAsync(token), GetHybridCacheEntryOptions(ttl), cancellationToken: cancellationToken);
    }


    /// <summary>
    /// 获取或创建异步方法ValueTask返回值（表达式版本）
    /// </summary>
    /// <typeparam name="T">返回类型</typeparam>
    /// <param name="cache">HybridCache</param>
    /// <param name="factory">执行表达式</param>
    /// <param name="ttl">缓存有效期</param>
    /// <param name="keyPrefix">表达式前缀标记</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    /// <remarks>表达式前缀标记不传的情况下默认通过反射计算</remarks>
    public static ValueTask<T> GetOrCreateAsync<T>(this HybridCache cache, Expression<Func<ValueTask<T>>> factory, int ttl = 300, string? keyPrefix = null, CancellationToken cancellationToken = default)
    {
        var prepared = PrepareMethodCall(factory, keyPrefix);

        return cache.GetOrCreateAsync(prepared.Key, async token => await ((ValueTask<T>)InvokePrepared(prepared)!).AsTask().WaitAsync(token), GetHybridCacheEntryOptions(ttl), cancellationToken: cancellationToken);
    }


    private static HybridCacheEntryOptions GetHybridCacheEntryOptions(int ttl)
    {
        if (ttl <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ttl), "缓存有效期必须大于0秒");
        }

        var expiration = TimeSpan.FromSeconds(ttl);

        return new()
        {
            Expiration = expiration,
            LocalCacheExpiration = expiration
        };
    }


    /// <summary>
    /// 生成缓存键的核心方法
    /// </summary>
    /// <param name="expression">方法调用表达式</param>
    /// <param name="keyPrefix">缓存键前缀</param>
    /// <returns>已准备的方法调用</returns>
    private static (MethodInfo Method, object? Target, object?[] Arguments, string Key) PrepareMethodCall(LambdaExpression expression, string? keyPrefix)
    {
        if (keyPrefix != null && string.IsNullOrWhiteSpace(keyPrefix))
        {
            throw new ArgumentException("缓存键前缀不能是空字符串或空白字符串", nameof(keyPrefix));
        }

        if (expression.Body is not MethodCallExpression methodCall)
        {
            throw new ArgumentException("缓存表达式必须是方法调用", nameof(expression));
        }

        if (methodCall.Method.GetParameters().Any(t => t.ParameterType.IsByRef))
        {
            throw new ArgumentException("缓存表达式不支持ref或out参数", nameof(expression));
        }

        object? target = methodCall.Object == null ? null : EvaluateExpression(methodCall.Object);
        object?[] arguments = methodCall.Arguments.Select(EvaluateExpression).ToArray();
        string key = GenerateCacheKey(methodCall.Method, target, arguments, keyPrefix);

        return (methodCall.Method, target, arguments, key);
    }


    /// <summary>
    /// 生成包含方法身份和参数值的缓存键
    /// </summary>
    /// <param name="method">方法信息</param>
    /// <param name="target">方法目标实例</param>
    /// <param name="arguments">已求值参数</param>
    /// <param name="keyPrefix">缓存键前缀</param>
    /// <returns>缓存键</returns>
    private static string GenerateCacheKey(MethodInfo method, object? target, object?[] arguments, string? keyPrefix)
    {
        StringBuilder sb = new();

        if (keyPrefix != null)
        {
            sb.Append(keyPrefix);
            sb.Append(':');
        }

        if (target != null)
        {
            Type targetType = target.GetType();
            sb.Append(targetType.FullName ?? targetType.Name);
            sb.Append("->");
        }

        sb.Append(method.DeclaringType?.FullName ?? method.ReflectedType?.FullName ?? "UnknownType");
        sb.Append('.');
        sb.Append(method.Name);

        if (method.IsGenericMethod)
        {
            sb.Append('<');
            sb.Append(string.Join(',', method.GetGenericArguments().Select(t => t.FullName)));
            sb.Append('>');
        }

        var parameters = method.GetParameters();
        sb.Append('(');

        for (int index = 0; index < parameters.Length; index++)
        {
            if (index > 0)
            {
                sb.Append(',');
            }

            sb.Append(parameters[index].ParameterType.FullName);
            sb.Append('=');
            sb.Append(arguments[index] == null ? "null" : JsonHelper.ObjectCloneJson(arguments[index]!));
        }

        sb.Append(')');
        return CryptoHelper.SHA256HashData(sb.ToString());
    }


    /// <summary>
    /// 获取表达式参数的对象值（用于成员访问）
    /// </summary>
    /// <param name="expression">表达式</param>
    /// <returns>对象值</returns>
    private static object? EvaluateExpression(Expression expression)
    {
        var compiled = Expression.Lambda(expression).Compile();
        return compiled.DynamicInvoke();
    }


    /// <summary>
    /// 调用已求值的方法和参数
    /// </summary>
    /// <param name="prepared">已准备的方法调用</param>
    /// <returns>方法返回值</returns>
    private static object? InvokePrepared((MethodInfo Method, object? Target, object?[] Arguments, string Key) prepared)
    {
        try
        {
            return prepared.Method.Invoke(prepared.Target, prepared.Arguments);
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }


}


public sealed class HybridCacheJsonSerializerFactory : IHybridCacheSerializerFactory
{
    private static readonly JsonSerializerOptions _defaultOptions;

    static HybridCacheJsonSerializerFactory()
    {
        _defaultOptions = new()
        {
            ReferenceHandler = ReferenceHandler.Preserve,   //解决循环依赖
            DefaultIgnoreCondition = JsonIgnoreCondition.Never, //屏蔽 JsonIgnore 配置
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,  //关闭默认转义
        };
    }

    public bool TryCreateSerializer<T>([NotNullWhen(true)] out IHybridCacheSerializer<T>? serializer)
    {
        serializer = new DefaultJsonSerializer<T>();
        return true;
    }

    internal sealed class DefaultJsonSerializer<T> : IHybridCacheSerializer<T>
    {
        T IHybridCacheSerializer<T>.Deserialize(ReadOnlySequence<byte> source)
        {
            var reader = new Utf8JsonReader(source);
            return JsonSerializer.Deserialize<T>(ref reader, _defaultOptions)!;
        }

        void IHybridCacheSerializer<T>.Serialize(T value, IBufferWriter<byte> target)
        {
            using var writer = new Utf8JsonWriter(target);
            JsonSerializer.Serialize(writer, value, _defaultOptions);
        }
    }
}
