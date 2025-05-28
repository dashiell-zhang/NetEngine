using Microsoft.Extensions.Caching.Hybrid;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Common
{
    public static class HybridCacheExtension
    {

        /// <summary>
        /// 获取或创建同步方法返回值
        /// </summary>
        /// <typeparam name="T">返回类型</typeparam>
        /// <param name="cache">HybridCache</param>
        /// <param name="factory">执行表达式</param>
        /// <param name="ttl">缓存有效期</param>
        /// <param name="keyPrefix">表达式前缀标记</param>
        /// <returns></returns>
        /// <remarks>表达式前缀标记不传的情况下默认通过反射计算</remarks>
        public static ValueTask<T> GetOrCreateAsync<T>(this HybridCache cache, Expression<Func<T>> factory, int ttl = 300, string? keyPrefix = null)
        {
            return cache.GetOrCreateInternalAsync(() => new ValueTask<T>(factory.Compile()()), factory, ttl, keyPrefix);
        }


        /// <summary>
        /// 获取或创建异步方法Task返回值
        /// </summary>
        /// <typeparam name="T">返回类型</typeparam>
        /// <param name="cache">HybridCache</param>
        /// <param name="factory">执行表达式</param>
        /// <param name="ttl">缓存有效期</param>
        /// <param name="keyPrefix">表达式前缀标记</param>
        /// <returns></returns>
        /// <remarks>表达式前缀标记不传的情况下默认通过反射计算</remarks>
        public static ValueTask<T> GetOrCreateAsync<T>(this HybridCache cache, Expression<Func<Task<T>>> factory, int ttl = 300, string? keyPrefix = null)
        {
            return cache.GetOrCreateInternalAsync(async () => await factory.Compile()(), factory, ttl, keyPrefix);
        }


        /// <summary>
        /// 获取或创建异步方法ValueTask返回值
        /// </summary>
        /// <typeparam name="T">返回类型</typeparam>
        /// <param name="cache">HybridCache</param>
        /// <param name="factory">执行表达式</param>
        /// <param name="ttl">缓存有效期</param>
        /// <param name="keyPrefix">表达式前缀标记</param>
        /// <returns></returns>
        /// <remarks>表达式前缀标记不传的情况下默认通过反射计算</remarks>
        public static ValueTask<T> GetOrCreateAsync<T>(this HybridCache cache, Expression<Func<ValueTask<T>>> factory, int ttl = 300, string? keyPrefix = null)
        {
            return cache.GetOrCreateInternalAsync(() => factory.Compile()(), factory, ttl, keyPrefix);
        }


        private static ValueTask<T> GetOrCreateInternalAsync<T>(this HybridCache cache, Func<ValueTask<T>> compiledFactory, Expression expression, int ttl, string? keyPrefix = null)
        {
            var key = GenerateCacheKey(expression, keyPrefix);

            var expiration = TimeSpan.FromSeconds(ttl);

            HybridCacheEntryOptions options = new()
            {
                Expiration = expiration,
                LocalCacheExpiration = TimeSpan.FromSeconds(1)
            };

            return cache.GetOrCreateAsync(key, _ => compiledFactory(), options);
        }


        /// <summary>
        /// 生成缓存键的核心方法
        /// </summary>
        /// <param name="expression">方法调用表达式</param>
        /// <returns>生成的缓存键</returns>
        private static string GenerateCacheKey(Expression expression, string? keyPrefix = null)
        {
            StringBuilder sb = new();

            // 解析表达式
            if (expression is LambdaExpression lambda)
            {
                expression = lambda.Body;
            }

            if (expression is MethodCallExpression methodCall)
            {

                bool isGetInstanceType = false;

                if (keyPrefix == null)
                {
                    var instanceExpr = methodCall.Object;

                    if (instanceExpr != null)
                    {
                        var instanceType = Expression.Lambda<Func<object>>(Expression.Convert(instanceExpr, typeof(object))).Compile()()?.GetType();

                        if (instanceType != null)
                        {
                            sb.Append(instanceType.FullName);
                            sb.Append('.');

                            isGetInstanceType = true;
                        }
                    }
                }

                if (isGetInstanceType == false && methodCall.Method.ReflectedType != null)
                {
                    sb.Append(methodCall.Method.ReflectedType.FullName);
                    sb.Append('.');
                }

                // 添加方法名
                sb.Append(methodCall.Method.Name);

                // 添加参数
                if (methodCall.Arguments.Count > 0)
                {
                    sb.Append('(');
                    for (int i = 0; i < methodCall.Arguments.Count; i++)
                    {
                        if (i > 0) sb.Append(',');
                        var argStr = GetArgumentValue(methodCall.Arguments[i]);
                        sb.Append(argStr);
                    }
                    sb.Append(')');
                }
            }
            else
            {
                // 如果不是方法调用，使用表达式的字符串表示
                sb.Append(expression.ToString());
            }

            string argsStr = sb.ToString();

            string argsHash = CryptoHelper.SHA256HashData(argsStr);

            return argsHash;
        }


        /// <summary>
        /// 获取表达式参数的值并序列化为JSON
        /// </summary>
        /// <param name="argument">参数表达式</param>
        /// <returns>JSON序列化后的参数值</returns>
        private static string GetArgumentValue(Expression argument)
        {
            object? value = null;

            // 常量表达式
            if (argument is ConstantExpression constant)
            {
                value = constant.Value;
            }
            // 成员访问表达式
            else if (argument is MemberExpression member)
            {
                var container = GetArgumentValueObject(member.Expression!);
                if (member.Member is FieldInfo field)
                {
                    value = field.GetValue(container);
                }
                else if (member.Member is PropertyInfo property)
                {
                    value = property.GetValue(container);
                }
            }
            // 编译并执行表达式
            else
            {
                var compiled = Expression.Lambda(argument).Compile();
                value = compiled.DynamicInvoke();
            }

            // 序列化值
            if (value != null)
            {
                return JsonHelper.ObjectCloneJson(value);
            }
            else
            {
                return "null";
            }
        }


        /// <summary>
        /// 获取表达式参数的对象值（用于成员访问）
        /// </summary>
        /// <param name="expression">表达式</param>
        /// <returns>对象值</returns>
        private static object? GetArgumentValueObject(Expression expression)
        {
            if (expression is ConstantExpression constant)
            {
                return constant.Value;
            }

            if (expression is MemberExpression member)
            {
                var container = GetArgumentValueObject(member.Expression!);
                if (member.Member is FieldInfo field)
                {
                    return field.GetValue(container);
                }
                if (member.Member is PropertyInfo property)
                {
                    return property.GetValue(container);
                }
            }

            var compiled = Expression.Lambda(expression).Compile();
            return compiled.DynamicInvoke();
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
}
