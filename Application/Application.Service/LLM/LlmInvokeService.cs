using LLM;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Repository;
using SourceGenerator.Runtime.Attributes;
using System.Runtime.CompilerServices;

namespace Application.Service.LLM;

[RegisterService(Lifetime = ServiceLifetime.Scoped)]
public class LlmInvokeService(DatabaseContext db, ILlmClientFactory llmClientFactory)
{

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

        var systemPrompt = RenderTemplate(app.SystemPromptTemplate, parameters);
        var userPrompt = RenderTemplate(app.PromptTemplate, parameters);

        List<ChatMessage> messages = [];
        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            messages.Add(new ChatMessage(ChatRole.System, systemPrompt));
        }

        messages.Add(new ChatMessage(ChatRole.User, userPrompt));

        var request = new ChatRequest(
            app.Model,
            messages,
            app.Temperature,
            app.MaxTokens
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

        var systemPrompt = RenderTemplate(app.SystemPromptTemplate, parameters);
        var userPrompt = RenderTemplate(app.PromptTemplate, parameters);

        List<ChatMessage> messages = [];
        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            messages.Add(new ChatMessage(ChatRole.System, systemPrompt));
        }

        messages.Add(new ChatMessage(ChatRole.User, userPrompt));

        var request = new ChatRequest(
            app.Model,
            messages,
            app.Temperature,
            app.MaxTokens
        );

        var client = llmClientFactory.GetClient(app.Provider);
        await foreach (var chunk in client.ChatStreamAsync(request, cancellationToken).WithCancellation(cancellationToken))
        {
            yield return chunk;
        }
    }


    private static string RenderTemplate(string? template, Dictionary<string, string> parameters)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return string.Empty;
        }

        var result = template;
        foreach (var kv in parameters)
        {
            var key = kv.Key?.Trim();
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            result = result.Replace("{{" + key + "}}", kv.Value ?? string.Empty, StringComparison.Ordinal);
        }

        return result;
    }
}
