using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace LLM.Anthropic;

/// <summary>
/// Anthropic Messages API 协议的通用 LLM 客户端
/// </summary>
public sealed class AnthropicMessagesLlmClient(HttpClient httpClient, LlmModelConfig settings) : ILlmClient
{

    private const string AnthropicVersion = "2023-06-01";

    private const int DefaultMaxTokens = 4096;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };


    /// <summary>
    /// 以非流式方式发起 Anthropic Messages 请求
    /// </summary>
    public async Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {

        ValidateSettings();
        ValidateRequest(request);

        var payload = BuildPayload(request, stream: false);

        using var httpRequest = CreateRequest(payload);
        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        response.EnsureSuccessStatusCode();

        var dto = await response.Content.ReadFromJsonAsync<AnthropicMessageResponseDto>(JsonOptions, cancellationToken);
        if (dto == null)
        {
            throw new InvalidOperationException("Anthropic response is empty.");
        }

        return Map(dto, request.Model);
    }


    /// <summary>
    /// 以流式方式发起 Anthropic Messages 请求
    /// </summary>
    public async IAsyncEnumerable<ChatStreamChunk> ChatStreamAsync(ChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {

        ValidateSettings();
        ValidateRequest(request);

        var payload = BuildPayload(request, stream: true);

        using var httpRequest = CreateRequest(payload);
        using var response = await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        string? messageId = null;
        string model = request.Model;
        int? inputTokens = null;

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

            AnthropicStreamEventDto? dto;
            try
            {
                dto = JsonSerializer.Deserialize<AnthropicStreamEventDto>(data, JsonOptions);
            }
            catch (JsonException)
            {
                continue;
            }

            if (dto == null)
            {
                continue;
            }

            if (string.Equals(dto.Type, "message_start", StringComparison.Ordinal) && dto.Message != null)
            {
                messageId = dto.Message.Id;
                model = dto.Message.Model ?? model;
                inputTokens = dto.Message.Usage?.InputTokens;
                continue;
            }

            var textDelta = dto.Delta?.Text;
            if (string.Equals(dto.Type, "content_block_delta", StringComparison.Ordinal) &&
                string.Equals(dto.Delta?.Type, "text_delta", StringComparison.Ordinal) &&
                !string.IsNullOrEmpty(textDelta))
            {
                yield return new ChatStreamChunk(
                    model,
                    [new ChatStreamChoiceDelta(0, new ChatStreamDelta(ChatRole.Assistant, textDelta), null)],
                    null,
                    messageId);
                continue;
            }

            if (string.Equals(dto.Type, "message_delta", StringComparison.Ordinal))
            {
                var outputTokens = dto.Usage?.OutputTokens;
                var usage = outputTokens == null && inputTokens == null
                    ? null
                    : new Usage(inputTokens, outputTokens, inputTokens + outputTokens);

                yield return new ChatStreamChunk(
                    model,
                    [new ChatStreamChoiceDelta(0, new ChatStreamDelta(null, null), dto.Delta?.StopReason)],
                    usage,
                    messageId);
                continue;
            }

            if (string.Equals(dto.Type, "message_stop", StringComparison.Ordinal))
            {
                yield break;
            }

            if (string.Equals(dto.Type, "error", StringComparison.Ordinal))
            {
                var message = dto.Error?.Message ?? "Anthropic stream returned an error.";
                throw new InvalidOperationException(message);
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
            throw new InvalidOperationException("Anthropic Endpoint is required.");
        }

        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            throw new InvalidOperationException("Anthropic ApiKey is required.");
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
    /// 创建 Anthropic HTTP 请求
    /// </summary>
    private HttpRequestMessage CreateRequest(JsonObject payload)
    {

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, settings.Endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json")
        };

        httpRequest.Headers.Add("x-api-key", settings.ApiKey);
        httpRequest.Headers.Add("anthropic-version", AnthropicVersion);

        return httpRequest;
    }


    /// <summary>
    /// 构造 Anthropic Messages 请求体
    /// </summary>
    private static JsonObject BuildPayload(ChatRequest request, bool stream)
    {

        var systemPrompt = BuildSystemPrompt(request.Messages);
        var messages = BuildMessages(request.Messages);

        var payload = new JsonObject
        {
            ["model"] = request.Model,
            ["messages"] = messages,
            ["stream"] = stream
        };

        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            payload["system"] = systemPrompt;
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

        if (!payload.ContainsKey("max_tokens"))
        {
            payload["max_tokens"] = DefaultMaxTokens;
        }

        return payload;
    }


    /// <summary>
    /// 构造 Anthropic 系统提示词
    /// </summary>
    private static string BuildSystemPrompt(IReadOnlyList<ChatMessage> messages)
    {

        var systemMessages = messages
            .Where(t => t.Role == ChatRole.System && !string.IsNullOrWhiteSpace(t.Content))
            .Select(t => t.Content)
            .ToList();

        return string.Join(Environment.NewLine + Environment.NewLine, systemMessages);
    }


    /// <summary>
    /// 构造 Anthropic 消息列表
    /// </summary>
    private static JsonArray BuildMessages(IReadOnlyList<ChatMessage> messages)
    {

        var jsonMessages = messages
            .Where(t => t.Role != ChatRole.System)
            .Select(t => new JsonObject
            {
                ["role"] = RoleToString(t.Role),
                ["content"] = t.Content
            })
            .ToArray();

        return jsonMessages.Length == 0
            ? [new JsonObject { ["role"] = "user", ["content"] = string.Empty }]
            : new JsonArray(jsonMessages);
    }


    /// <summary>
    /// 将 Anthropic 非流式响应映射为统一响应模型
    /// </summary>
    private static ChatResponse Map(AnthropicMessageResponseDto dto, string fallbackModel)
    {

        var content = ExtractText(dto);
        var usage = dto.Usage == null
            ? null
            : new Usage(dto.Usage.InputTokens, dto.Usage.OutputTokens, dto.Usage.InputTokens + dto.Usage.OutputTokens);

        return new ChatResponse(
            dto.Model ?? fallbackModel,
            [new ChatChoice(0, new ChatMessage(ChatRole.Assistant, content), dto.StopReason)],
            usage,
            dto.Id);
    }


    /// <summary>
    /// 提取 Anthropic 输出文本
    /// </summary>
    private static string ExtractText(AnthropicMessageResponseDto dto)
    {

        StringBuilder builder = new();
        foreach (var content in dto.Content ?? [])
        {
            if (string.Equals(content.Type, "text", StringComparison.Ordinal) && !string.IsNullOrEmpty(content.Text))
            {
                builder.Append(content.Text);
            }
        }

        return builder.ToString();
    }


    /// <summary>
    /// 将内部角色转换为 Anthropic 角色
    /// </summary>
    private static string RoleToString(ChatRole role) => role switch
    {
        ChatRole.Assistant => "assistant",
        _ => "user"
    };


    /// <summary>
    /// Anthropic 非流式响应数据结构
    /// </summary>
    private sealed class AnthropicMessageResponseDto
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
        /// 停止原因
        /// </summary>
        [JsonPropertyName("stop_reason")]
        public string? StopReason { get; set; }

        /// <summary>
        /// 输出内容集合
        /// </summary>
        [JsonPropertyName("content")]
        public List<AnthropicContentDto>? Content { get; set; }

        /// <summary>
        /// 用量统计
        /// </summary>
        [JsonPropertyName("usage")]
        public AnthropicUsageDto? Usage { get; set; }
    }


    /// <summary>
    /// Anthropic 输出内容数据结构
    /// </summary>
    private sealed class AnthropicContentDto
    {
        /// <summary>
        /// 内容类型
        /// </summary>
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        /// <summary>
        /// 文本内容
        /// </summary>
        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }


    /// <summary>
    /// Anthropic 流式事件数据结构
    /// </summary>
    private sealed class AnthropicStreamEventDto
    {
        /// <summary>
        /// 事件类型
        /// </summary>
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        /// <summary>
        /// 消息开始事件的消息数据
        /// </summary>
        [JsonPropertyName("message")]
        public AnthropicMessageResponseDto? Message { get; set; }

        /// <summary>
        /// 内容或消息增量
        /// </summary>
        [JsonPropertyName("delta")]
        public AnthropicDeltaDto? Delta { get; set; }

        /// <summary>
        /// 用量统计
        /// </summary>
        [JsonPropertyName("usage")]
        public AnthropicUsageDto? Usage { get; set; }

        /// <summary>
        /// 错误详情
        /// </summary>
        [JsonPropertyName("error")]
        public AnthropicErrorDto? Error { get; set; }
    }


    /// <summary>
    /// Anthropic 流式增量数据结构
    /// </summary>
    private sealed class AnthropicDeltaDto
    {
        /// <summary>
        /// 增量类型
        /// </summary>
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        /// <summary>
        /// 文本增量
        /// </summary>
        [JsonPropertyName("text")]
        public string? Text { get; set; }

        /// <summary>
        /// 停止原因
        /// </summary>
        [JsonPropertyName("stop_reason")]
        public string? StopReason { get; set; }
    }


    /// <summary>
    /// Anthropic 用量统计数据结构
    /// </summary>
    private sealed class AnthropicUsageDto
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
    }


    /// <summary>
    /// Anthropic 错误详情数据结构
    /// </summary>
    private sealed class AnthropicErrorDto
    {
        /// <summary>
        /// 错误消息
        /// </summary>
        [JsonPropertyName("message")]
        public string? Message { get; set; }
    }

}
