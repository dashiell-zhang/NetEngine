using Application.Model.LLM.Decision;
using LLM;
using Microsoft.EntityFrameworkCore;

namespace Application.Service.LLM;

/// <summary>
/// 按应用配置执行结构化决策并写入会话日志
/// </summary>
public partial class LlmInvokeService
{

    /// <summary>
    /// 按应用编码调用决策模型，保留概率和连续评分
    /// </summary>
    public async Task<LlmDecisionResponseDto> DecideAsync(long? actorUserId, string code, Dictionary<string, string> parameters, CancellationToken cancellationToken = default)
    {

        ArgumentNullException.ThrowIfNull(parameters);
        var app = await db.LlmApp.AsNoTracking().FirstOrDefaultAsync(item => item.Code == code && item.IsEnable, cancellationToken)
            ?? throw new InvalidOperationException("决策应用不存在或未启用");
        var config = await configResolver.GetConfigAsync(app.LlmModelId, cancellationToken)
            ?? throw new InvalidOperationException("关联模型不存在或未启用");
        if (config.ProtocolType != (int)LlmProtocolType.SystemOne)
        {
            throw new InvalidOperationException("该应用不是决策应用，请使用聊天接口");
        }

        var request = LlmDecisionSupport.CreateRequest(app.PromptTemplate, app.SystemPromptTemplate, app.ExtraBodyJson, parameters);
        var response = await llmClientFactory.CreateDecisionClient(config).DecideAsync(request, cancellationToken);
        await TrySaveConversationAsync(actorUserId, app.Id, string.Empty, response.RequestJson, response.RawContent, cancellationToken);
        return LlmDecisionSupport.Map(response);

    }

}
