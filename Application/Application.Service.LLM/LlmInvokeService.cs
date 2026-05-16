using Application.Interface;
using Common;
using IdentifierGenerator;
using LLM;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Repository;
using Repository.Database;
using SourceGenerator.Runtime.Attributes;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Application.Service.LLM;

/// <summary>
/// LLM 调用服务
/// </summary>
[RegisterService(Lifetime = ServiceLifetime.Scoped)]
public partial class LlmInvokeService(DatabaseContext db, IdService idService, IUserContext userContext, ILlmClientFactory llmClientFactory, ILlmModelConfigResolver configResolver)
{

    private static readonly Regex PlaceholderRegex = KeyRegex();


    /// <summary>
    /// 按 LLM 应用 Code 调用对话接口，返回完整响应
    /// </summary>
    public async Task<ChatResponse> ChatAsync(string code, Dictionary<string, string> parameters, CancellationToken cancellationToken = default)
    {

        var app = await db.LlmApp.AsNoTracking().Where(t => t.Code == code && t.IsEnable && t.DeleteTime == null).FirstOrDefaultAsync(cancellationToken);
        if (app == null)
        {
            throw new InvalidOperationException($"LLM app not found or disabled: {code}");
        }

        if (string.IsNullOrWhiteSpace(app.PromptTemplate))
        {
            throw new InvalidOperationException($"LLM app prompt template is required: {code}");
        }

        ValidateRequiredParameters(app.SystemPromptTemplate, app.PromptTemplate, parameters);

        var systemPrompt = RenderTemplate(app.SystemPromptTemplate, parameters);
        var userPrompt = RenderTemplate(app.PromptTemplate, parameters);

        List<ChatMessage> messages = [];
        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            messages.Add(new ChatMessage(ChatRole.System, systemPrompt));
        }

        messages.Add(new ChatMessage(ChatRole.User, userPrompt));

        var extraBody = ParseExtraBodyJson(app.ExtraBodyJson, code);

        var config = await configResolver.GetConfigAsync(app.LlmModelId, cancellationToken)
            ?? throw new InvalidOperationException($"LLM model config not found or disabled for app: {code}");

        var request = new ChatRequest(
            config.ModelId,
            messages,
            ExtraBody: extraBody
        );

        var client = await llmClientFactory.GetClientAsync(app.LlmModelId, configResolver);
        var response = await client.ChatAsync(request, cancellationToken);

        var assistantContent = response.Choices.FirstOrDefault()?.Message.Content ?? string.Empty;
        await SaveConversationAsync(app.Id, systemPrompt, userPrompt, assistantContent, cancellationToken);

