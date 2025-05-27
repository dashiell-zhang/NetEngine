using Microsoft.Extensions.Caching.Hybrid;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace Common
{
    public static class HybridCacheExtension
    {

        public static async ValueTask<T> GetOrCreateAsync<T>(this HybridCache cache, Expression<Func<ValueTask<T>>> factory, TimeSpan expiration)
        {
            var key = GenerateCacheKey(factory);
            HybridCacheEntryOptions options = new()
            {
                Expiration = expiration,
                LocalCacheExpiration = expiration
            };
            return await cache.GetOrCreateAsync(key, async _ => await factory.Compile()(), options);
        }


        public static async ValueTask<T> GetOrCreateAsync<T>(this HybridCache cache, Expression<Func<T>> factory, TimeSpan expiration)
        {
            var key = GenerateCacheKey(factory);
            HybridCacheEntryOptions options = new()
            {
                Expiration = expiration,
                LocalCacheExpiration = expiration
            };
            return await cache.GetOrCreateAsync(key, _ => new ValueTask<T>(factory.Compile()()), options);
        }


        public static async ValueTask<T> GetOrCreateAsync<T>(this HybridCache cache, Expression<Func<Task<T>>> factory, TimeSpan expiration)
        {
            var key = GenerateCacheKey(factory);
            HybridCacheEntryOptions options = new()
            {
                Expiration = expiration,
                LocalCacheExpiration = expiration
            };
            return await cache.GetOrCreateAsync(key, async _ => await factory.Compile()(), options);
        }



        /// <summary>
        /// 生成缓存键的核心方法
        /// </summary>
        /// <param name="expression">方法调用表达式</param>
        /// <returns>生成的缓存键</returns>
        private static string GenerateCacheKey(Expression expression)
        {
            var sb = new StringBuilder();

            // 解析表达式
            if (expression is LambdaExpression lambda)
            {
                expression = lambda.Body;
            }

            if (expression is MethodCallExpression methodCall)
            {
                // 添加类型全名（如果存在）
                if (methodCall.Method.ReflectedType != null)
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

            return sb.ToString();
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
}
