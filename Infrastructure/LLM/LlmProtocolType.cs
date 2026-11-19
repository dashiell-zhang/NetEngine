namespace LLM;

/// <summary>
/// LLM 协议类型
/// </summary>
public enum LlmProtocolType
{

    /// <summary>
    /// OpenAI Chat Completions 兼容协议
    /// </summary>
    Chat = 0,


    /// <summary>
    /// OpenAI Responses API 协议
    /// </summary>
    Responses = 1,


    /// <summary>
    /// Anthropic Messages API 协议
    /// </summary>
    Anthropic = 2,


    /// <summary>
    /// System One 结构化决策协议
    /// </summary>
    SystemOne = 3

}