        return response;
    }


    /// <summary>
    /// 按 LLM 应用 Code 调用对话接口，返回首条文本内容
    /// </summary>
    public async Task<string?> ChatContentAsync(string code, Dictionary<string, string> parameters, CancellationToken cancellationToken = default)
    {

        var response = await ChatAsync(code, parameters, cancellationToken);
        return response.Choices.FirstOrDefault()?.Message.Content;
    }


    /// <summary>
    /// 按 LLM 应用 Code 调用流式对话接口
    /// </summary>
    public async IAsyncEnumerable<ChatStreamChunk> ChatStreamAsync(string code, Dictionary<string, string> parameters, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {

        var app = await db.LlmApp.AsNoTracking().Where(t => t.Code == code && t.IsEnable && t.DeleteTime == null).FirstOrDefaultAsync(cancellationToken);
        if (app == null)
        {
            throw new InvalidOperationException($"LLM app not found or disabled: {code}");
        }

        if (string.IsNullOrWhiteSpace(app.PromptTemplate))
        {
            throw new InvalidOperationException($"LLM app prompt template is required: {code}");
        }

        ValidateRequiredParameters(app.SystemPromptTemplate, app.PromptTemplate, parameters);

        var systemPrompt = RenderTemplate(app.SystemPromptTemplate, parameters);
        var userPrompt = RenderTemplate(app.PromptTemplate, parameters);

        List<ChatMessage> messages = [];
        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            messages.Add(new ChatMessage(ChatRole.System, systemPrompt));
        }

        messages.Add(new ChatMessage(ChatRole.User, userPrompt));

        var extraBody = ParseExtraBodyJson(app.ExtraBodyJson, code);

        var config = await configResolver.GetConfigAsync(app.LlmModelId, cancellationToken)
            ?? throw new InvalidOperationException($"LLM model config not found or disabled for app: {code}");

        var request = new ChatRequest(
            config.ModelId,
            messages,
            ExtraBody: extraBody
        );

        var client = await llmClientFactory.GetClientAsync(app.LlmModelId, configResolver);
        var assistantBuilder = new StringBuilder(2048);
        await foreach (var chunk in client.ChatStreamAsync(request, cancellationToken).WithCancellation(cancellationToken))
        {
            var delta = chunk.Choices.FirstOrDefault()?.Delta?.Content;
            if (!string.IsNullOrEmpty(delta))
            {
                assistantBuilder.Append(delta);
            }

            yield return chunk;
        }

        await SaveConversationAsync(app.Id, systemPrompt, userPrompt, assistantBuilder.ToString(), cancellationToken);
    }


    /// <summary>
    /// 解析额外请求体 JSON
    /// </summary>
    private static Dictionary<string, JsonNode>? ParseExtraBodyJson(string? extraBodyJson, string code)
    {

        if (string.IsNullOrWhiteSpace(extraBodyJson))
        {
            return null;
        }

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(extraBodyJson);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"LLM app ExtraBodyJson is invalid JSON: {code}", ex);
        }

        if (node is not JsonObject obj)
        {
            throw new InvalidOperationException($"LLM app ExtraBodyJson must be a JSON object: {code}");
        }

        Dictionary<string, JsonNode> dict = new(StringComparer.Ordinal);
        foreach (var kv in obj)
        {
            if (string.IsNullOrWhiteSpace(kv.Key))
            {
                continue;
            }

            if (kv.Value != null)
            {
                dict[kv.Key] = kv.Value;
            }
        }

        return dict.Count == 0 ? null : dict;
    }


    /// <summary>
    /// 校验必填模板参数
    /// </summary>
    private static void ValidateRequiredParameters(string? systemPromptTemplate, string? promptTemplate, Dictionary<string, string> parameters)
    {

        var requiredKeys = ExtractRequiredKeys(systemPromptTemplate)
            .Concat(ExtractRequiredKeys(promptTemplate))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (requiredKeys.Count == 0)
        {
            return;
        }

        var missing = requiredKeys
            .Where(k => !parameters.TryGetValue(k, out var v) || string.IsNullOrWhiteSpace(v))
            .ToList();

        if (missing.Count != 0)
        {
            throw new CustomException("必传参数未填写: " + string.Join("、", missing));
        }
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


    /// <summary>
    /// 渲染提示词模板
    /// </summary>
    private static string RenderTemplate(string? template, Dictionary<string, string> parameters)
    {

        if (string.IsNullOrWhiteSpace(template))
        {
            return string.Empty;
        }

        return PlaceholderRegex.Replace(template, m =>
        {
            var key = m.Groups["key"].Value;
            if (string.IsNullOrWhiteSpace(key))
            {
                return m.Value;
            }

            return parameters.TryGetValue(key, out var value) ? (value ?? string.Empty) : m.Value;
        });
    }


    /// <summary>
    /// 保存对话记录
    /// </summary>
    private async Task SaveConversationAsync(long llmAppId, string systemPrompt, string userPrompt, string assistantContent, CancellationToken cancellationToken)
    {

        long? createUserId = userContext.IsAuthenticated ? userContext.UserId : null;

        db.LlmConversation.Add(new LlmConversation
        {
            Id = idService.GetId(),
            LlmAppId = llmAppId,
            SystemContent = systemPrompt ?? string.Empty,
            UserContent = userPrompt ?? string.Empty,
            AssistantContent = assistantContent ?? string.Empty,
            CreateUserId = createUserId
        });

        await db.SaveChangesAsync(cancellationToken);
    }


    [GeneratedRegex(@"\{\{\s*(?<required>\*)?\s*(?<key>[^{}\s]+)\s*\}\}", RegexOptions.Compiled)]
    private static partial Regex KeyRegex();
}
