using System.Security.Cryptography;
using System.Text;

namespace SourceGenerator.Runtime.Pipeline;

/// <summary>
/// 为代理行为生成不包含原始参数的稳定调用摘要
/// </summary>
internal static class InvocationKey
{

    /// <summary>
    /// 根据完整方法标识和可选参数内容生成 SHA-256 摘要
    /// </summary>
    /// <param name="context">当前调用上下文</param>
    /// <param name="includeArguments">是否将规范化参数内容纳入摘要</param>
    /// <returns>小写十六进制 SHA-256 摘要</returns>
    public static string ComposeHash(InvocationContext context, bool includeArguments)
    {

        var seed = includeArguments
            ? context.MethodKey + "\n" + context.ArgumentsKey
            : context.MethodKey;
        var bytes = Encoding.UTF8.GetBytes(seed);
        var hash = SHA256.HashData(bytes);
        var builder = new StringBuilder(hash.Length * 2);

        foreach (var value in hash)
        {
            builder.Append(value.ToString("x2"));
        }

        return builder.ToString();

    }

}
