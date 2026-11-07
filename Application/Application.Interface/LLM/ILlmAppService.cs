using Application.Model.LLM.LlmApp;
using Application.Model.Shared;

namespace Application.Interface.LLM;

/// <summary>
/// LLM 应用配置与测试调用
/// </summary>
public interface ILlmAppService
{

    /// <summary>
    /// 获取 LLM 应用配置列表
    /// </summary>
    Task<PageListDto<LlmAppDto>> GetLlmAppListAsync(LlmAppPageRequestDto request);


    /// <summary>
    /// 创建 LLM 应用配置
    /// </summary>
    Task<long> CreateLlmAppAsync(long actorUserId, EditLlmAppDto createLlmApp);


    /// <summary>
    /// 更新 LLM 应用配置
    /// </summary>
    Task<bool> UpdateLlmAppAsync(long actorUserId, long id, EditLlmAppDto updateLlmApp);


    /// <summary>
    /// 删除 LLM 应用配置
    /// </summary>
    Task<bool> DeleteLlmAppAsync(long actorUserId, long id);


    /// <summary>
    /// 调用测试（不依赖数据库保存）
    /// </summary>
    Task<TestLlmAppResultDto> TestLlmAppAsync(long actorUserId, TestLlmAppRequestDto request, CancellationToken cancellationToken = default);

}
