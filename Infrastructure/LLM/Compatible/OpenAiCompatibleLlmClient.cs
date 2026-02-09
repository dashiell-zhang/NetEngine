using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LLM.Compatible;

public abstract class OpenAiCompatibleLlmClient<TSetting>(HttpClient httpClient, TSetting settings) : ILlmClient
    where TSetting : class, IOpenAiCompatibleSetting
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
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

        var model = request.Model;
        var payload = new ChatCompletionRequestDto
        {
            Model = model,
            Messages = request.Messages.Select(m => new ChatMessageDto
            {
                Role = RoleToString(m.Role),
                Content = m.Content
            }).ToList(),
            Temperature = request.Temperature,
            MaxTokens = request.MaxTokens,
            User = request.User,
            Stream = false
        };

        using var response = await httpClient.PostAsJsonAsync("chat/completions", payload, JsonOptions, cancellationToken);
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
        var payload = new ChatCompletionRequestDto
        {
            Model = model,
            Messages = request.Messages.Select(m => new ChatMessageDto
            {
                Role = RoleToString(m.Role),
                Content = m.Content
            }).ToList(),
            Temperature = request.Temperature,
            MaxTokens = request.MaxTokens,
            User = request.User,
            Stream = true
        };

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

    private static ChatResponse Map(ChatCompletionResponseDto dto)
    {
        var choices = dto.Choices?.Select(c => new ChatChoice(
            c.Index,
            new ChatMessage(RoleFromString(c.Message?.Role), c.Message?.Content ?? string.Empty),
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

    private sealed class ChatCompletionRequestDto
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("messages")]
        public List<ChatMessageDto> Messages { get; set; } = [];

        [JsonPropertyName("temperature")]
        public float? Temperature { get; set; }

        [JsonPropertyName("max_tokens")]
        public int? MaxTokens { get; set; }

        [JsonPropertyName("user")]
        public string? User { get; set; }

        [JsonPropertyName("stream")]
        public bool Stream { get; set; }
    }

    private sealed class ChatMessageDto
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
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
