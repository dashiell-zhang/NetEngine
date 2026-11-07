namespace Application.Model.LLM.Invocation;

/// <summary>
/// 应用对话消息的角色
/// </summary>
public enum LlmChatRole
{

    /// <summary>
    /// 系统消息
    /// </summary>
    System = 0,


    /// <summary>
    /// 用户消息
    /// </summary>
    User = 1,


    /// <summary>
    /// 助手消息
    /// </summary>
    Assistant = 2,


    /// <summary>
    /// 工具消息
    /// </summary>
    Tool = 3

}
