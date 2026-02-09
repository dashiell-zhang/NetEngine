using Application.Interface;
using Application.Model.LLM.LlmApp;
using Application.Model.Shared;
using Common;
using IdentifierGenerator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Repository;
using Repository.Database;
using SourceGenerator.Runtime.Attributes;

namespace Application.Service.LLM;

[RegisterService(Lifetime = ServiceLifetime.Scoped)]
public class LlmAppService(DatabaseContext db, IdService idService, IUserContext userContext)
{

    /// <summary>
    /// 获取 LLM 应用配置列表
    /// </summary>
    public async Task<PageListDto<LlmAppDto>> GetLlmAppListAsync(LlmAppPageRequestDto request)
    {
        PageListDto<LlmAppDto> result = new();

        var query = db.LlmApp.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(t =>
                t.Code.Contains(keyword) ||
                t.Name.Contains(keyword) ||
                t.Provider.Contains(keyword) ||
                t.Model.Contains(keyword) ||
                (t.Remark != null && t.Remark.Contains(keyword)));
        }

        if (!string.IsNullOrWhiteSpace(request.Provider))
        {
            var provider = request.Provider.Trim();
            query = query.Where(t => t.Provider == provider);
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
                .Select(t => new LlmAppDto
                {
                    Id = t.Id,
                    Code = t.Code,
                    Name = t.Name,
                    Provider = t.Provider,
                    Model = t.Model,
                    SystemPromptTemplate = t.SystemPromptTemplate,
                    PromptTemplate = t.PromptTemplate,
                    MaxTokens = t.MaxTokens,
                    Temperature = t.Temperature,
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
    /// 创建 LLM 应用配置
    /// </summary>
    public async Task<long> CreateLlmAppAsync(EditLlmAppDto createLlmApp)
    {
        var code = createLlmApp.Code.Trim();
        var name = createLlmApp.Name.Trim();
        var provider = createLlmApp.Provider.Trim();
        var model = createLlmApp.Model.Trim();
        var promptTemplate = createLlmApp.PromptTemplate.Trim();

        var isHave = await db.LlmApp.AsNoTracking().Where(t => t.Code == code).AnyAsync();
        if (isHave)
        {
            throw new CustomException("Code 已存在");
        }

        LlmApp llmApp = new()
        {
            Id = idService.GetId(),
            Code = code,
            Name = name,
            Provider = provider,
            Model = model,
            SystemPromptTemplate = createLlmApp.SystemPromptTemplate,
            PromptTemplate = promptTemplate,
            MaxTokens = createLlmApp.MaxTokens,
            Temperature = createLlmApp.Temperature,
            IsEnable = createLlmApp.IsEnable,
            Remark = createLlmApp.Remark,
            CreateUserId = userContext.UserId
        };

        db.LlmApp.Add(llmApp);
        await db.SaveChangesAsync();

        return llmApp.Id;
    }


    /// <summary>
    /// 更新 LLM 应用配置
    /// </summary>
    public async Task<bool> UpdateLlmAppAsync(long id, EditLlmAppDto updateLlmApp)
    {
        var llmApp = await db.LlmApp.Where(t => t.Id == id).FirstOrDefaultAsync();

        if (llmApp == null)
        {
            throw new CustomException("无效的 id");
        }

        var code = updateLlmApp.Code.Trim();
        var name = updateLlmApp.Name.Trim();
        var provider = updateLlmApp.Provider.Trim();
        var model = updateLlmApp.Model.Trim();
        var promptTemplate = updateLlmApp.PromptTemplate.Trim();

        var isHave = await db.LlmApp.AsNoTracking().Where(t => t.Id != id && t.Code == code).AnyAsync();
        if (isHave)
        {
            throw new CustomException("Code 已存在");
        }

        llmApp.Code = code;
        llmApp.Name = name;
        llmApp.Provider = provider;
        llmApp.Model = model;
        llmApp.SystemPromptTemplate = updateLlmApp.SystemPromptTemplate;
        llmApp.PromptTemplate = promptTemplate;
        llmApp.MaxTokens = updateLlmApp.MaxTokens;
        llmApp.Temperature = updateLlmApp.Temperature;
        llmApp.IsEnable = updateLlmApp.IsEnable;
        llmApp.Remark = updateLlmApp.Remark;
        llmApp.UpdateUserId = userContext.UserId;

        await db.SaveChangesAsync();

        return true;
    }


    /// <summary>
    /// 删除 LLM 应用配置
    /// </summary>
    public async Task<bool> DeleteLlmAppAsync(long id)
    {
        var llmApp = await db.LlmApp.Where(t => t.Id == id).FirstOrDefaultAsync();

        if (llmApp != null)
        {
            llmApp.IsDelete = true;
            llmApp.DeleteUserId = userContext.UserId;
            await db.SaveChangesAsync();
        }

        return true;
    }

}
