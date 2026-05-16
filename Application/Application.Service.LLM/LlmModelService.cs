using Application.Interface;
using Application.Model.LLM.LlmModel;
using Application.Model.Shared;
using Common;
using IdentifierGenerator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Repository;
using Repository.Database;
using SourceGenerator.Runtime.Attributes;

namespace Application.Service.LLM;

/// <summary>
/// LLM 模型配置服务
/// </summary>
[RegisterService(Lifetime = ServiceLifetime.Scoped)]
public class LlmModelService(DatabaseContext db, IdService idService, IUserContext userContext)
{

    /// <summary>
    /// 获取 LLM 模型配置列表
    /// </summary>
    public async Task<PageListDto<LlmModelDto>> GetLlmModelListAsync(LlmModelPageRequestDto request)
    {

        PageListDto<LlmModelDto> result = new();

        var query = db.LlmModel.Where(t => t.DeleteTime == null).AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(t =>
                t.Name.Contains(keyword) ||
                t.ModelId.Contains(keyword) ||
                (t.Remark != null && t.Remark.Contains(keyword)));
        }

        if (request.IsEnable != null)
        {
            query = query.Where(t => t.IsEnable == request.IsEnable.Value);
        }

        result.Total = await query.CountAsync();

        if (result.Total != 0)
        {
            result.List = await query
                .OrderByDescending(t => t.Id)
                .Select(t => new LlmModelDto
                {
                    Id = t.Id,
                    Name = t.Name,
                    ModelId = t.ModelId,
                    Endpoint = t.Endpoint,
                    ApiKey = t.ApiKey,
                    ProtocolType = t.ProtocolType,
                    IsEnable = t.IsEnable,
                    Remark = t.Remark,
                    CreateTime = t.CreateTime,
                    UpdateTime = t.UpdateTime
                })
                .Skip(request.Skip())
                .Take(request.PageSize)
                .ToListAsync();
        }

        return result;
    }


    /// <summary>
    /// 创建 LLM 模型配置
    /// </summary>
    public async Task<long> CreateLlmModelAsync(EditLlmModelDto createLlmModel)
    {

        var name = createLlmModel.Name.Trim();
        var modelId = createLlmModel.ModelId.Trim();
        var endpoint = createLlmModel.Endpoint.Trim();
        var apiKey = createLlmModel.ApiKey.Trim();

        var isHave = await db.LlmModel.Where(t => t.Name == name && t.DeleteTime == null).AnyAsync();
        if (isHave)
        {
            throw new CustomException("名称已存在");
        }

        LlmModel llmModel = new()
        {
            Id = idService.GetId(),
            Name = name,
            ModelId = modelId,
            Endpoint = endpoint,
            ApiKey = apiKey,
            ProtocolType = createLlmModel.ProtocolType,
            IsEnable = createLlmModel.IsEnable,
            Remark = createLlmModel.Remark,
            CreateUserId = userContext.UserId
        };

        db.LlmModel.Add(llmModel);
        await db.SaveChangesAsync();

        return llmModel.Id;
    }


    /// <summary>
    /// 更新 LLM 模型配置
    /// </summary>
    public async Task<bool> UpdateLlmModelAsync(long id, EditLlmModelDto updateLlmModel)
    {

        var llmModel = await db.LlmModel.Where(t => t.Id == id && t.DeleteTime == null).FirstOrDefaultAsync();

        if (llmModel == null)
        {
            throw new CustomException("无效的 id");
        }

        var name = updateLlmModel.Name.Trim();
        var modelId = updateLlmModel.ModelId.Trim();
        var endpoint = updateLlmModel.Endpoint.Trim();
        var apiKey = updateLlmModel.ApiKey.Trim();

        var isHave = await db.LlmModel.Where(t => t.Id != id && t.Name == name && t.DeleteTime == null).AnyAsync();
        if (isHave)
        {
            throw new CustomException("名称已存在");
        }

        llmModel.Name = name;
        llmModel.ModelId = modelId;
        llmModel.Endpoint = endpoint;
        llmModel.ApiKey = apiKey;
        llmModel.ProtocolType = updateLlmModel.ProtocolType;
        llmModel.IsEnable = updateLlmModel.IsEnable;
        llmModel.Remark = updateLlmModel.Remark;
        llmModel.UpdateUserId = userContext.UserId;

        await db.SaveChangesAsync();

        return true;
    }


    /// <summary>
    /// 删除 LLM 模型配置
    /// </summary>
    public async Task<bool> DeleteLlmModelAsync(long id)
    {

        var llmModel = await db.LlmModel.Where(t => t.Id == id && t.DeleteTime == null).FirstOrDefaultAsync();

        if (llmModel != null)
        {
            llmModel.DeleteTime = DateTimeOffset.UtcNow;
            llmModel.DeleteUserId = userContext.UserId;
            await db.SaveChangesAsync();
        }

        return true;
    }


    /// <summary>
    /// 获取 LLM 模型下拉列表（仅启用的模型）
    /// </summary>
    public async Task<List<LlmModelSelectDto>> GetLlmModelSelectAsync()
    {
        return await db.LlmModel
            .AsNoTracking()
            .Where(t => t.IsEnable && t.DeleteTime == null)
            .OrderBy(t => t.Name)
            .Select(t => new LlmModelSelectDto
            {
                Id = t.Id,
                Name = t.Name
            })
            .ToListAsync();
    }

}
