using Application.Model.LLM.Invocation;
using Application.Model.LLM.Decision;

namespace Application.Interface.LLM;

/// <summary>
/// 按应用编码执行 LLM 对话、流式调用与结构化决策
/// </summary>
public interface ILlmInvokeService
{

    /// <summary>
    /// 按应用编码执行结构化决策并记录成功调用
    /// </summary>
    Task<LlmDecisionResponseDto> DecideAsync(long? actorUserId, string code, Dictionary<string, string> parameters, CancellationToken cancellationToken = default);


    /// <summary>
    /// 按 LLM 应用 Code 调用对话接口，返回完整响应
    /// </summary>
    Task<LlmChatResponseDto> ChatAsync(long? actorUserId, string code, Dictionary<string, string> parameters, CancellationToken cancellationToken = default);


    /// <summary>
    /// 按 LLM 应用 Code 调用对话接口，返回首条文本内容
    /// </summary>
    Task<string?> ChatContentAsync(long? actorUserId, string code, Dictionary<string, string> parameters, CancellationToken cancellationToken = default);


    /// <summary>
    /// 按 LLM 应用 Code 调用流式对话接口
    /// </summary>
    IAsyncEnumerable<LlmChatStreamChunkDto> ChatStreamAsync(long? actorUserId, string code, Dictionary<string, string> parameters, CancellationToken cancellationToken = default);

}
