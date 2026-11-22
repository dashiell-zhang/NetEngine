using Application.Interface.LLM;
using Application.Model.LLM.LlmApp;
using Application.Model.Shared;
using Common;
using DistributedLock;
using IdentifierGenerator;
using LLM;
using LLM.SystemOne;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Repository;
using Repository.Database;
using SourceGenerator.Runtime.Attributes;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Application.Service.LLM;

/// <summary>
/// LLM 应用配置服务
/// </summary>
[RegisterService(typeof(ILlmAppService), Lifetime = ServiceLifetime.Scoped)]
public partial class LlmAppService(DatabaseContext db, IdService idService, ILlmClientFactory llmClientFactory, ILlmModelConfigResolver configResolver, IDistributedLock distributedLock) : ILlmAppService
{

    private static readonly Regex PlaceholderRegex = KeyRegex();


    /// <summary>
    /// 获取 LLM 应用配置列表
    /// </summary>
    public async Task<PageListDto<LlmAppDto>> GetLlmAppListAsync(LlmAppPageRequestDto request)
    {

        PageListDto<LlmAppDto> result = new();

        var query = db.LlmApp.Where(t => t.DeleteTime == null).AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(t =>
                t.Code.Contains(keyword) ||
                t.Name.Contains(keyword) ||
                (t.Remark != null && t.Remark.Contains(keyword)));
        }

        if (request.IsEnable != null)
        {
            query = query.Where(t => t.IsEnable == request.IsEnable.Value);
        }

        result.Total = await query.CountAsync();

        if (result.Total != 0)
        {
            var skip = request.Skip();
            if (skip >= result.Total)
            {
                result.List = [];
                return result;
            }

            result.List = await query
                .OrderByDescending(t => t.Id)
                .Select(t => new LlmAppDto
                {
                    Id = t.Id,
                    Code = t.Code,
                    Name = t.Name,
                    LlmModelId = t.LlmModelId,
                    LlmModelName = t.LlmModel != null ? t.LlmModel.Name : string.Empty,
                    ModelId = t.LlmModel != null ? t.LlmModel.ModelId : string.Empty,
                    SystemPromptTemplate = t.SystemPromptTemplate,
                    PromptTemplate = t.PromptTemplate,
                    ExtraBodyJson = t.ExtraBodyJson,
                    ProtocolType = t.LlmModel != null ? t.LlmModel.ProtocolType : -1,
                    IsEnable = t.IsEnable,
                    Remark = t.Remark,
                    CreateTime = t.CreateTime,
                    UpdateTime = t.UpdateTime
                })
                .Skip(skip)
                .Take(request.PageSize)
                .ToListAsync();
        }

