namespace LLM;

/// <summary>
/// 工具选择策略（OpenAI tool_choice 子集）
/// </summary>
public enum ToolChoiceType
{
    Auto = 0,
    None = 1,
    Required = 2,
    Specific = 3
}

/// <summary>
/// 工具选择（auto/none/required 或指定某个工具）
/// </summary>
public sealed record ToolChoice(ToolChoiceType Type, string? Name = null)
{
    public static ToolChoice Auto { get; } = new(ToolChoiceType.Auto);

    public static ToolChoice None { get; } = new(ToolChoiceType.None);

    public static ToolChoice Required { get; } = new(ToolChoiceType.Required);

    public static ToolChoice Specific(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Tool name is required for Specific tool choice.", nameof(name));
        }

        return new ToolChoice(ToolChoiceType.Specific, name.Trim());
    }
}

