using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace LLM.Compatible;

public abstract class OpenAiCompatibleLlmClient<TSetting>(HttpClient httpClient, TSetting settings) : ILlmClient
    where TSetting : class, IOpenAiCompatibleSetting
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    protected abstract string ProviderName { get; }

    protected virtual void ValidateSettings(TSetting settings)
    {
        if (string.IsNullOrWhiteSpace(settings.BaseUrl))
        {
            throw new InvalidOperationException($"{ProviderName} BaseUrl is required.");
        }

        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            throw new InvalidOperationException($"{ProviderName} ApiKey is required.");
        }
    }

    public async Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        ValidateSettings(settings);

        if (string.IsNullOrWhiteSpace(request.Model))
        {
            throw new InvalidOperationException("LLM model is required (request.Model).");
        }

        var payload = BuildPayload(request, stream: false);

        using var response = await httpClient.PostAsync(
            "chat/completions",
            new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json"),
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var dto = await response.Content.ReadFromJsonAsync<ChatCompletionResponseDto>(JsonOptions, cancellationToken);
        if (dto == null)
        {
            throw new InvalidOperationException($"{ProviderName} response is empty.");
        }

        return Map(dto);
    }

    public async IAsyncEnumerable<ChatStreamChunk> ChatStreamAsync(
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ValidateSettings(settings);

        if (string.IsNullOrWhiteSpace(request.Model))
        {
            throw new InvalidOperationException("LLM model is required (request.Model).");
        }

        var model = request.Model;
        var payload = BuildPayload(request, stream: true);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json")
        };

        using var response = await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line == null)
            {
                break;
            }

            if (line.Length == 0)
            {
                continue;
            }

            if (!line.StartsWith("data:", StringComparison.Ordinal))
            {
                continue;
            }

            var data = line["data:".Length..].Trim();
            if (data.Length == 0)
            {
                continue;
            }

            if (string.Equals(data, "[DONE]", StringComparison.OrdinalIgnoreCase))
            {
                yield break;
            }

            ChatCompletionStreamResponseDto? dto;
            try
            {
                dto = JsonSerializer.Deserialize<ChatCompletionStreamResponseDto>(data, JsonOptions);
            }
            catch (JsonException)
            {
                continue;
            }

            if (dto == null)
            {
                continue;
            }

            yield return Map(dto, model);
        }
    }

    private static JsonObject BuildPayload(ChatRequest request, bool stream)
    {
        var payload = new JsonObject
        {
            ["model"] = request.Model,
            ["messages"] = new JsonArray(
                request.Messages.Select(m =>
                {
                    var msg = new JsonObject
                    {
                        ["role"] = RoleToString(m.Role),
                        ["content"] = m.Role == ChatRole.Tool ? (m.Content ?? string.Empty) : m.Content
                    };

                    if (!string.IsNullOrWhiteSpace(m.Name))
                    {
                        msg["name"] = m.Name;
                    }

                    if (m.Role == ChatRole.Tool && !string.IsNullOrWhiteSpace(m.ToolCallId))
                    {
                        msg["tool_call_id"] = m.ToolCallId;
                    }

                    if (m.ToolCalls != null && m.ToolCalls.Count != 0)
                    {
                        msg["tool_calls"] = new JsonArray(
                            m.ToolCalls.Select(c =>
                            {
                                var call = new JsonObject
                                {
                                    ["id"] = c.Id,
                                    ["type"] = "function",
                                    ["function"] = new JsonObject
                                    {
                                        ["name"] = c.Name,
                                        ["arguments"] = c.ArgumentsJson
                                    }
                                };
                                return call;
                            }).ToArray());
                    }
                    return msg;
                }).ToArray()),
            ["stream"] = stream
        };

        if (request.Temperature != null)
        {
            payload["temperature"] = request.Temperature.Value;
        }

        if (request.MaxTokens != null)
        {
            payload["max_tokens"] = request.MaxTokens.Value;
        }

        if (!string.IsNullOrWhiteSpace(request.User))
        {
            payload["user"] = request.User;
        }

        if (request.Tools != null && request.Tools.Count != 0)
        {
            payload["tools"] = new JsonArray(
                request.Tools.Select(t =>
                {
                    if (string.IsNullOrWhiteSpace(t.Name))
                    {
                        throw new InvalidOperationException("Tool name is required.");
                    }

                    var fn = new JsonObject
                    {
                        ["name"] = t.Name
                    };

                    if (!string.IsNullOrWhiteSpace(t.Description))
                    {
                        fn["description"] = t.Description;
                    }

                    fn["parameters"] = t.ParametersSchema.DeepClone();

                    return new JsonObject
                    {
                        ["type"] = "function",
                        ["function"] = fn
                    };
                }).ToArray());
        }

        if (request.ToolChoice != null)
        {
            if (request.ToolChoice.Type == ToolChoiceType.Specific && string.IsNullOrWhiteSpace(request.ToolChoice.Name))
            {
                throw new InvalidOperationException("ToolChoice.Specific requires a tool name.");
            }

            payload["tool_choice"] = request.ToolChoice.Type switch
            {
                ToolChoiceType.Auto => "auto",
                ToolChoiceType.None => "none",
                ToolChoiceType.Required => "required",
                ToolChoiceType.Specific => new JsonObject
                {
                    ["type"] = "function",
                    ["function"] = new JsonObject { ["name"] = request.ToolChoice.Name }
                },
                _ => "auto"
            };
        }

        if (request.ExtraBody != null && request.ExtraBody.Count != 0)
        {
            foreach (var kv in request.ExtraBody)
            {
                if (string.IsNullOrWhiteSpace(kv.Key))
                {
                    continue;
                }

                if (payload.ContainsKey(kv.Key))
                {
                    throw new InvalidOperationException($"ExtraBody conflicts with base payload field: {kv.Key}");
                }

                // JsonNode 不能同时属于多个父节点：这里必须 Clone，否则会抛 "The node already has a parent."
                payload[kv.Key] = kv.Value?.DeepClone();
            }
        }

        return payload;
    }

    private static ChatResponse Map(ChatCompletionResponseDto dto)
    {
        var choices = dto.Choices?.Select(c => new ChatChoice(
            c.Index,
            new ChatMessage(
                RoleFromString(c.Message?.Role),
                c.Message?.Content,
                MapToolCalls(c.Message, c.Index)),
            c.FinishReason
        )).ToList() ?? [];

        var usage = dto.Usage == null
            ? null
            : new Usage(dto.Usage.PromptTokens, dto.Usage.CompletionTokens, dto.Usage.TotalTokens);

        return new ChatResponse(
            dto.Model ?? string.Empty,
            choices,
            usage,
            dto.Id
        );
    }

    private static IReadOnlyList<ToolCall>? MapToolCalls(ChatMessageDto? message, int choiceIndex)
    {
        if (message == null)
        {
            return null;
        }

        if (message.ToolCalls != null && message.ToolCalls.Count != 0)
        {
            var toolCalls = message.ToolCalls
                .Select((c, i) =>
                {
                    var id = string.IsNullOrWhiteSpace(c.Id) ? $"call_{choiceIndex}_{i}" : c.Id;
                    var name = c.Function?.Name ?? string.Empty;
                    var args = c.Function?.Arguments ?? "{}";
                    return new ToolCall(id, name, args);
                })
                .Where(c => !string.IsNullOrWhiteSpace(c.Name))
                .ToList();

            return toolCalls.Count == 0 ? null : toolCalls;
        }

        if (message.FunctionCall != null && !string.IsNullOrWhiteSpace(message.FunctionCall.Name))
        {
            var args = message.FunctionCall.Arguments ?? "{}";
            return [new ToolCall($"call_{choiceIndex}_0", message.FunctionCall.Name, args)];
        }

        return null;
    }

    private static ChatStreamChunk Map(ChatCompletionStreamResponseDto dto, string fallbackModel)
    {
        var choices = dto.Choices?.Select(c => new ChatStreamChoiceDelta(
            c.Index,
            new ChatStreamDelta(RoleFromStringNullable(c.Delta?.Role), c.Delta?.Content),
            c.FinishReason
        )).ToList() ?? [];

        var usage = dto.Usage == null
            ? null
            : new Usage(dto.Usage.PromptTokens, dto.Usage.CompletionTokens, dto.Usage.TotalTokens);

        return new ChatStreamChunk(
            dto.Model ?? fallbackModel,
            choices,
            usage,
            dto.Id
        );
    }

    private static string RoleToString(ChatRole role) => role switch
    {
        ChatRole.System => "system",
        ChatRole.User => "user",
        ChatRole.Assistant => "assistant",
        ChatRole.Tool => "tool",
        _ => "user"
    };

    private static ChatRole RoleFromString(string? role) => role?.ToLowerInvariant() switch
    {
        "system" => ChatRole.System,
        "user" => ChatRole.User,
        "assistant" => ChatRole.Assistant,
        "tool" => ChatRole.Tool,
        _ => ChatRole.Assistant
    };

    private static ChatRole? RoleFromStringNullable(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return null;
        }

        return RoleFromString(role);
    }

    private sealed class ChatMessageDto
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string? Content { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("tool_calls")]
        public List<ToolCallDto>? ToolCalls { get; set; }

        [JsonPropertyName("function_call")]
        public FunctionCallDto? FunctionCall { get; set; }
    }

    private sealed class ToolCallDto
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("function")]
        public ToolFunctionDto? Function { get; set; }
    }

    private sealed class ToolFunctionDto
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("arguments")]
        public string? Arguments { get; set; }
    }

    private sealed class FunctionCallDto
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("arguments")]
        public string? Arguments { get; set; }
    }

    private sealed class ChatCompletionResponseDto
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("model")]
        public string? Model { get; set; }

        [JsonPropertyName("choices")]
        public List<ChatChoiceDto>? Choices { get; set; }

        [JsonPropertyName("usage")]
        public UsageDto? Usage { get; set; }
    }

    private sealed class ChatChoiceDto
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("message")]
        public ChatMessageDto? Message { get; set; }

        [JsonPropertyName("finish_reason")]
        public string? FinishReason { get; set; }
    }

    private sealed class ChatCompletionStreamResponseDto
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("model")]
        public string? Model { get; set; }

        [JsonPropertyName("choices")]
        public List<ChatStreamChoiceDto>? Choices { get; set; }

        [JsonPropertyName("usage")]
        public UsageDto? Usage { get; set; }
    }

    private sealed class ChatStreamChoiceDto
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("delta")]
        public ChatStreamDeltaDto? Delta { get; set; }

        [JsonPropertyName("finish_reason")]
        public string? FinishReason { get; set; }
    }

    private sealed class ChatStreamDeltaDto
    {
        [JsonPropertyName("role")]
        public string? Role { get; set; }

        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }

    private sealed class UsageDto
    {
        [JsonPropertyName("prompt_tokens")]
        public int? PromptTokens { get; set; }

        [JsonPropertyName("completion_tokens")]
        public int? CompletionTokens { get; set; }

        [JsonPropertyName("total_tokens")]
        public int? TotalTokens { get; set; }
    }
}
