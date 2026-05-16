using Application.Model.LLM.LlmModel;
using Application.Model.Shared;
using Application.Service.LLM;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebAPI.Core.Filters;

namespace Admin.WebAPI.Controllers;


/// <summary>
/// LLM 模型管理
/// </summary>
[SignVerifyFilter]
[Route("[controller]/[action]")]
[Authorize]
[ApiController]
public class LlmModelController(LlmModelService llmModelService) : ControllerBase
{


    /// <summary>
    /// 获取 LLM 模型配置列表
    /// </summary>
    [HttpGet]
    public Task<PageListDto<LlmModelDto>> GetLlmModelList([FromQuery] LlmModelPageRequestDto request) => llmModelService.GetLlmModelListAsync(request);


    /// <summary>
    /// 获取 LLM 模型下拉列表
    /// </summary>
    [HttpGet]
    public Task<List<LlmModelSelectDto>> GetLlmModelSelect() => llmModelService.GetLlmModelSelectAsync();


    /// <summary>
    /// 创建 LLM 模型配置
    /// </summary>
    [HttpPost]
    public Task<long> CreateLlmModel(EditLlmModelDto createLlmModel) => llmModelService.CreateLlmModelAsync(createLlmModel);


    /// <summary>
    /// 更新 LLM 模型配置
    /// </summary>
    [HttpPost]
    public Task<bool> UpdateLlmModel(long id, EditLlmModelDto updateLlmModel) => llmModelService.UpdateLlmModelAsync(id, updateLlmModel);


    /// <summary>
    /// 删除 LLM 模型配置
    /// </summary>
    [HttpDelete]
    public Task<bool> DeleteLlmModel(long id) => llmModelService.DeleteLlmModelAsync(id);

}
