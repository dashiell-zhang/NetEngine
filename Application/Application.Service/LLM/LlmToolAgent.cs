using Common;
using LLM;
using Microsoft.Extensions.DependencyInjection;
using SourceGenerator.Runtime.Attributes;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Application.Service.LLM;

[RegisterService(Lifetime = ServiceLifetime.Scoped)]
public sealed class LlmToolAgent(ILlmClientFactory llmClientFactory)
{


    public async Task<LlmToolAgentResult> RunAsync(
        string provider,
        string model,
        IReadOnlyList<ChatMessage> messages,
        IReadOnlyList<LlmAgentTool> tools,
        ToolChoice? toolChoice = null,
        int maxSteps = 8,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            throw new ArgumentException("Provider is required.", nameof(provider));
        }

        if (string.IsNullOrWhiteSpace(model))
        {
            throw new ArgumentException("Model is required.", nameof(model));
        }

        if (messages == null)
        {
            throw new ArgumentNullException(nameof(messages));
        }

        if (tools == null)
        {
            throw new ArgumentNullException(nameof(tools));
        }

        if (maxSteps <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxSteps));
        }

        var client = llmClientFactory.GetClient(provider.Trim());

        var toolList = tools.Select(t => t.Definition).ToList();
        var toolMap = tools.ToDictionary(t => t.Name, StringComparer.Ordinal);

        var history = messages.ToList();
        var initialChoice = toolChoice ?? ToolChoice.Auto;
        var currentChoice = initialChoice;

        ChatResponse? lastResponse = null;
        ChatMessage? lastAssistant = null;

        for (var step = 0; step < maxSteps; step++)
        {
            lastResponse = await client.ChatAsync(
                new ChatRequest(
                    model.Trim(),
                    history,
                    Tools: toolList,
                    ToolChoice: currentChoice),
                cancellationToken);

            lastAssistant = lastResponse.Choices.FirstOrDefault()?.Message;
            if (lastAssistant == null)
            {
                throw new InvalidOperationException("LLM response has no choices.");
            }

            history.Add(lastAssistant);

            if (lastAssistant.ToolCalls == null || lastAssistant.ToolCalls.Count == 0)
            {
                return new LlmToolAgentResult(history, lastResponse, step + 1, true);
            }

            foreach (var call in lastAssistant.ToolCalls)
            {
                var toolResultJson = await ExecuteToolAsync(toolMap, call, cancellationToken);
                history.Add(new ChatMessage(ChatRole.Tool, toolResultJson, ToolCallId: call.Id));
            }

            // ToolChoice.Required / Specific(name) 很容易导致“永远必须调用工具”的循环：
            // 这里采用务实策略：满足一次“必须调用工具”的约束后，后续切换为 Auto 允许模型输出最终答案。
            if (initialChoice.Type == ToolChoiceType.Required)
            {
                currentChoice = ToolChoice.Auto;
            }
            else if (initialChoice.Type == ToolChoiceType.Specific &&
                     !string.IsNullOrWhiteSpace(initialChoice.Name) &&
                     lastAssistant.ToolCalls.Any(c => string.Equals(c.Name, initialChoice.Name, StringComparison.Ordinal)))
            {
                currentChoice = ToolChoice.Auto;
            }
        }

        return new LlmToolAgentResult(history, lastResponse ?? throw new InvalidOperationException("No LLM response."), maxSteps, false);
    }

    private static async Task<string> ExecuteToolAsync(
        IReadOnlyDictionary<string, LlmAgentTool> toolMap,
        ToolCall call,
        CancellationToken cancellationToken)
    {
        if (!toolMap.TryGetValue(call.Name, out var tool))
        {
            return JsonHelper.ObjectToJson(new JsonObject
            {
                ["error"] = new JsonObject
                {
                    ["code"] = "tool_not_found",
                    ["message"] = $"Tool not found: {call.Name}"
                }
            });
        }

        try
        {
            var json = await tool.InvokeAsync(call.ArgumentsJson, cancellationToken);
            if (string.IsNullOrWhiteSpace(json))
            {
                return JsonHelper.ObjectToJson(new JsonObject { ["result"] = new JsonObject() });
            }

            var parsed = JsonNode.Parse(json);
            if (parsed == null)
            {
                return JsonHelper.ObjectToJson(new JsonObject { ["result"] = new JsonObject() });
            }

            return JsonHelper.ObjectToJson(parsed);
        }
        catch (Exception ex)
        {
            return JsonHelper.ObjectToJson(new JsonObject
            {
                ["error"] = new JsonObject
                {
                    ["code"] = "tool_execution_error",
                    ["message"] = ex.Message
                }
            });
        }
    }

}

public sealed record LlmToolAgentResult(
    IReadOnlyList<ChatMessage> Messages,
    ChatResponse LastResponse,
    int Steps,
    bool IsCompleted
);
