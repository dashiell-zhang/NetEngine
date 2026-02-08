namespace LLM;

/// <summary>
/// 对话消息角色
/// </summary>
public enum ChatRole
{
    /// <summary>
    /// 系统消息：用于设定行为/规则
    /// </summary>
    System = 0,

    /// <summary>
    /// 用户消息：最终用户输入
    /// </summary>
    User = 1,

    /// <summary>
    /// 助手消息：模型输出
    /// </summary>
    Assistant = 2,

    /// <summary>
    /// 工具消息：工具调用的返回/中间信息（是否支持取决于供应商）
    /// </summary>
    Tool = 3
}
