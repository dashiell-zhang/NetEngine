using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace LLM.SystemOne;

/// <summary>
/// System One 问题定义
/// </summary>
public sealed class DecisionQuestion
{

    /// <summary>
    /// 决策类型：choice、noul 或 score
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;


    /// <summary>
    /// 文字、对象或数组形式的问题说明
    /// </summary>
    [JsonPropertyName("instructions")]
    public JsonNode? Instructions { get; set; }


    /// <summary>
    /// 分类选项、是否判断标准或有序评分等级
    /// </summary>
    [JsonPropertyName("criteria")]
    [JsonConverter(typeof(CriteriaConverter))]
    public JsonNode? Criteria { get; set; }


    /// <summary>
    /// 保留标准中的原始 JSON 键名供选项校验使用
    /// </summary>
    private sealed class CriteriaConverter : JsonConverter<JsonNode>
    {

        /// <summary>
        /// 按原始大小写读取标准节点
        /// </summary>
        public override JsonNode? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => JsonNode.Parse(ref reader);


        /// <summary>
        /// 将标准节点写入请求正文
        /// </summary>
        public override void Write(Utf8JsonWriter writer, JsonNode value, JsonSerializerOptions options) => value.WriteTo(writer, options);

    }

}
