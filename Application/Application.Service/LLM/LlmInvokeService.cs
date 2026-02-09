using Common;
using LLM;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Repository;
using SourceGenerator.Runtime.Attributes;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Text.Json.Nodes;

namespace Application.Service.LLM;

[RegisterService(Lifetime = ServiceLifetime.Scoped)]
public partial class LlmInvokeService(DatabaseContext db, ILlmClientFactory llmClientFactory)
{
    private static readonly Regex PlaceholderRegex = KeyRegex();

    /// <summary>
    /// 按 LLM 应用 Code 调用对话接口，返回完整响应
    /// </summary>
    /// <param name="code">LLM 应用标记</param>
    /// <param name="parameters">模板参数（用于替换 {{Key}} 占位符）</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<ChatResponse> ChatAsync(string code, Dictionary<string, string> parameters, CancellationToken cancellationToken = default)
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
        return await client.ChatAsync(request, cancellationToken);
    }


    /// <summary>
    /// 按 LLM 应用 Code 调用对话接口，返回首条文本内容（便于业务直接使用）
    /// </summary>
    public async Task<string?> ChatContentAsync(string code, Dictionary<string, string> parameters, CancellationToken cancellationToken = default)
    {
        var response = await ChatAsync(code, parameters, cancellationToken);
        return response.Choices.FirstOrDefault()?.Message.Content;
    }


    /// <summary>
    /// 按 LLM 应用 Code 调用流式对话接口（SSE），返回流式 chunk
    /// </summary>
    public async IAsyncEnumerable<ChatStreamChunk> ChatStreamAsync(
        string code,
        Dictionary<string, string> parameters,
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
        await foreach (var chunk in client.ChatStreamAsync(request, cancellationToken).WithCancellation(cancellationToken))
        {
            yield return chunk;
        }
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

    [GeneratedRegex(@"\{\{\s*(?<required>\*)?\s*(?<key>[^{}\s]+)\s*\}\}", RegexOptions.Compiled)]
    private static partial Regex KeyRegex();
}
