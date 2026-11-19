using System.Net.Http.Headers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace LLM.SystemOne;

/// <summary>
/// 使用 System One 协议执行分类、是否判断和评分
/// </summary>
public sealed class SystemOneDecisionClient(HttpClient httpClient, LlmModelConfig config) : ILlmDecisionClient
{

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    private static readonly JsonSerializerOptions ResponseOptions = new(JsonSerializerDefaults.Web);


    /// <summary>
    /// 从模板节点构造已校验的请求，模型标识由客户端发送时填入
    /// </summary>
    public static DecisionRequest CreateRequest(JsonObject template)
    {

        var state = template["state"];
        if (state is not (JsonObject or JsonArray) && !IsDescription(state))
        {
            throw new InvalidOperationException("state 必须是对象、数组或非空文本");
        }
        if (template["questions"] is not JsonObject questionNodes)
        {
            throw new InvalidOperationException("questions 必须是问题对象");
        }

        try
        {
            var questions = questionNodes.Deserialize<Dictionary<string, DecisionQuestion>>(JsonOptions)!;
            ValidateQuestions(questions);
            var extras = template.Where(field => field.Key is not ("model" or "state" or "questions"))
                .ToDictionary(field => field.Key, field => field.Value?.DeepClone());
            ValidateExtraBody(extras.Keys);
            return new DecisionRequest(state!.DeepClone(), questions, extras);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("决策问题 JSON 无效，请检查类型、说明和选项格式", exception);
        }

    }


