using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Text.Json.Nodes;

namespace WebAPI.Core.Libraries.Swagger;

/// <summary>
/// 为接口参数上的枚举类型补充更易读的说明
/// </summary>
/// <remarks>
/// Swagger UI 对参数枚举通常只展示整数列表
/// 本过滤器把枚举值与注释拼接到参数 description 中
/// 以便前端在参数输入处直接理解每个值的含义
/// </remarks>
public sealed class EnumParameterDescriptionOperationFilter : IOperationFilter
{

    /// <summary>
    /// 在生成操作文档时为枚举参数追加描述
    /// </summary>
    /// <param name="operation">OpenAPI 操作对象</param>
    /// <param name="context">过滤器上下文</param>
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (operation.Parameters is null || operation.Parameters.Count == 0)
        {
            return;
        }

        // ApiDescription 提供了控制器 action 的参数元信息
        // operation.Parameters 则是 Swagger 生成的 OpenAPI 参数集合
        foreach (var apiParam in context.ApiDescription.ParameterDescriptions)
        {
            var paramType = apiParam.Type ?? apiParam.ModelMetadata?.ModelType;
            if (paramType is null)
            {
                continue;
            }

            paramType = Nullable.GetUnderlyingType(paramType) ?? paramType;
            if (!paramType.IsEnum)
            {
                continue;
            }

            // Flags 枚举允许按位组合 通过提示文案帮助前端理解输入方式
            var isFlagsEnum = paramType.IsDefined(typeof(FlagsAttribute), inherit: false);

            // 使用参数名匹配 OpenAPI 参数对象
            // 名称在不同版本的生成器中大小写可能不一致 因此用 OrdinalIgnoreCase
            var openApiParam = operation.Parameters.FirstOrDefault(p =>
                string.Equals(p.Name, apiParam.Name, StringComparison.OrdinalIgnoreCase));

            if (openApiParam is null)
            {
                continue;
            }

            // 构建映射文本并拼接到参数描述中
            var mapping = EnumDocHelper.BuildEnumMappingText(paramType);
            if (string.IsNullOrWhiteSpace(mapping))
            {
                continue;
            }

            var flagsHint = isFlagsEnum ? "（Flags 枚举：可按位组合）" : null;
            var prefix = flagsHint is null ? mapping : flagsHint + Environment.NewLine + Environment.NewLine + mapping;

            openApiParam.Description = string.IsNullOrWhiteSpace(openApiParam.Description)
                ? prefix
                : openApiParam.Description + Environment.NewLine + Environment.NewLine + prefix;

            // 扩展字段用于前端自定义渲染
            // 注意 openApiParam 的 Extensions 接口是只读的 需要转换到具体类型写入
            var descriptions = BuildEnumDescriptionsArray(paramType);
            if (descriptions.Count > 0 && openApiParam is OpenApiParameter concreteParam)
            {
                concreteParam.Extensions ??= new Dictionary<string, IOpenApiExtension>(StringComparer.Ordinal);
                concreteParam.Extensions["x-enumDescriptions"] = new JsonNodeExtension(descriptions);
                if (isFlagsEnum)
                {
                    concreteParam.Extensions["x-enumFlags"] = new JsonNodeExtension(JsonValue.Create(true)!);
                }
            }
        }
    }


    /// <summary>
    /// 构建枚举成员描述数组
    /// </summary>
    /// <param name="enumType">枚举类型</param>
    /// <returns>按 Enum.GetValues 顺序排列的描述数组</returns>
    /// <remarks>
    /// 当某个成员缺少注释时 回退为成员名称
    /// </remarks>
    private static JsonArray BuildEnumDescriptionsArray(Type enumType)
    {
        var arr = new JsonArray();
        foreach (var value in Enum.GetValues(enumType).Cast<object>())
        {
            var name = Enum.GetName(enumType, value);
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var description = EnumDocHelper.GetEnumMemberDescription(enumType, name) ?? name;
            arr.Add(description);
        }

        return arr;
    }

}
