namespace LLM;

using System.Text.Json.Nodes;

/// <summary>
/// 工具定义（OpenAI tools/function 子集）
/// </summary>
/// <param name="Name">工具名称（建议使用小写字母 + 下划线）</param>
/// <param name="ParametersSchema">参数 JSON Schema（常见子集：type=object, properties, required）</param>
/// <param name="Description">可选：工具描述</param>
public sealed record ToolDefinition(
    string Name,
    JsonNode ParametersSchema,
    string? Description = null
);