    /// <summary>
    /// 校验问题说明、类型及其对应的选项结构
    /// </summary>
    private static void ValidateQuestions(IReadOnlyDictionary<string, DecisionQuestion> questions)
    {

        if (questions.Count == 0)
        {
            throw new InvalidOperationException("请至少配置一个决策问题");
        }

        foreach (var (id, question) in questions)
        {
            if (string.IsNullOrWhiteSpace(id) || question == null)
            {
                throw new InvalidOperationException("决策问题标识及定义不能为空");
            }
            if (question.Instructions is not (JsonObject or JsonArray) && !IsDescription(question.Instructions))
            {
                throw new InvalidOperationException($"问题 {id} 的 instructions 必须是非空文字、对象或数组");
            }

            switch (question.Type)
            {
                case "choice":
                    if (question.Criteria is not JsonObject options || options.Count == 0)
                    {
                        throw new InvalidOperationException($"问题 {id} 的分类选项必须是非空对象，选项名和说明不能为空");
                    }
                    var optionNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var (name, description) in options)
                    {
                        if (!optionNames.Add(name))
                        {
                            optionNames.TryGetValue(name, out var original);
                            throw new InvalidOperationException($"问题 {id} 的分类选项“{name}”与“{original}”重复，选项名不区分大小写");
                        }
                        if (string.IsNullOrWhiteSpace(name) || !IsDescription(description))
                        {
                            throw new InvalidOperationException($"问题 {id} 的分类选项名和说明不能为空");
                        }
                    }
                    break;
                case "noul":
                    if (question.Criteria != null && (question.Criteria is not JsonObject criteria || criteria.Count == 0 || criteria.Any(option => (option.Key != "true" && option.Key != "false") || !IsDescription(option.Value))))
                    {
                        throw new InvalidOperationException($"问题 {id} 的是否判断标准只能包含 true、false 的文字说明");
                    }
                    break;
                case "score":
                    if (question.Criteria is not JsonArray levels || levels.Count == 0 || levels.Any(level => !IsDescription(level)))
                    {
                        throw new InvalidOperationException($"问题 {id} 的评分标准必须是非空有序数组，每个等级都需要文字说明");
                    }
                    break;
                default:
                    throw new InvalidOperationException($"问题 {id} 的类型仅支持 choice、noul、score");
            }
        }

    }


    /// <summary>
    /// 校验扩展字段不会覆盖协议基础字段或启用流式输出
    /// </summary>
    private static void ValidateExtraBody(IEnumerable<string> keys)
    {

        if (keys.Any(key => new[] { "model", "state", "questions", "stream" }.Contains(key, StringComparer.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("决策扩展参数不能包含 model、state、questions 或 stream");
        }

    }


    /// <summary>
    /// 发送非流式决策请求并校验返回结果与问题定义一致
    /// </summary>
    public async Task<DecisionResponse> DecideAsync(DecisionRequest request, CancellationToken cancellationToken = default)
    {

        ArgumentNullException.ThrowIfNull(request);
        var payload = new JsonObject
        {
            ["model"] = config.ModelId,
            ["state"] = request.State.DeepClone(),
            ["questions"] = JsonSerializer.SerializeToNode(request.Questions, JsonOptions)
        };
        foreach (var (key, value) in request.ExtraBody)
        {
            payload.Add(key, value?.DeepClone());
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, config.Endpoint);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);
        var requestJson = payload.ToJsonString(JsonOptions);
        message.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");
        using var response = await httpClient.SendAsync(message, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw CreateProviderException(response, content);
        }
        var result = JsonSerializer.Deserialize<DecisionResponse>(content, ResponseOptions)
            ?? throw new InvalidOperationException("决策模型返回了空响应");
        ValidateResponse(result, request.Questions);
        result.RequestJson = requestJson;
        result.RawContent = content;
        return result;

    }


    /// <summary>
    /// 从供应商错误响应中提取状态、错误说明及请求标识
    /// </summary>
    private HttpRequestException CreateProviderException(HttpResponseMessage response, string content)
    {

        string? code = null;
        string? message = null;
        string? requestId = null;
        try
        {
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Object)
            {
                root.TryGetProperty("error", out var error);
                code = ReadErrorField(error, "code") ?? ReadErrorField(root, "code");
                message = ReadErrorField(error, "message") ?? ReadErrorField(root, "message") ?? ReadErrorField(root, "detail") ?? ReadErrorField(root, "error");
                requestId = ReadErrorField(root, "request_id") ?? ReadErrorField(error, "request_id");
                if (message == null && root.TryGetProperty("detail", out var details) && details.ValueKind == JsonValueKind.Array && details.GetArrayLength() > 0)
                {
                    message = ReadErrorField(details[0], "msg");
                }
            }
        }
        catch (JsonException)
        {
            // 空正文或非 JSON 错误页使用 HTTP 状态及响应头中的请求标识
        }

        if (requestId == null && (response.Headers.TryGetValues("x-request-id", out var ids) || response.Headers.TryGetValues("x-acs-request-id", out ids)))
        {
            requestId = ids.FirstOrDefault();
        }

        var parts = new List<string> { $"决策模型调用失败（HTTP {(int)response.StatusCode}）" };
        if (!string.IsNullOrWhiteSpace(code))
        {
            parts.Add($"错误码 {code}");
        }
        if (!string.IsNullOrWhiteSpace(message))
        {
            parts.Add(message);
        }
        if (!string.IsNullOrWhiteSpace(requestId))
        {
            parts.Add($"请求 ID {requestId}");
        }
        return new HttpRequestException(string.Join("，", parts.Select(FormatErrorPart)), null, response.StatusCode);

    }


    /// <summary>
    /// 隐去供应商可能回显的密钥并限制单项错误提示长度
    /// </summary>
    private string FormatErrorPart(string value)
    {

        var text = string.IsNullOrEmpty(config.ApiKey) ? value : value.Replace(config.ApiKey, "[已隐藏]", StringComparison.Ordinal);
        return text.Length <= 512 ? text : text[..512] + "…";

    }


    /// <summary>
    /// 读取错误响应中的文本或数字字段
    /// </summary>
    private static string? ReadErrorField(JsonElement parent, string name)
    {

        if (parent.ValueKind != JsonValueKind.Object || !parent.TryGetProperty(name, out var field))
        {
            return null;
        }
        return field.ValueKind is JsonValueKind.String or JsonValueKind.Number ? field.ToString() : null;

    }


    /// <summary>
    /// 判断节点是否为非空文字说明
    /// </summary>
    private static bool IsDescription(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text);


    /// <summary>
    /// 判断数值是否为有效概率
    /// </summary>
    private static bool IsProbability(double? value) => value.HasValue && double.IsFinite(value.Value) && value.Value >= 0 && value.Value <= 1;


    /// <summary>
    /// 拒绝缺失问题、错误选项以及无效概率和评分
    /// </summary>
    private static void ValidateResponse(DecisionResponse response, IReadOnlyDictionary<string, DecisionQuestion> questions)
    {

        if (string.IsNullOrWhiteSpace(response.Model) || response.Answers == null || response.Answers.Count != questions.Count)
        {
            throw new InvalidOperationException("决策响应缺少模型或完整的问题答案");
        }

        foreach (var (id, question) in questions)
        {
            if (!response.Answers.TryGetValue(id, out var answer) || answer == null || answer.Type != question.Type)
            {
                throw new InvalidOperationException($"决策响应的问题 {id} 缺失或类型不匹配");
            }

            if (question.Type == "noul")
            {
                if (!IsProbability(answer.Noul))
                {
                    throw new InvalidOperationException($"问题 {id} 的是否判断概率无效");
                }
                continue;
            }

            var expectedKeys = question.Type == "choice"
                ? ((JsonObject)question.Criteria!).Select(option => option.Key).ToHashSet(StringComparer.Ordinal)
                : Enumerable.Range(0, ((JsonArray)question.Criteria!).Count).Select(index => index.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToHashSet(StringComparer.Ordinal);
            if (!IsProbability(answer.Confidence) || answer.Probabilities == null || !expectedKeys.SetEquals(answer.Probabilities.Keys) || answer.Probabilities.Values.Any(value => !IsProbability(value)))
            {
                throw new InvalidOperationException($"问题 {id} 的概率分布或置信度无效");
            }

            if (question.Type == "choice" && (answer.Choice == null || !expectedKeys.Contains(answer.Choice)))
            {
                throw new InvalidOperationException($"问题 {id} 返回了不在候选范围内的分类");
            }

            if (question.Type == "score" && (!answer.Score.HasValue || !double.IsFinite(answer.Score.Value) || answer.Score.Value < 0 || answer.Score.Value > expectedKeys.Count - 1 || answer.Legend == null || !expectedKeys.SetEquals(answer.Legend.Keys)))
            {
                throw new InvalidOperationException($"问题 {id} 的评分或等级说明无效");
            }
        }

    }

}
