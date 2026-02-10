using Common;
using IdentifierGenerator;
using LLM;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Application.Interface;
using Repository;
using Repository.Database;
using SourceGenerator.Runtime.Attributes;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Text;
using System.Text.Json.Nodes;

namespace Application.Service.LLM;

[RegisterService(Lifetime = ServiceLifetime.Scoped)]
public partial class LlmInvokeService(DatabaseContext db, IdService idService, IUserContext userContext, ILlmClientFactory llmClientFactory)
{
    private static readonly Regex PlaceholderRegex = KeyRegex();
    private readonly long trackKey = idService.GetId();

    /// <summary>
    /// 按 LLM 应用 Code 调用对话接口，返回完整响应
    /// </summary>
    /// <param name="code">LLM 应用标记</param>
    /// <param name="parameters">模板参数（用于替换 {{Key}} 占位符）</param>
    /// <param name="conversationKey">可选：会话Key（用于拼接历史对话）</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<ChatResponse> ChatAsync(
        string code,
        Dictionary<string, string> parameters,
        long? conversationKey = null,
        CancellationToken cancellationToken = default)
    {
        var app = await db.LlmApp.AsNoTracking().Where(t => t.Code == code && t.IsEnable).FirstOrDefaultAsync(cancellationToken);
        if (app == null)
        {
            throw new InvalidOperationException($"LLM app not found or disabled: {code}");
        }

        if (string.IsNullOrWhiteSpace(app.Provider))
        {
            throw new InvalidOperationException($"LLM app provider is required: {code}");
        }

        if (string.IsNullOrWhiteSpace(app.Model))
        {
            throw new InvalidOperationException($"LLM app model is required: {code}");
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

        var effectiveConversationKey = await GetEffectiveConversationKeyAsync(conversationKey, app.Id, cancellationToken);
        if (effectiveConversationKey != null)
        {
            var history = await LoadConversationHistoryAsync(effectiveConversationKey.Value, app.Id, cancellationToken);
            if (history.Count != 0)
            {
                messages.AddRange(history);
            }
        }

        messages.Add(new ChatMessage(ChatRole.User, userPrompt));

        var extraBody = ParseExtraBodyJson(app.ExtraBodyJson, code);

        var request = new ChatRequest(
            app.Model,
            messages,
            app.Temperature,
            app.MaxTokens,
            ExtraBody: extraBody
        );

        var client = llmClientFactory.GetClient(app.Provider);
        var response = await client.ChatAsync(request, cancellationToken);

        var assistantContent = response.Choices.FirstOrDefault()?.Message.Content ?? string.Empty;
        await SaveConversationAsync(app.Id, effectiveConversationKey, userPrompt, assistantContent, cancellationToken);

        return response;
    }


    /// <summary>
    /// 按 LLM 应用 Code 调用对话接口，返回首条文本内容（便于业务直接使用）
    /// </summary>
    public async Task<string?> ChatContentAsync(
        string code,
        Dictionary<string, string> parameters,
        long? conversationKey = null,
        CancellationToken cancellationToken = default)
    {
        var response = await ChatAsync(code, parameters, conversationKey, cancellationToken);
        return response.Choices.FirstOrDefault()?.Message.Content;
    }


    /// <summary>
    /// 按 LLM 应用 Code 调用流式对话接口（SSE），返回流式 chunk
    /// </summary>
    public async IAsyncEnumerable<ChatStreamChunk> ChatStreamAsync(
        string code,
        Dictionary<string, string> parameters,
        long? conversationKey = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var app = await db.LlmApp.AsNoTracking().Where(t => t.Code == code && t.IsEnable).FirstOrDefaultAsync(cancellationToken);
        if (app == null)
        {
            throw new InvalidOperationException($"LLM app not found or disabled: {code}");
        }

        if (string.IsNullOrWhiteSpace(app.Provider))
        {
            throw new InvalidOperationException($"LLM app provider is required: {code}");
        }

        if (string.IsNullOrWhiteSpace(app.Model))
        {
            throw new InvalidOperationException($"LLM app model is required: {code}");
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

        var effectiveConversationKey = await GetEffectiveConversationKeyAsync(conversationKey, app.Id, cancellationToken);
        if (effectiveConversationKey != null)
        {
            var history = await LoadConversationHistoryAsync(effectiveConversationKey.Value, app.Id, cancellationToken);
            if (history.Count != 0)
            {
                messages.AddRange(history);
            }
        }

        messages.Add(new ChatMessage(ChatRole.User, userPrompt));

        var extraBody = ParseExtraBodyJson(app.ExtraBodyJson, code);

        var request = new ChatRequest(
            app.Model,
            messages,
            app.Temperature,
            app.MaxTokens,
            ExtraBody: extraBody
        );

        var client = llmClientFactory.GetClient(app.Provider);
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

        await SaveConversationAsync(app.Id, effectiveConversationKey, userPrompt, assistantBuilder.ToString(), cancellationToken);
    }

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

    private async Task<long?> GetEffectiveConversationKeyAsync(long? conversationKey, long llmAppId, CancellationToken cancellationToken)
    {
        if (conversationKey == null)
        {
            return null;
        }

        var otherAppId = await db.LlmConversation.AsNoTracking()
            .Where(t => t.ConversationKey == conversationKey.Value)
            .OrderBy(t => t.Id)
            .Select(t => t.LlmAppId)
            .FirstOrDefaultAsync(cancellationToken);

        if (otherAppId != default && otherAppId != llmAppId)
        {
            return null;
        }

        return conversationKey.Value;
    }

    private async Task<List<ChatMessage>> LoadConversationHistoryAsync(long conversationKey, long llmAppId, CancellationToken cancellationToken)
    {
        var list = await db.LlmConversation.AsNoTracking()
            .Where(t => t.ConversationKey == conversationKey && t.LlmAppId == llmAppId)
            .OrderBy(t => t.Id)
            .Select(t => new { t.Role, t.Content })
            .ToListAsync(cancellationToken);

        if (list.Count == 0)
        {
            return [];
        }

        List<ChatMessage> messages = new(list.Count);
        foreach (var item in list)
        {
            var role = ParseRole(item.Role);
            messages.Add(new ChatMessage(role, item.Content ?? string.Empty));
        }

        return messages;
    }

    private async Task SaveConversationAsync(long llmAppId, long? conversationKey, string userPrompt, string assistantContent, CancellationToken cancellationToken)
    {
        long? createUserId = userContext.IsAuthenticated && userContext.UserId != default ? userContext.UserId : null;

        db.LlmConversation.Add(new LlmConversation
        {
            Id = idService.GetId(),
            TrackKey = trackKey,
            ConversationKey = conversationKey,
            LlmAppId = llmAppId,
            Role = "user",
            Content = userPrompt ?? string.Empty,
            CreateUserId = createUserId
        });

        db.LlmConversation.Add(new LlmConversation
        {
            Id = idService.GetId(),
            TrackKey = trackKey,
            ConversationKey = conversationKey,
            LlmAppId = llmAppId,
            Role = "assistant",
            Content = assistantContent ?? string.Empty,
            CreateUserId = createUserId
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    private static ChatRole ParseRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return ChatRole.User;
        }

        return role.Trim().ToLowerInvariant() switch
        {
            "system" => ChatRole.System,
            "user" => ChatRole.User,
            "assistant" => ChatRole.Assistant,
            "tool" => ChatRole.Tool,
            _ => ChatRole.User
        };
    }

    [GeneratedRegex(@"\{\{\s*(?<required>\*)?\s*(?<key>[^{}\s]+)\s*\}\}", RegexOptions.Compiled)]
    private static partial Regex KeyRegex();
}
