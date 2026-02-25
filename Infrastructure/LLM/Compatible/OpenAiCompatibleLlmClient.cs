using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace LLM.Compatible;

/// <summary>
/// OpenAI-Compatible 协议的通用 LLM 客户端基类
/// </summary>
public abstract class OpenAiCompatibleLlmClient<TSetting>(HttpClient httpClient, TSetting settings) : ILlmClient
    where TSetting : class, IOpenAiCompatibleSetting
{

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };


    /// <summary>
    /// Provider 名称 用于错误提示与诊断
    /// </summary>
    protected abstract string ProviderName { get; }


    /// <summary>
    /// 校验 Provider 配置
    /// </summary>
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


    /// <summary>
    /// 以非流式方式发起对话请求
    /// </summary>
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


    /// <summary>
    /// 以流式方式发起对话请求 通过 SSE data 行增量返回
    /// </summary>
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

            // OpenAI 兼容的流式响应按行返回 且以 data: 前缀承载 JSON 片段
            if (!line.StartsWith("data:", StringComparison.Ordinal))
            {
                continue;
            }

            var data = line["data:".Length..].Trim();
            if (data.Length == 0)
            {
                continue;
            }

            // [DONE] 表示服务端结束本次流式响应
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
                // 非 JSON 片段或不完整片段直接跳过
                continue;
            }

            if (dto == null)
            {
                continue;
            }

            yield return Map(dto, model);
        }
    }


    /// <summary>
    /// 构造 OpenAI-Compatible 的请求体
    /// </summary>
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
                        ["content"] = m.Content
                    };
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

                // JsonNode 不能同时属于多个父节点 这里必须 Clone 否则会抛 The node already has a parent
                payload[kv.Key] = kv.Value?.DeepClone();
            }
        }

        return payload;
    }


    /// <summary>
    /// 将非流式响应 DTO 映射为统一的对话响应模型
    /// </summary>
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


    /// <summary>
    /// 将流式响应 DTO 映射为统一的流式分片模型
    /// </summary>
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


    /// <summary>
    /// 将角色枚举转换为 OpenAI 兼容协议的字符串值
    /// </summary>
    private static string RoleToString(ChatRole role) => role switch
    {
        ChatRole.System => "system",
        ChatRole.User => "user",
        ChatRole.Assistant => "assistant",
        ChatRole.Tool => "tool",
        _ => "user"
    };


    /// <summary>
    /// 将 OpenAI 兼容协议的字符串角色解析为角色枚举
    /// </summary>
    private static ChatRole RoleFromString(string? role) => role?.ToLowerInvariant() switch
    {
        "system" => ChatRole.System,
        "user" => ChatRole.User,
        "assistant" => ChatRole.Assistant,
        "tool" => ChatRole.Tool,
        _ => ChatRole.Assistant
    };


    /// <summary>
    /// 解析可空字符串角色 为空时返回 null
    /// </summary>
    private static ChatRole? RoleFromStringNullable(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return null;
        }

        return RoleFromString(role);
    }


    /// <summary>
    /// OpenAI 兼容响应中单条消息对象的数据结构
    /// </summary>
    private sealed class ChatMessageDto
    {
        /// <summary>
        /// 角色字段 例如 system user assistant tool
        /// </summary>
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        /// <summary>
        /// 文本内容
        /// </summary>
        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }


    /// <summary>
    /// OpenAI 兼容非流式响应数据结构
    /// </summary>
    private sealed class ChatCompletionResponseDto
    {
        /// <summary>
        /// 响应标识
        /// </summary>
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        /// <summary>
        /// 实际使用的模型名称
        /// </summary>
        [JsonPropertyName("model")]
        public string? Model { get; set; }

        /// <summary>
        /// 候选列表
        /// </summary>
        [JsonPropertyName("choices")]
        public List<ChatChoiceDto>? Choices { get; set; }

        /// <summary>
        /// 用量统计
        /// </summary>
        [JsonPropertyName("usage")]
        public UsageDto? Usage { get; set; }
    }


    /// <summary>
    /// OpenAI 兼容非流式响应的单个候选结构
    /// </summary>
    private sealed class ChatChoiceDto
    {
        /// <summary>
        /// 候选序号
        /// </summary>
        [JsonPropertyName("index")]
        public int Index { get; set; }

        /// <summary>
        /// 候选消息
        /// </summary>
        [JsonPropertyName("message")]
        public ChatMessageDto? Message { get; set; }

        /// <summary>
        /// 结束原因
        /// </summary>
        [JsonPropertyName("finish_reason")]
        public string? FinishReason { get; set; }
    }


    /// <summary>
    /// OpenAI 兼容流式响应数据结构
    /// </summary>
    private sealed class ChatCompletionStreamResponseDto
    {
        /// <summary>
        /// 响应标识
        /// </summary>
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        /// <summary>
        /// 实际使用的模型名称
        /// </summary>
        [JsonPropertyName("model")]
        public string? Model { get; set; }

        /// <summary>
        /// 增量候选列表
        /// </summary>
        [JsonPropertyName("choices")]
        public List<ChatStreamChoiceDto>? Choices { get; set; }

        /// <summary>
        /// 用量统计 部分供应商会在流末尾提供
        /// </summary>
        [JsonPropertyName("usage")]
        public UsageDto? Usage { get; set; }
    }


    /// <summary>
    /// OpenAI 兼容流式响应的单个候选增量结构
    /// </summary>
    private sealed class ChatStreamChoiceDto
    {
        /// <summary>
        /// 候选序号
        /// </summary>
        [JsonPropertyName("index")]
        public int Index { get; set; }

        /// <summary>
        /// 增量内容
        /// </summary>
        [JsonPropertyName("delta")]
        public ChatStreamDeltaDto? Delta { get; set; }

        /// <summary>
        /// 结束原因
        /// </summary>
        [JsonPropertyName("finish_reason")]
        public string? FinishReason { get; set; }
    }


    /// <summary>
    /// OpenAI 兼容流式响应的 delta 对象结构
    /// </summary>
    private sealed class ChatStreamDeltaDto
    {
        /// <summary>
        /// 角色字段 部分实现仅在首个 chunk 提供
        /// </summary>
        [JsonPropertyName("role")]
        public string? Role { get; set; }

        /// <summary>
        /// 文本内容增量
        /// </summary>
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }


    /// <summary>
    /// OpenAI 兼容响应中的用量对象结构
    /// </summary>
    private sealed class UsageDto
    {
        /// <summary>
        /// 提示词 token 数
        /// </summary>
        [JsonPropertyName("prompt_tokens")]
        public int? PromptTokens { get; set; }

        /// <summary>
        /// 生成 token 数
        /// </summary>
        [JsonPropertyName("completion_tokens")]
        public int? CompletionTokens { get; set; }

        /// <summary>
        /// 总 token 数
        /// </summary>
        [JsonPropertyName("total_tokens")]
        public int? TotalTokens { get; set; }
    }

}
