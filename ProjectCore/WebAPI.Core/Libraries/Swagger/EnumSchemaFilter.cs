using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Text;
using System.Text.Json.Nodes;
using System.Reflection;

namespace WebAPI.Core.Libraries.Swagger;

/// <summary>
/// 为枚举类型的 schema 补充枚举名称与注释信息
/// </summary>
/// <remarks>
/// Swagger 默认只会在 schema.enum 中输出数值列表
/// 本过滤器会追加 x-enumNames 与 x-enumDescriptions 扩展字段
/// 同时把可读的枚举映射文本拼接到 schema.description
/// 以便 Swagger UI 的 Schemas 面板与前端代码生成器可直接利用
/// </remarks>
public sealed class EnumSchemaFilter : ISchemaFilter
{

    /// <summary>
    /// 在生成 schema 时对枚举类型进行增强
    /// </summary>
    /// <param name="schema">OpenAPI schema 对象</param>
    /// <param name="context">过滤器上下文</param>
    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        // Swashbuckle 传入的是 IOpenApiSchema 接口
        // 需要转换到可写的 OpenApiSchema 才能设置 Extensions 与 Description
        if (schema is not OpenApiSchema openApiSchema)
        {
            return;
        }

        var enumType = context.Type;
        if (!enumType.IsEnum)
        {
            return;
        }

        // Flags 枚举允许按位组合
        // 这里写入提示与扩展字段供前端识别
        var isFlagsEnum = enumType.IsDefined(typeof(FlagsAttribute), inherit: false);

        // 某些情况下 schema.Enum 可能为空
        // 这里兜底填充以确保后续可以按枚举值顺序生成扩展信息
        if (openApiSchema.Enum is null || openApiSchema.Enum.Count == 0)
        {
            openApiSchema.Enum = Enum.GetValues(enumType)
                .Cast<object>()
                .Select(v => (JsonNode)JsonValue.Create(Convert.ToInt64(v))!)
                .ToList();
        }

        var enumNames = new JsonArray();
        var enumDescriptions = new JsonArray();

        // 组装成 0 = None: 无; 1 = Read 这样的可读文本
        // 当成员无注释时仅输出 1 = Read 避免 Read: Read
        var mapping = new StringBuilder(256);
        for (var i = 0; i < openApiSchema.Enum.Count; i++)
        {
            var valueNode = openApiSchema.Enum[i];
            if (!TryGetEnumValueInt64(valueNode, out var value))
            {
                continue;
            }

            var enumValue = Enum.ToObject(enumType, value);
            var memberName = Enum.GetName(enumType, enumValue);
            if (string.IsNullOrWhiteSpace(memberName))
            {
                continue;
            }

            var description = EnumDocHelper.GetEnumMemberDescription(enumType, memberName);

            enumNames.Add(memberName);
            enumDescriptions.Add(description ?? memberName);

            if (mapping.Length > 0)
            {
                mapping.Append("; ");
            }

            mapping.Append(value);
            mapping.Append(" = ");
            mapping.Append(memberName);

            // 仅当描述存在且不等于名称时才追加描述
            if (!string.IsNullOrWhiteSpace(description) && !string.Equals(description, memberName, StringComparison.Ordinal))
            {
                mapping.Append(": ");
                mapping.Append(description);
            }
        }

        if (enumNames.Count > 0)
        {
            openApiSchema.Extensions ??= new Dictionary<string, IOpenApiExtension>(StringComparer.Ordinal);
            openApiSchema.Extensions["x-enumNames"] = new JsonNodeExtension(enumNames);
            openApiSchema.Extensions["x-enumDescriptions"] = new JsonNodeExtension(enumDescriptions);
        }

        // 给 Flags 枚举打标识 便于前端自定义渲染
        if (isFlagsEnum)
        {
            openApiSchema.Extensions ??= new Dictionary<string, IOpenApiExtension>(StringComparer.Ordinal);
            openApiSchema.Extensions["x-enumFlags"] = new JsonNodeExtension(JsonValue.Create(true)!);
        }

        // 把映射文本写到 description 以便 Swagger UI 的 Schemas 面板可直接看到
        var mappingText = mapping.ToString();
        if (!string.IsNullOrWhiteSpace(mappingText))
        {
            var flagsHint = isFlagsEnum ? "（Flags 枚举：可按位组合）" : null;

            openApiSchema.Description =
                string.IsNullOrWhiteSpace(openApiSchema.Description)
                    ? (flagsHint is null ? mappingText : flagsHint + Environment.NewLine + Environment.NewLine + mappingText)
                    : (flagsHint is null
                        ? openApiSchema.Description + Environment.NewLine + Environment.NewLine + mappingText
                        : openApiSchema.Description + Environment.NewLine + Environment.NewLine + flagsHint + Environment.NewLine + Environment.NewLine + mappingText);
        }
    }


    /// <summary>
    /// 从 schema.enum 的 JsonNode 里读取整数值
    /// </summary>
    /// <param name="any">枚举值节点</param>
    /// <param name="value">输出的 long 值</param>
    /// <returns>解析成功返回 true</returns>
    private static bool TryGetEnumValueInt64(JsonNode any, out long value)
    {
        // Microsoft.OpenApi 2.x 的 schema.Enum 使用 JsonNode 存储
        if (any is JsonValue jsonValue)
        {
            if (jsonValue.TryGetValue<long>(out value))
            {
                return true;
            }

            if (jsonValue.TryGetValue<int>(out var intValue))
            {
                value = intValue;
                return true;
            }
        }

        value = default;
        return false;
    }

}
