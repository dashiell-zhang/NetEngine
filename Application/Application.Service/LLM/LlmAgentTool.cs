using LLM;
using System.Text.Json;
using System.Text.Encodings.Web;
using System.Text.Json.Nodes;

namespace Application.Service.LLM;

public sealed record LlmAgentTool(
    ToolDefinition Definition,
    Func<string, CancellationToken, Task<string>> InvokeAsync
)
{
    private static readonly JsonSerializerOptions DeserializeOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonSerializerOptions SerializeOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public string Name => Definition.Name;

    public static LlmAgentTool Create(
        string name,
        JsonNode parametersSchema,
        Func<string, CancellationToken, Task<string>> invokeAsync,
        string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Tool name is required.", nameof(name));
        }

        if (parametersSchema == null)
        {
            throw new ArgumentNullException(nameof(parametersSchema));
        }

        if (invokeAsync == null)
        {
            throw new ArgumentNullException(nameof(invokeAsync));
        }

        _ = JsonSerializer.Serialize(parametersSchema);

        return new LlmAgentTool(
            new ToolDefinition(name.Trim(), parametersSchema, description),
            invokeAsync);
    }

    public static LlmAgentTool Create<TArgs, TResult>(
        string name,
        JsonNode parametersSchema,
        Func<TArgs, CancellationToken, Task<TResult>> invokeAsync,
        string? description = null)
        where TArgs : class
    {
        if (invokeAsync == null)
        {
            throw new ArgumentNullException(nameof(invokeAsync));
        }

        return Create(
            name,
            parametersSchema,
            async (argumentsJson, ct) =>
            {
                TArgs? args;
                try
                {
                    args = JsonSerializer.Deserialize<TArgs>(argumentsJson, DeserializeOptions);
                }
                catch (JsonException ex)
                {
                    throw new InvalidOperationException($"Tool '{name}' arguments is invalid JSON.", ex);
                }

                if (args == null)
                {
                    throw new InvalidOperationException($"Tool '{name}' arguments is empty.");
                }

                var result = await invokeAsync(args, ct);

                if (result is JsonNode node)
                {
                    return JsonSerializer.Serialize(EnsureObjectResult(node), SerializeOptions);
                }

                var serializedNode = JsonSerializer.SerializeToNode(result, SerializeOptions);
                return JsonSerializer.Serialize(EnsureObjectResult(serializedNode), SerializeOptions);
            },
            description);
    }

    private static JsonNode EnsureObjectResult(JsonNode? node)
    {
        if (node is JsonObject)
        {
            return node;
        }

        return new JsonObject
        {
            ["result"] = node
        };
    }
}
