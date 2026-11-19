using LLM.SystemOne;

namespace LLM;

/// <summary>
/// 结构化决策客户端，仅提供一次性决策结果
/// </summary>
public interface ILlmDecisionClient
{

    /// <summary>
    /// 根据业务状态和问题定义返回决策结果
    /// </summary>
    Task<DecisionResponse> DecideAsync(DecisionRequest request, CancellationToken cancellationToken = default);

}