        return result;
    }


    /// <summary>
    /// 创建 LLM 应用配置
    /// </summary>
    /// <param name="actorUserId">行为发起人用户ID</param>
    /// <param name="createLlmApp">应用配置创建内容</param>
    /// <returns>应用配置ID</returns>
    public async Task<long> CreateLlmAppAsync(long actorUserId, EditLlmAppDto createLlmApp)
    {

        var code = createLlmApp.Code.Trim();
        var name = createLlmApp.Name.Trim();
        var promptTemplate = createLlmApp.PromptTemplate.Trim();

        ValidateExtraBodyJson(createLlmApp.ExtraBodyJson);

        await using var lockHandle = await distributedLock.TryLockAsync("llm:app:code:" + code);
        if (lockHandle == null)
        {
            throw new CustomException("当前应用 Code 正在处理中，请稍后重试");
        }

        await using var modelLockHandle = await distributedLock.TryLockAsync("llm:model:id:" + createLlmApp.LlmModelId);
        if (modelLockHandle == null)
        {
            throw new CustomException("当前关联模型正在处理中，请稍后重试");
        }

        var isHave = await db.LlmApp.Where(t => t.Code == code && t.DeleteTime == null).AnyAsync();
        if (isHave)
        {
            throw new CustomException("Code 已存在");
        }

        var protocolType = await ValidateLlmModelStateAsync(createLlmApp.LlmModelId, createLlmApp.IsEnable);
        promptTemplate = LlmDecisionSupport.PrepareTemplateForSave(protocolType, createLlmApp.SystemPromptTemplate, promptTemplate, createLlmApp.ExtraBodyJson);

        LlmApp llmApp = new()
        {
            Id = idService.GetId(),
            Code = code,
            Name = name,
            LlmModelId = createLlmApp.LlmModelId,
            SystemPromptTemplate = createLlmApp.SystemPromptTemplate,
            PromptTemplate = promptTemplate,
            ExtraBodyJson = createLlmApp.ExtraBodyJson,
            IsEnable = createLlmApp.IsEnable,
            Remark = createLlmApp.Remark,
            CreateUserId = actorUserId
        };

        db.LlmApp.Add(llmApp);
        await db.SaveChangesAsync();

        return llmApp.Id;
    }


    /// <summary>
    /// 更新 LLM 应用配置
    /// </summary>
    /// <param name="actorUserId">行为发起人用户ID</param>
    /// <param name="id">应用配置ID</param>
    /// <param name="updateLlmApp">应用配置修改内容</param>
    /// <returns>是否更新成功</returns>
    public async Task<bool> UpdateLlmAppAsync(long actorUserId, long id, EditLlmAppDto updateLlmApp)
    {

        var llmApp = await db.LlmApp.Where(t => t.Id == id && t.DeleteTime == null).FirstOrDefaultAsync();

        if (llmApp == null)
        {
            throw new CustomException("无效的 id");
        }

        var code = updateLlmApp.Code.Trim();
        var name = updateLlmApp.Name.Trim();
        var promptTemplate = updateLlmApp.PromptTemplate.Trim();

        ValidateExtraBodyJson(updateLlmApp.ExtraBodyJson);

        await using var lockHandle = await distributedLock.TryLockAsync("llm:app:code:" + code);
        if (lockHandle == null)
        {
            throw new CustomException("当前应用 Code 正在处理中，请稍后重试");
        }

        await using var modelLockHandle = await distributedLock.TryLockAsync("llm:model:id:" + updateLlmApp.LlmModelId);
        if (modelLockHandle == null)
        {
            throw new CustomException("当前关联模型正在处理中，请稍后重试");
        }

        var isHave = await db.LlmApp.Where(t => t.Id != id && t.Code == code && t.DeleteTime == null).AnyAsync();
        if (isHave)
        {
            throw new CustomException("Code 已存在");
        }

        var protocolType = await ValidateLlmModelStateAsync(updateLlmApp.LlmModelId, updateLlmApp.IsEnable);
        promptTemplate = LlmDecisionSupport.PrepareTemplateForSave(protocolType, updateLlmApp.SystemPromptTemplate, promptTemplate, updateLlmApp.ExtraBodyJson);

        llmApp.Code = code;
        llmApp.Name = name;
        llmApp.LlmModelId = updateLlmApp.LlmModelId;
        llmApp.SystemPromptTemplate = updateLlmApp.SystemPromptTemplate;
        llmApp.PromptTemplate = promptTemplate;
        llmApp.ExtraBodyJson = updateLlmApp.ExtraBodyJson;
        llmApp.IsEnable = updateLlmApp.IsEnable;
        llmApp.Remark = updateLlmApp.Remark;
        llmApp.UpdateUserId = actorUserId;

        await db.SaveChangesAsync();

        return true;
    }


    /// <summary>
    /// 删除 LLM 应用配置
    /// </summary>
    /// <param name="actorUserId">行为发起人用户ID</param>
    /// <param name="id">应用配置ID</param>
    /// <returns>是否删除成功</returns>
    public async Task<bool> DeleteLlmAppAsync(long actorUserId, long id)
    {

        var llmApp = await db.LlmApp.Where(t => t.Id == id && t.DeleteTime == null).FirstOrDefaultAsync();

        if (llmApp != null)
        {
            llmApp.DeleteTime = DateTimeOffset.UtcNow;
            llmApp.DeleteUserId = actorUserId;
            await db.SaveChangesAsync();
        }

        return true;
    }


    /// <summary>
    /// 调用测试（不依赖数据库保存）
    /// </summary>
    /// <param name="actorUserId">行为发起人用户ID</param>
    /// <param name="request">LLM 应用测试内容</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>LLM 应用测试结果</returns>
    public async Task<TestLlmAppResultDto> TestLlmAppAsync(long actorUserId, TestLlmAppRequestDto request, CancellationToken cancellationToken = default)
    {

        var config = await configResolver.GetConfigAsync(request.LlmModelId, cancellationToken)
            ?? throw new CustomException("无效的 LlmModelId 或模型已禁用");

        if (string.IsNullOrWhiteSpace(request.PromptTemplate))
        {
            throw new CustomException("待调用的内容模板不能为空");
        }

        var parameters = request.Parameters == null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(request.Parameters, StringComparer.OrdinalIgnoreCase);

        if (config.ProtocolType == (int)LlmProtocolType.SystemOne)
        {
            var decisionRequest = LlmDecisionSupport.CreateRequest(request.PromptTemplate, request.SystemPromptTemplate, request.ExtraBody == null ? null : JsonSerializer.Serialize(request.ExtraBody), parameters);
            DecisionResponse decision;
            try
            {
                decision = await llmClientFactory.CreateDecisionClient(config).DecideAsync(decisionRequest, cancellationToken);
            }
            catch (HttpRequestException exception) when (exception.StatusCode.HasValue)
            {
                throw new CustomException(exception.Message, exception);
            }
            return new TestLlmAppResultDto
            {
                Model = decision.Model,
                ResponseId = decision.RequestId,
                Content = decision.RawContent,
                Usage = decision.Usage == null ? null : new TestLlmAppUsageDto
                {
                    PromptTokens = decision.Usage.InputTokens,
                    CompletionTokens = decision.Usage.OutputTokens,
                    TotalTokens = decision.Usage.InputTokens + decision.Usage.OutputTokens
                }
            };
        }

        var requiredKeys = ExtractRequiredKeys(request.SystemPromptTemplate)
            .Concat(ExtractRequiredKeys(request.PromptTemplate))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (requiredKeys.Count != 0)
        {
            var missing = requiredKeys
                .Where(k => !parameters.TryGetValue(k, out var v) || string.IsNullOrWhiteSpace(v))
                .ToList();

            if (missing.Count != 0)
            {
                throw new CustomException("必传参数未填写: " + string.Join("、", missing));
            }
        }

        var systemPrompt = RenderTemplate(request.SystemPromptTemplate, parameters);
        var prompt = RenderTemplate(request.PromptTemplate, parameters) ?? string.Empty;

        var client = llmClientFactory.CreateClient(config);
        List<ChatMessage> messages = [];

        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            messages.Add(new ChatMessage(ChatRole.System, systemPrompt));
        }

        messages.Add(new ChatMessage(ChatRole.User, prompt));

        var resp = await client.ChatAsync(
            new ChatRequest(
                config.ModelId,
                messages,
                actorUserId.ToString(),
                request.ExtraBody),
            cancellationToken);

        var content = resp.Choices.FirstOrDefault()?.Message.Content;

        return new TestLlmAppResultDto
        {
            Model = resp.Model,
            ResponseId = resp.Id,
            Content = content,
            Usage = resp.Usage == null
                ? null
                : new TestLlmAppUsageDto
                {
                    PromptTokens = resp.Usage.PromptTokens,
                    CompletionTokens = resp.Usage.CompletionTokens,
                    TotalTokens = resp.Usage.TotalTokens
                }
        };
    }


    /// <summary>
    /// 渲染提示词模板
    /// </summary>
    private static string? RenderTemplate(string? template, IReadOnlyDictionary<string, string> parameters)
    {

        if (template == null)
        {
            return null;
        }

        if (template.Length == 0)
        {
            return template;
        }

        return PlaceholderRegex.Replace(template, m =>
        {
            var key = m.Groups["key"].Value;
            if (string.IsNullOrWhiteSpace(key))
            {
                return m.Value;
            }

            if (parameters.TryGetValue(key, out var value))
            {
                return value ?? string.Empty;
            }

            return m.Value;
        });
    }


    /// <summary>
    /// 提取必填占位参数
    /// </summary>
    private static IEnumerable<string> ExtractRequiredKeys(string? template)
    {

        if (string.IsNullOrWhiteSpace(template))
        {
            yield break;
        }

        foreach (Match m in PlaceholderRegex.Matches(template))
        {
            if (!m.Groups["required"].Success)
            {
                continue;
            }

            var key = m.Groups["key"].Value?.Trim();
            if (!string.IsNullOrWhiteSpace(key))
            {
                yield return key;
            }
        }
    }


    [GeneratedRegex(@"\{\{\s*(?<required>\*)?\s*(?<key>[^{}|\s]+)\s*(?:\|\s*(?<comment>[^{}]*?))?\s*\}\}", RegexOptions.Compiled)]
    private static partial Regex KeyRegex();


    /// <summary>
    /// 校验 LLM 应用关联模型的状态并返回协议类型
    /// </summary>
    private async Task<int> ValidateLlmModelStateAsync(long llmModelId, bool appIsEnable)
    {

        var model = await db.LlmModel
            .Where(t => t.Id == llmModelId && t.DeleteTime == null)
            .Select(t => new { t.IsEnable, t.ProtocolType })
            .FirstOrDefaultAsync();

        if (model == null)
        {
            throw new CustomException("无效的 LlmModelId");
        }

        if (appIsEnable && !model.IsEnable)
        {
            throw new CustomException("启用的 LLM 应用不可以关联已禁用模型");
        }

        return model.ProtocolType;

    }


    /// <summary>
    /// 校验额外请求体 JSON
    /// </summary>
    private static void ValidateExtraBodyJson(string? extraBodyJson)
    {

        if (string.IsNullOrWhiteSpace(extraBodyJson))
        {
            return;
        }

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(extraBodyJson);
        }
        catch
        {
            throw new CustomException("ExtraBodyJson 不是合法的 JSON");
        }

        if (node is not JsonObject)
        {
            throw new CustomException("ExtraBodyJson 必须是 JSON 对象（例如 {\"enable_thinking\":true}）");
        }
    }
}
