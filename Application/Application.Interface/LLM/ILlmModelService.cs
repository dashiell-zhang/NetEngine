using Application.Model.LLM.LlmModel;
using Application.Model.Shared;

namespace Application.Interface.LLM;

/// <summary>
/// LLM 模型配置管理
/// </summary>
public interface ILlmModelService
{

    /// <summary>
    /// 获取 LLM 模型配置列表
    /// </summary>
    Task<PageListDto<LlmModelDto>> GetLlmModelListAsync(LlmModelPageRequestDto request);


    /// <summary>
    /// 创建 LLM 模型配置
    /// </summary>
    Task<long> CreateLlmModelAsync(long actorUserId, EditLlmModelDto createLlmModel);


    /// <summary>
    /// 更新 LLM 模型配置
    /// </summary>
    Task<bool> UpdateLlmModelAsync(long actorUserId, long id, EditLlmModelDto updateLlmModel);


    /// <summary>
    /// 删除 LLM 模型配置
    /// </summary>
    Task<bool> DeleteLlmModelAsync(long actorUserId, long id);


    /// <summary>
    /// 获取 LLM 模型下拉列表（仅启用的模型）
    /// </summary>
    Task<List<LlmModelSelectDto>> GetLlmModelSelectAsync();

}
