using System;

namespace SourceGenerator.Runtime.Attributes;

/// <summary>
/// 标记一个方法为 LLM Tool（Function Calling）。
/// 约定方法签名：Task&lt;TResult&gt;/ValueTask&lt;TResult&gt; Method(TArgs args, CancellationToken ct)
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class LlmToolAttribute : Attribute
{
    public string Name { get; }

    public string? Description { get; }

    public LlmToolAttribute(string name, string? description = null)
    {
        Name = name;
        Description = description;
    }
}

