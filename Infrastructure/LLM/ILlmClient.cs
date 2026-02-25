namespace LLM;

/// <summary>
/// LLM 客户端抽象 用于统一对话能力的调用方式
/// </summary>
public interface ILlmClient
{

    /// <summary>
    /// 对话 Chat Completion
    /// </summary>
    /// <param name="request">请求参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>对话响应</returns>
    Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default);


    /// <summary>
    /// 对话 Chat Completion 流式
    /// </summary>
    /// <param name="request">请求参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>流式分片序列</returns>
    IAsyncEnumerable<ChatStreamChunk> ChatStreamAsync(ChatRequest request, CancellationToken cancellationToken = default);

}
