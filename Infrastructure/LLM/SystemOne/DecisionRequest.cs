using System.Text.Json.Nodes;

namespace LLM.SystemOne;

/// <summary>
/// 已校验的 System One 决策请求
/// </summary>
public sealed class DecisionRequest
{

    /// <summary>
    /// 保存校验后的请求快照供客户端发送
    /// </summary>
    internal DecisionRequest(JsonNode state, Dictionary<string, DecisionQuestion> questions, Dictionary<string, JsonNode?> extraBody)
    {

        State = state;
        Questions = questions;
        ExtraBody = extraBody;

    }


    /// <summary>
    /// 待判断的业务内容
    /// </summary>
    internal JsonNode State { get; }


    /// <summary>
    /// 以问题标识索引的决策定义
    /// </summary>
    internal Dictionary<string, DecisionQuestion> Questions { get; }


    /// <summary>
    /// 额外请求参数
    /// </summary>
    internal Dictionary<string, JsonNode?> ExtraBody { get; }

}
