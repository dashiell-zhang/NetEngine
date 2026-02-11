using System;

namespace SourceGenerator.Runtime.Attributes;

/// <summary>
/// 可选：为工具参数提供描述/别名（用于生成 JSON Schema 子集）。
/// 可作用于参数类型的属性或 record 主构造参数。
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
public sealed class LlmToolParamAttribute : Attribute
{
    public string? Name { get; }

    public string? Description { get; }

    public LlmToolParamAttribute(string? name = null, string? description = null)
    {
        Name = name;
        Description = description;
    }
}

