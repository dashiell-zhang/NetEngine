using Application.Model.LLM.Invocation;

namespace Application.Interface.LLM;

/// <summary>
/// 按应用编码执行 LLM 对话与流式调用
/// </summary>
public interface ILlmInvokeService
{

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
