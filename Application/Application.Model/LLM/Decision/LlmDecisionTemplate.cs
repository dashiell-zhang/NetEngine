using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Application.Model.LLM.Decision;

/// <summary>
/// 管理端预览与服务端调用共用的决策请求模板
/// </summary>
public static partial class LlmDecisionTemplate
{

    /// <summary>
    /// 保留中文的编辑和预览格式
    /// </summary>
    public static JsonSerializerOptions DisplayOptions { get; } = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };


    /// <summary>
    /// 匹配文本参数、必填标记和参数说明
    /// </summary>
    [GeneratedRegex(@"\{\{\s*(?<required>\*)?\s*(?<key>[^{}|\s]+)\s*(?:\|\s*(?<comment>[^{}]*?))?\s*\}\}")]
    public static partial Regex PlaceholderPattern();


    /// <summary>
    /// 读取决策模板，检查固定题型并移除由模型配置提供的根字段
    /// </summary>
    public static JsonObject Parse(string template, out bool removedModel)
    {

        if (string.IsNullOrWhiteSpace(template))
        {
            throw new InvalidOperationException("请填写决策请求模板");
        }
        try
        {
            var root = JsonNode.Parse(template, documentOptions: new JsonDocumentOptions { AllowDuplicateProperties = false }) as JsonObject
                ?? throw new InvalidOperationException("决策请求模板必须是 JSON 对象");
            ValidateQuestionTypes(root);
            removedModel = root.Remove("model");
            return root;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"决策模板 JSON 格式错误（第 {ex.LineNumber + 1} 行）：{ex.Message}", ex);
        }

    }


    /// <summary>
    /// 在替换文本参数前校验每道题的固定类型
    /// </summary>
    private static void ValidateQuestionTypes(JsonObject root)
    {

        if (root["questions"] is not JsonObject questions)
        {
            throw new InvalidOperationException("questions 必须是问题对象");
        }

        foreach (var (id, question) in questions)
        {
            // 与协议反序列化保持一致，大小写不同的 type 也属于同一字段
            var types = (question as JsonObject)?.Where(field => field.Key.Equals("type", StringComparison.OrdinalIgnoreCase))
                .Select(field => field.Value).ToArray();
            if (types is not [JsonValue value] || !value.TryGetValue<string>(out var type) || type is not ("noul" or "choice" or "score"))
            {
                throw new InvalidOperationException($"问题 {id} 的 type 必须固定为 noul、choice 或 score");
            }
        }

    }


    /// <summary>
    /// 枚举支持参数的文本值，字段名和问题标识保持固定
    /// </summary>
    public static IEnumerable<JsonValue> TextValues(JsonNode? node)
    {

        if (node is JsonObject obj)
        {
            foreach (var (key, child) in obj)
            {
                if (PlaceholderPattern().IsMatch(key))
                {
                    throw new InvalidOperationException("占位符只能写在文本值中，不能用作字段名或问题标识");
                }
                foreach (var value in TextValues(child))
                {
                    yield return value;
                }
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var child in array)
            {
                foreach (var value in TextValues(child))
                {
                    yield return value;
                }
            }
        }
        else if (node is JsonValue text && text.TryGetValue<string>(out _))
        {
            yield return text;
        }

    }


    /// <summary>
    /// 在模板节点中按文本替换参数，未传的可选参数保留原占位符
    /// </summary>
    public static void Render(JsonObject root, IReadOnlyDictionary<string, string> parameters)
    {

        var values = new Dictionary<string, string>(parameters, StringComparer.OrdinalIgnoreCase);
        foreach (var node in TextValues(root).ToList())
        {
            var text = node.GetValue<string>();
            if (!PlaceholderPattern().IsMatch(text))
            {
                continue;
            }
            var rendered = PlaceholderPattern().Replace(text, match =>
            {
                var key = match.Groups["key"].Value;
                var found = values.TryGetValue(key, out var value);
                if (match.Groups["required"].Success && string.IsNullOrWhiteSpace(value))
                {
                    throw new InvalidOperationException($"必传参数未填写：{key}");
                }
                return found ? value ?? string.Empty : match.Value;
            });
            node.ReplaceWith(rendered);
        }

    }

}
