using Application.Model.LLM.LlmConversation;
using Application.Model.Shared;

namespace Application.Interface.LLM;

/// <summary>
/// LLM 对话记录查询
/// </summary>
public interface ILlmConversationManageService
{

    /// <summary>
    /// 获取 LLM 调用日志列表
    /// </summary>
    Task<PageListDto<LlmConversationDto>> GetLlmConversationListAsync(LlmConversationPageRequestDto request);

}
