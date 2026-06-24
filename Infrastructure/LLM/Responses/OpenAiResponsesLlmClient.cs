using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace LLM.Responses;

/// <summary>
/// OpenAI Responses API 协议的通用 LLM 客户端
/// </summary>
public sealed class OpenAiResponsesLlmClient(HttpClient httpClient, LlmModelConfig settings) : ILlmClient
{

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };


    /// <summary>
    /// 以非流式方式发起 Responses API 请求
    /// </summary>
    public async Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {

        ValidateSettings();
        ValidateRequest(request);

        var payload = BuildPayload(request, stream: false);

        using var response = await httpClient.PostAsync(
            settings.Endpoint,
            new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json"),
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var dto = await response.Content.ReadFromJsonAsync<ResponseDto>(JsonOptions, cancellationToken);
        if (dto == null)
        {
            throw new InvalidOperationException("OpenAI Responses response is empty.");
        }

        return Map(dto, request.Model);
    }


    /// <summary>
    /// 以流式方式发起 Responses API 请求
    /// </summary>
    public async IAsyncEnumerable<ChatStreamChunk> ChatStreamAsync(ChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {

        ValidateSettings();
        ValidateRequest(request);

        var payload = BuildPayload(request, stream: true);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, settings.Endpoint)
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

            if (line.Length == 0 || !line.StartsWith("data:", StringComparison.Ordinal))
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

            ResponseStreamEventDto? dto;
            try
            {
                dto = JsonSerializer.Deserialize<ResponseStreamEventDto>(data, JsonOptions);
            }
            catch (JsonException)
            {
                continue;
            }

            if (dto == null)
            {
                continue;
            }

            var chunk = MapStreamEvent(dto, request.Model);
            if (chunk != null)
            {
                yield return chunk;
            }
        }
    }


    /// <summary>
    /// 校验模型配置
    /// </summary>
    private void ValidateSettings()
    {

        if (string.IsNullOrWhiteSpace(settings.Endpoint))
        {
            throw new InvalidOperationException("OpenAI Responses Endpoint is required.");
        }

        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            throw new InvalidOperationException("OpenAI Responses ApiKey is required.");
        }
    }


    /// <summary>
    /// 校验调用请求
    /// </summary>
    private static void ValidateRequest(ChatRequest request)
    {

        if (string.IsNullOrWhiteSpace(request.Model))
        {
            throw new InvalidOperationException("LLM model is required (request.Model).");
        }

        if (request.Messages.Count == 0)
        {
            throw new InvalidOperationException("LLM messages are required (request.Messages).");
        }
    }


    /// <summary>
    /// 构造 Responses API 请求体
    /// </summary>
    private static JsonObject BuildPayload(ChatRequest request, bool stream)
    {

        var payload = new JsonObject
        {
            ["model"] = request.Model,
            ["input"] = new JsonArray(
                request.Messages.Select(m =>
                {
                    var message = new JsonObject
                    {
                        ["role"] = RoleToString(m.Role),
                        ["content"] = m.Content
                    };

                    return message;
                }).ToArray()),
            ["stream"] = stream
        };

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

                payload[kv.Key] = kv.Value?.DeepClone();
            }
        }

        return payload;
    }


    /// <summary>
    /// 将 Responses 非流式响应映射为统一响应模型
    /// </summary>
    private static ChatResponse Map(ResponseDto dto, string fallbackModel)
    {

        var content = ExtractOutputText(dto);
        var choices = new List<ChatChoice>
        {
            new(0, new ChatMessage(ChatRole.Assistant, content), dto.Status)
        };

        var usage = dto.Usage == null
            ? null
            : new Usage(dto.Usage.InputTokens, dto.Usage.OutputTokens, dto.Usage.TotalTokens);

        return new ChatResponse(
            dto.Model ?? fallbackModel,
            choices,
            usage,
            dto.Id
        );
    }


    /// <summary>
    /// 将 Responses 流式事件映射为统一流式分片
    /// </summary>
    private static ChatStreamChunk? MapStreamEvent(ResponseStreamEventDto dto, string fallbackModel)
    {

        if (string.Equals(dto.Type, "response.output_text.delta", StringComparison.Ordinal))
        {
            return new ChatStreamChunk(
                fallbackModel,
                [new ChatStreamChoiceDelta(0, new ChatStreamDelta(ChatRole.Assistant, dto.Delta), null)],
                null,
                dto.ResponseId);
        }

        if (string.Equals(dto.Type, "response.completed", StringComparison.Ordinal) && dto.Response != null)
        {
            var usage = dto.Response.Usage == null
                ? null
                : new Usage(dto.Response.Usage.InputTokens, dto.Response.Usage.OutputTokens, dto.Response.Usage.TotalTokens);

            return new ChatStreamChunk(
                dto.Response.Model ?? fallbackModel,
                [new ChatStreamChoiceDelta(0, new ChatStreamDelta(null, null), dto.Response.Status)],
                usage,
                dto.Response.Id);
        }

        if (string.Equals(dto.Type, "response.failed", StringComparison.Ordinal) && dto.Response != null)
        {
            var message = dto.Response.Error?.Message ?? "OpenAI Responses response failed.";
            throw new InvalidOperationException(message);
        }

        if (string.Equals(dto.Type, "error", StringComparison.Ordinal))
        {
            var message = dto.Message ?? dto.Error?.Message ?? "OpenAI Responses stream returned an error.";
            throw new InvalidOperationException(message);
        }

        return null;
    }


    /// <summary>
    /// 提取 Responses 输出文本
    /// </summary>
    private static string ExtractOutputText(ResponseDto dto)
    {

        if (!string.IsNullOrEmpty(dto.OutputText))
        {
            return dto.OutputText;
        }

        StringBuilder builder = new();
        foreach (var item in dto.Output ?? [])
        {
            if (item.Content == null)
            {
                continue;
            }

            foreach (var content in item.Content)
            {
                if (!string.Equals(content.Type, "output_text", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(content.Text))
                {
                    builder.Append(content.Text);
                }
            }
        }

        return builder.ToString();
    }


    /// <summary>
    /// 将内部角色转换为 Responses API 角色
    /// </summary>
    private static string RoleToString(ChatRole role) => role switch
    {
        ChatRole.System => "system",
        ChatRole.User => "user",
        ChatRole.Assistant => "assistant",
        ChatRole.Tool => "assistant",
        _ => "user"
    };


    /// <summary>
    /// Responses API 非流式响应数据结构
    /// </summary>
    private sealed class ResponseDto
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
        /// 响应状态
        /// </summary>
        [JsonPropertyName("status")]
        public string? Status { get; set; }

        /// <summary>
        /// SDK 友好的输出文本字段
        /// </summary>
        [JsonPropertyName("output_text")]
        public string? OutputText { get; set; }

        /// <summary>
        /// 输出项集合
        /// </summary>
        [JsonPropertyName("output")]
        public List<ResponseOutputItemDto>? Output { get; set; }

        /// <summary>
        /// 用量统计
        /// </summary>
        [JsonPropertyName("usage")]
        public ResponseUsageDto? Usage { get; set; }

        /// <summary>
        /// 错误详情
        /// </summary>
        [JsonPropertyName("error")]
        public ResponseErrorDto? Error { get; set; }
    }


    /// <summary>
    /// Responses API 输出项数据结构
    /// </summary>
    private sealed class ResponseOutputItemDto
    {
        /// <summary>
        /// 输出项类型
        /// </summary>
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        /// <summary>
        /// 输出角色
        /// </summary>
        [JsonPropertyName("role")]
        public string? Role { get; set; }

        /// <summary>
        /// 输出内容集合
        /// </summary>
        [JsonPropertyName("content")]
        public List<ResponseOutputContentDto>? Content { get; set; }
    }


    /// <summary>
    /// Responses API 输出内容数据结构
    /// </summary>
    private sealed class ResponseOutputContentDto
    {
        /// <summary>
        /// 输出内容类型
        /// </summary>
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        /// <summary>
        /// 输出文本
        /// </summary>
        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }


    /// <summary>
    /// Responses API 流式事件数据结构
    /// </summary>
    private sealed class ResponseStreamEventDto
    {
        /// <summary>
        /// 事件类型
        /// </summary>
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        /// <summary>
        /// 响应标识
        /// </summary>
        [JsonPropertyName("response_id")]
        public string? ResponseId { get; set; }

        /// <summary>
        /// 文本增量
        /// </summary>
        [JsonPropertyName("delta")]
        public string? Delta { get; set; }

        /// <summary>
        /// 完整响应
        /// </summary>
        [JsonPropertyName("response")]
        public ResponseDto? Response { get; set; }

        /// <summary>
        /// 错误消息
        /// </summary>
        [JsonPropertyName("message")]
        public string? Message { get; set; }

        /// <summary>
        /// 错误详情
        /// </summary>
        [JsonPropertyName("error")]
        public ResponseErrorDto? Error { get; set; }
    }


    /// <summary>
    /// Responses API 错误详情数据结构
    /// </summary>
    private sealed class ResponseErrorDto
    {
        /// <summary>
        /// 错误消息
        /// </summary>
        [JsonPropertyName("message")]
        public string? Message { get; set; }
    }


    /// <summary>
    /// Responses API 用量统计数据结构
    /// </summary>
    private sealed class ResponseUsageDto
    {
        /// <summary>
        /// 输入 token 数
        /// </summary>
        [JsonPropertyName("input_tokens")]
        public int? InputTokens { get; set; }

        /// <summary>
        /// 输出 token 数
        /// </summary>
        [JsonPropertyName("output_tokens")]
        public int? OutputTokens { get; set; }

        /// <summary>
        /// 总 token 数
        /// </summary>
        [JsonPropertyName("total_tokens")]
        public int? TotalTokens { get; set; }
    }

}
