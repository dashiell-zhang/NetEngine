using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace SourceGenerator.Core;

/// <summary>
/// 根据 [LlmTool] 方法生成工具注册代码（每个项目独立生成一份）。
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class LlmToolGenerator : IIncrementalGenerator
{
    private const string LlmToolAttributeMetadataName = "SourceGenerator.Runtime.Attributes.LlmToolAttribute";
    private const string LlmToolParamAttributeMetadataName = "SourceGenerator.Runtime.Attributes.LlmToolParamAttribute";
    private const string DescriptionAttributeMetadataName = "System.ComponentModel.DescriptionAttribute";
    private const string LlmAgentToolMetadataName = "Application.Service.LLM.LlmAgentTool";

    private sealed class ToolCandidate
    {
        public IMethodSymbol Method { get; }

        public AttributeData Attribute { get; }

        public ToolCandidate(IMethodSymbol method, AttributeData attribute)
        {
            Method = method;
            Attribute = attribute;
        }
    }

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider.ForAttributeWithMetadataName(
            LlmToolAttributeMetadataName,
            static (node, _) => node is MethodDeclarationSyntax,
            static (syntaxContext, _) => new ToolCandidate((IMethodSymbol)syntaxContext.TargetSymbol, syntaxContext.Attributes[0]));

        var collected = candidates.Collect();

        context.RegisterSourceOutput(
            context.CompilationProvider.Combine(collected),
            static (spc, pair) => Execute(spc, pair.Left, pair.Right));
    }

    private static void Execute(SourceProductionContext context, Compilation compilation, ImmutableArray<ToolCandidate> candidates)
    {
        var llmAgentToolSymbol = compilation.GetTypeByMetadataName(LlmAgentToolMetadataName);
        if (llmAgentToolSymbol == null)
        {
            // 当前项目未引用 Application.Service：不生成，避免编译错误。
            return;
        }

        var llmToolAttributeSymbol = compilation.GetTypeByMetadataName(LlmToolAttributeMetadataName);
        var toolParamAttributeSymbol = compilation.GetTypeByMetadataName(LlmToolParamAttributeMetadataName);
        var descriptionAttributeSymbol = compilation.GetTypeByMetadataName(DescriptionAttributeMetadataName);
        var cancellationTokenSymbol = compilation.GetTypeByMetadataName("System.Threading.CancellationToken");

        var allCandidates = new List<ToolCandidate>(candidates.Length + 32);
        foreach (var c in candidates)
        {
            allCandidates.Add(c);
        }

        // Also include tools declared in referenced project assemblies, so a startup project can expose tools
        // defined in Application.Service (or other referenced projects) without runtime reflection scanning.
        if (llmToolAttributeSymbol != null)
        {
            foreach (var asm in compilation.SourceModule.ReferencedAssemblySymbols)
            {
                var asmName = asm.Identity?.Name;
                if (ShouldSkipReferencedAssembly(asmName))
                {
                    continue;
                }

                CollectToolCandidatesFromNamespace(asm.GlobalNamespace, llmToolAttributeSymbol, allCandidates);
            }
        }

        var tools = new List<GeneratedTool>();
        var generatedArgsTypes = new List<GeneratedArgsType>();

        foreach (var c in allCandidates)
        {
            if (c.Method == null)
            {
                continue;
            }

            if (c.Method.MethodKind != MethodKind.Ordinary || c.Method.IsStatic)
            {
                continue;
            }

            if (c.Method.DeclaredAccessibility != Accessibility.Public)
            {
                continue;
            }

            if (!TryGetToolNameAndDescription(c.Attribute, out var toolName, out var toolDescription))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(toolName))
            {
                continue;
            }

            // If [LlmTool] didn't provide a description, fall back to [Description] on the method
            if (string.IsNullOrWhiteSpace(toolDescription))
            {
                toolDescription = GetDescriptionFromAttributes(c.Method.GetAttributes(), descriptionAttributeSymbol);
            }

            if (!TryGetArgsAndResultTypes(compilation, c.Method, cancellationTokenSymbol, out var argsType, out var resultType, out var hasCancellationToken))
            {
                continue;
            }

            var declaringType = c.Method.ContainingType;
            if (declaringType == null)
            {
                continue;
            }

            var returnsAwaitable = ReturnsAwaitable(compilation, c.Method);
            toolDescription = NormalizeToolDescription(toolDescription);

            JsonSchemaObject schema;
            var effectiveArgsTypeFullName = argsType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            List<string>? multiParamAccessors = null;
            if (TryGetNonCancellationTokenParameters(c.Method, cancellationTokenSymbol, out var methodArgs))
            {
                if (methodArgs.Count == 1)
                {
                    if (ShouldGenerateArgsWrapper(methodArgs[0].Type))
                    {
                        var generatedArgs = GeneratedArgsType.Create(declaringType, c.Method, methodArgs, toolParamAttributeSymbol, descriptionAttributeSymbol);
                        generatedArgsTypes.Add(generatedArgs);
                        effectiveArgsTypeFullName = generatedArgs.TypeFullName;
                        multiParamAccessors = generatedArgs.Properties.Select(p => p.Identifier).ToList();
                        schema = BuildParametersSchemaFromParameters(compilation, methodArgs, toolParamAttributeSymbol, descriptionAttributeSymbol);
                    }
                    else
                    {
                        schema = BuildParametersSchemaFromArgsType(compilation, argsType, toolParamAttributeSymbol, descriptionAttributeSymbol);
                    }
                }
                else
                {
                    // 多参数：生成一个内部参数类型，用于统一的 JSON 反序列化和 schema 输出。
                    var generatedArgs = GeneratedArgsType.Create(declaringType, c.Method, methodArgs, toolParamAttributeSymbol, descriptionAttributeSymbol);
                    generatedArgsTypes.Add(generatedArgs);
                    effectiveArgsTypeFullName = generatedArgs.TypeFullName;
                    multiParamAccessors = generatedArgs.Properties.Select(p => p.Identifier).ToList();
                    schema = BuildParametersSchemaFromParameters(compilation, methodArgs, toolParamAttributeSymbol, descriptionAttributeSymbol);
                }
            }
            else
            {
                schema = BuildParametersSchemaFromArgsType(compilation, argsType, toolParamAttributeSymbol, descriptionAttributeSymbol);
            }

            tools.Add(new GeneratedTool(
                toolName.Trim(),
                toolDescription,
                declaringType,
                c.Method,
                effectiveArgsTypeFullName,
                resultType,
                returnsAwaitable,
                hasCancellationToken,
                multiParamAccessors,
                schema));
        }

        var src = GenerateSource(tools, generatedArgsTypes);
        context.AddSource("LlmTools.g.cs", src);
    }

    private static bool ShouldSkipReferencedAssembly(string? assemblyName)
    {
        if (string.IsNullOrWhiteSpace(assemblyName))
        {
            return true;
        }

        var name = assemblyName!;

        // Avoid scanning framework & common third-party assemblies for performance.
        if (name.StartsWith("System", StringComparison.Ordinal) ||
            name.StartsWith("Microsoft", StringComparison.Ordinal) ||
            name.Equals("netstandard", StringComparison.Ordinal) ||
            name.Equals("mscorlib", StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }

    private static void CollectToolCandidatesFromNamespace(
        INamespaceSymbol ns,
        INamedTypeSymbol llmToolAttributeSymbol,
        List<ToolCandidate> output)
    {
        foreach (var m in ns.GetMembers())
        {
            if (m is INamespaceSymbol childNs)
            {
                CollectToolCandidatesFromNamespace(childNs, llmToolAttributeSymbol, output);
            }
            else if (m is INamedTypeSymbol type)
            {
                CollectToolCandidatesFromType(type, llmToolAttributeSymbol, output);
            }
        }
    }

    private static void CollectToolCandidatesFromType(
        INamedTypeSymbol type,
        INamedTypeSymbol llmToolAttributeSymbol,
        List<ToolCandidate> output)
    {
        foreach (var nested in type.GetTypeMembers())
        {
            CollectToolCandidatesFromType(nested, llmToolAttributeSymbol, output);
        }

        foreach (var method in type.GetMembers().OfType<IMethodSymbol>())
        {
            if (method.MethodKind != MethodKind.Ordinary || method.IsStatic)
            {
                continue;
            }

            if (method.DeclaredAccessibility != Accessibility.Public)
            {
                continue;
            }

            AttributeData? toolAttr = null;
            foreach (var a in method.GetAttributes())
            {
                if (SymbolEqualityComparer.Default.Equals(a.AttributeClass, llmToolAttributeSymbol))
                {
                    toolAttr = a;
                    break;
                }
            }

            if (toolAttr == null)
            {
                continue;
            }

            output.Add(new ToolCandidate(method, toolAttr));
        }
    }

    private sealed class GeneratedTool
    {
        public string Name { get; }

        public string? Description { get; }

        public INamedTypeSymbol DeclaringType { get; }

        public IMethodSymbol Method { get; }

        public string ArgsTypeFullName { get; }

        public ITypeSymbol ResultType { get; }

        public bool ReturnsAwaitable { get; }

        public bool HasCancellationToken { get; }

        public IReadOnlyList<string>? MultiParameterAccessors { get; }

        public JsonSchemaObject ParametersSchema { get; }

        public GeneratedTool(
            string name,
            string? description,
            INamedTypeSymbol declaringType,
            IMethodSymbol method,
            string argsTypeFullName,
            ITypeSymbol resultType,
            bool returnsAwaitable,
            bool hasCancellationToken,
            IReadOnlyList<string>? multiParameterAccessors,
            JsonSchemaObject parametersSchema)
        {
            Name = name;
            Description = description;
            DeclaringType = declaringType;
            Method = method;
            ArgsTypeFullName = argsTypeFullName;
            ResultType = resultType;
            ReturnsAwaitable = returnsAwaitable;
            HasCancellationToken = hasCancellationToken;
            MultiParameterAccessors = multiParameterAccessors;
            ParametersSchema = parametersSchema;
        }
    }

    private sealed class GeneratedArgsType
    {
        public string TypeName { get; }

        public string TypeFullName { get; }

        public IReadOnlyList<GeneratedArgsProperty> Properties { get; }

        private GeneratedArgsType(string typeName, string typeFullName, IReadOnlyList<GeneratedArgsProperty> properties)
        {
            TypeName = typeName;
            TypeFullName = typeFullName;
            Properties = properties;
        }

        public static GeneratedArgsType Create(
            INamedTypeSymbol declaringType,
            IMethodSymbol method,
            IReadOnlyList<IParameterSymbol> parameters,
            INamedTypeSymbol? toolParamAttributeSymbol,
            INamedTypeSymbol? descriptionAttributeSymbol)
        {
            var typeName = "LlmToolArgs_" + SanitizeIdentifier(declaringType.Name) + "_" + SanitizeIdentifier(method.Name);
            var fullTypeName = "global::NetEngine.Generated.LlmToolGeneratedArgs." + typeName;

            var props = new List<GeneratedArgsProperty>();
            foreach (var p in parameters)
            {
                var (jsonName, desc) = GetParamNameAndDescription(p, toolParamAttributeSymbol, descriptionAttributeSymbol);
                var identifier = ToPascalCase(SanitizeIdentifier(p.Name));
                if (string.IsNullOrWhiteSpace(identifier))
                {
                    identifier = "P";
                }

                props.Add(new GeneratedArgsProperty(identifier, jsonName, desc, p.Type));
            }

            return new GeneratedArgsType(typeName, fullTypeName, props);
        }

        private static string SanitizeIdentifier(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "X";
            }

            var sb = new StringBuilder(name.Length);
            foreach (var ch in name)
            {
                if (char.IsLetterOrDigit(ch) || ch == '_')
                {
                    sb.Append(ch);
                }
                else
                {
                    sb.Append('_');
                }
            }

            return sb.ToString();
        }

        private static string ToPascalCase(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return name;
            }

            if (name.Length == 1)
            {
                return name.ToUpperInvariant();
            }

            return char.ToUpperInvariant(name[0]) + name.Substring(1);
        }
    }

    private sealed class GeneratedArgsProperty
    {
        public string Identifier { get; }

        public string JsonName { get; }

        public string? Description { get; }

        public ITypeSymbol Type { get; }

        public GeneratedArgsProperty(string identifier, string jsonName, string? description, ITypeSymbol type)
        {
            Identifier = identifier;
            JsonName = jsonName;
            Description = description;
            Type = type;
        }
    }


    private static bool TryGetToolNameAndDescription(AttributeData attribute, out string name, out string? description)
    {
        name = string.Empty;
        description = null;

        if (attribute.ConstructorArguments.Length == 0)
        {
            return false;
        }

        var nameArg = attribute.ConstructorArguments[0];
        if (nameArg.Value is not string n)
        {
            return false;
        }

        name = n;

        if (attribute.ConstructorArguments.Length >= 2)
        {
            if (attribute.ConstructorArguments[1].Value is string d)
            {
                description = d;
            }
        }

        return true;
    }

    private static bool TryGetArgsAndResultTypes(
        Compilation compilation,
        IMethodSymbol method,
        INamedTypeSymbol? cancellationTokenSymbol,
        out ITypeSymbol argsType,
        out ITypeSymbol resultType,
        out bool hasCancellationToken)
    {
        argsType = null!;
        resultType = null!;
        hasCancellationToken = false;

        if (method.Parameters.Length == 0)
        {
            return false;
        }

        var firstParam = method.Parameters[0];
        if (cancellationTokenSymbol != null && SymbolEqualityComparer.Default.Equals(firstParam.Type, cancellationTokenSymbol))
        {
            return false;
        }

        argsType = firstParam.Type;

        if (method.Parameters.Length >= 2 &&
            cancellationTokenSymbol != null &&
            SymbolEqualityComparer.Default.Equals(method.Parameters[method.Parameters.Length - 1].Type, cancellationTokenSymbol))
        {
            hasCancellationToken = true;
        }

        // return type: Task<T> / ValueTask<T> / T
        var taskOfT = compilation.GetTypeByMetadataName("System.Threading.Tasks.Task`1");
        var valueTaskOfT = compilation.GetTypeByMetadataName("System.Threading.Tasks.ValueTask`1");

        if (method.ReturnType is INamedTypeSymbol named &&
            named.IsGenericType &&
            ((taskOfT != null && SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, taskOfT)) ||
             (valueTaskOfT != null && SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, valueTaskOfT))))
        {
            resultType = named.TypeArguments[0];
            return true;
        }

        // allow non-async TResult
        resultType = method.ReturnType;
        return true;
    }

    private static bool ReturnsAwaitable(Compilation compilation, IMethodSymbol method)
    {
        var taskOfT = compilation.GetTypeByMetadataName("System.Threading.Tasks.Task`1");
        var valueTaskOfT = compilation.GetTypeByMetadataName("System.Threading.Tasks.ValueTask`1");

        if (method.ReturnType is INamedTypeSymbol named &&
            named.IsGenericType &&
            ((taskOfT != null && SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, taskOfT)) ||
             (valueTaskOfT != null && SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, valueTaskOfT))))
        {
            return true;
        }

        return false;
    }

    private static string? NormalizeToolDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return description;
        }

        // 统一约定：工具返回 JSON object；primitive 会自动封装为 {"result": ...}
        // 避免每个工具重复写返回格式说明。
        const string suffix = "（返回 JSON：{\"result\": ...}）";

        // 已经写了返回约定的就不再追加
        if (description.Contains("\"result\"", StringComparison.OrdinalIgnoreCase) ||
            description.Contains("{\"result\"", StringComparison.OrdinalIgnoreCase) ||
            description.Contains("返回 JSON", StringComparison.OrdinalIgnoreCase) ||
            description.Contains("封装为", StringComparison.OrdinalIgnoreCase))
        {
            return description;
        }

        return description!.TrimEnd() + suffix;
    }

    private static bool ShouldGenerateArgsWrapper(ITypeSymbol type)
    {
        if (type == null)
        {
            return false;
        }

        if (type.TypeKind == TypeKind.Enum)
        {
            return true;
        }

        if (type.SpecialType != SpecialType.None && type.SpecialType != SpecialType.System_Object)
        {
            return true;
        }

        if (type is INamedTypeSymbol named &&
            named.ContainingNamespace != null &&
            named.ContainingNamespace.ToDisplayString().StartsWith("System", StringComparison.Ordinal))
        {
            // System.Guid/System.DateTime 等：通常更适合作为单值参数，而不是扫描其一堆属性生成 schema
            return true;
        }

        return false;
    }

    private sealed class JsonSchemaObject
    {
        public IReadOnlyList<JsonSchemaProperty> Properties { get; }

        public IReadOnlyList<string> Required { get; }

        public bool AdditionalPropertiesFalse { get; }

        public JsonSchemaObject(IReadOnlyList<JsonSchemaProperty> properties, IReadOnlyList<string> required, bool additionalPropertiesFalse)
        {
            Properties = properties;
            Required = required;
            AdditionalPropertiesFalse = additionalPropertiesFalse;
        }
    }

    private sealed class JsonSchemaProperty
    {
        public string Name { get; }

        public string? Description { get; }

        public JsonSchemaType SchemaType { get; }

        public IReadOnlyList<string>? EnumValues { get; }

        public JsonSchemaProperty(string name, string? description, JsonSchemaType schemaType, IReadOnlyList<string>? enumValues)
        {
            Name = name;
            Description = description;
            SchemaType = schemaType;
            EnumValues = enumValues;
        }
    }

    private enum JsonSchemaType
    {
        String,
        Boolean,
        Integer,
        Number,
        Object,
        Array
    }

    private static JsonSchemaObject BuildParametersSchema(
        Compilation compilation,
        ITypeSymbol argsType,
        INamedTypeSymbol? toolParamAttributeSymbol,
        INamedTypeSymbol? descriptionAttributeSymbol)
    {
        return BuildParametersSchemaFromArgsType(compilation, argsType, toolParamAttributeSymbol, descriptionAttributeSymbol);
    }

    private static JsonSchemaObject BuildParametersSchemaFromArgsType(
        Compilation compilation,
        ITypeSymbol argsType,
        INamedTypeSymbol? toolParamAttributeSymbol,
        INamedTypeSymbol? descriptionAttributeSymbol)
    {
        var props = new List<JsonSchemaProperty>();
        var required = new List<string>();

        if (argsType is not INamedTypeSymbol namedArgs)
        {
            return new JsonSchemaObject(props, required, additionalPropertiesFalse: true);
        }

        foreach (var p in namedArgs.GetMembers().OfType<IPropertySymbol>())
        {
            if (p.DeclaredAccessibility != Accessibility.Public || p.IsStatic)
            {
                continue;
            }

            if (p.GetMethod == null)
            {
                continue;
            }

            var (jsonName, desc) = GetParamNameAndDescription(p, toolParamAttributeSymbol, descriptionAttributeSymbol);

            var schemaType = MapToJsonSchemaType(compilation, p.Type, out var enumValues);
            props.Add(new JsonSchemaProperty(jsonName, desc, schemaType, enumValues));

            if (IsRequired(p.Type, p.NullableAnnotation))
            {
                required.Add(jsonName);
            }
        }

        return new JsonSchemaObject(props, required, additionalPropertiesFalse: true);
    }

    private static JsonSchemaObject BuildParametersSchemaFromParameters(
        Compilation compilation,
        IReadOnlyList<IParameterSymbol> parameters,
        INamedTypeSymbol? toolParamAttributeSymbol,
        INamedTypeSymbol? descriptionAttributeSymbol)
    {
        var props = new List<JsonSchemaProperty>();
        var required = new List<string>();

        foreach (var p in parameters)
        {
            if (p == null)
            {
                continue;
            }

            var (jsonName, desc) = GetParamNameAndDescription(p, toolParamAttributeSymbol, descriptionAttributeSymbol);
            var schemaType = MapToJsonSchemaType(compilation, p.Type, out var enumValues);
            props.Add(new JsonSchemaProperty(jsonName, desc, schemaType, enumValues));

            if (IsRequiredParameter(p))
            {
                required.Add(jsonName);
            }
        }

        return new JsonSchemaObject(props, required, additionalPropertiesFalse: true);
    }

    private static bool IsRequiredParameter(IParameterSymbol p)
    {
        if (p.HasExplicitDefaultValue || p.IsOptional)
        {
            return false;
        }

        if (p.Type.IsReferenceType)
        {
            return p.NullableAnnotation != NullableAnnotation.Annotated;
        }

        if (p.Type is INamedTypeSymbol named && named.IsGenericType && named.Name == "Nullable" && named.ContainingNamespace.ToDisplayString() == "System")
        {
            return false;
        }

        return true;
    }

    private static (string Name, string? Description) GetParamNameAndDescription(
        IPropertySymbol prop,
        INamedTypeSymbol? toolParamAttributeSymbol,
        INamedTypeSymbol? descriptionAttributeSymbol)
    {
        string? name = null;
        string? desc = null;

        if (toolParamAttributeSymbol != null)
        {
            foreach (var attr in prop.GetAttributes())
            {
                if (!SymbolEqualityComparer.Default.Equals(attr.AttributeClass, toolParamAttributeSymbol))
                {
                    continue;
                }

                if (attr.ConstructorArguments.Length >= 1 && attr.ConstructorArguments[0].Value is string n && !string.IsNullOrWhiteSpace(n))
                {
                    name = n;
                }

                if (attr.ConstructorArguments.Length >= 2 && attr.ConstructorArguments[1].Value is string d && !string.IsNullOrWhiteSpace(d))
                {
                    desc = d;
                }
            }
        }

        desc ??= GetDescriptionFromAttributes(prop.GetAttributes(), descriptionAttributeSymbol);
        name ??= ToCamelCase(prop.Name);
        return (name, desc);
    }

    private static (string Name, string? Description) GetParamNameAndDescription(
        IParameterSymbol param,
        INamedTypeSymbol? toolParamAttributeSymbol,
        INamedTypeSymbol? descriptionAttributeSymbol)
    {
        string? name = null;
        string? desc = null;

        if (toolParamAttributeSymbol != null)
        {
            foreach (var attr in param.GetAttributes())
            {
                if (!SymbolEqualityComparer.Default.Equals(attr.AttributeClass, toolParamAttributeSymbol))
                {
                    continue;
                }

                if (attr.ConstructorArguments.Length >= 1 && attr.ConstructorArguments[0].Value is string n && !string.IsNullOrWhiteSpace(n))
                {
                    name = n;
                }

                if (attr.ConstructorArguments.Length >= 2 && attr.ConstructorArguments[1].Value is string d && !string.IsNullOrWhiteSpace(d))
                {
                    desc = d;
                }
            }
        }

        desc ??= GetDescriptionFromAttributes(param.GetAttributes(), descriptionAttributeSymbol);
        name ??= ToCamelCase(param.Name);
        return (name, desc);
    }

    private static string? GetDescriptionFromAttributes(ImmutableArray<AttributeData> attributes, INamedTypeSymbol? descriptionAttributeSymbol)
    {
        if (descriptionAttributeSymbol == null)
        {
            return null;
        }

        foreach (var attr in attributes)
        {
            if (!SymbolEqualityComparer.Default.Equals(attr.AttributeClass, descriptionAttributeSymbol))
            {
                continue;
            }

            if (attr.ConstructorArguments.Length >= 1 && attr.ConstructorArguments[0].Value is string d && !string.IsNullOrWhiteSpace(d))
            {
                return d;
            }
        }

        return null;
    }

    private static bool TryGetNonCancellationTokenParameters(IMethodSymbol method, INamedTypeSymbol? cancellationTokenSymbol, out List<IParameterSymbol> args)
    {
        args = new List<IParameterSymbol>();

        var count = method.Parameters.Length;
        if (count != 0 &&
            cancellationTokenSymbol != null &&
            SymbolEqualityComparer.Default.Equals(method.Parameters[count - 1].Type, cancellationTokenSymbol))
        {
            count -= 1;
        }

        for (var i = 0; i < count; i++)
        {
            args.Add(method.Parameters[i]);
        }

        return args.Count != 0;
    }

    private static bool IsRequired(ITypeSymbol type, NullableAnnotation annotation)
    {
        if (type.IsReferenceType)
        {
            return annotation != NullableAnnotation.Annotated;
        }

        if (type is INamedTypeSymbol named && named.IsGenericType && named.Name == "Nullable" && named.ContainingNamespace.ToDisplayString() == "System")
        {
            return false;
        }

        return true;
    }

    private static JsonSchemaType MapToJsonSchemaType(Compilation compilation, ITypeSymbol type, out IReadOnlyList<string>? enumValues)
    {
        enumValues = null;

        if (type is INamedTypeSymbol named && named.IsGenericType && named.Name == "Nullable" && named.ContainingNamespace.ToDisplayString() == "System")
        {
            type = named.TypeArguments[0];
        }

        // Treat common "single value" framework types as string in JSON schema.
        // (STJ typically serializes these as JSON strings, not objects.)
        if (type is INamedTypeSymbol sysNamed &&
            sysNamed.ContainingNamespace.ToDisplayString() == "System")
        {
            var n = sysNamed.Name;
            if (n == "Guid" ||
                n == "DateTime" ||
                n == "DateTimeOffset" ||
                n == "DateOnly" ||
                n == "TimeOnly")
            {
                return JsonSchemaType.String;
            }
        }

        if (type.TypeKind == TypeKind.Enum && type is INamedTypeSymbol enumType)
        {
            enumValues = enumType.GetMembers()
                .OfType<IFieldSymbol>()
                .Where(f => f.HasConstantValue)
                .Select(f => f.Name)
                .ToList();
            return JsonSchemaType.String;
        }

        var special = type.SpecialType;
        return special switch
        {
            SpecialType.System_String => JsonSchemaType.String,
            SpecialType.System_Boolean => JsonSchemaType.Boolean,
            SpecialType.System_Int16 or SpecialType.System_Int32 or SpecialType.System_Int64 or
            SpecialType.System_UInt16 or SpecialType.System_UInt32 or SpecialType.System_UInt64 or
            SpecialType.System_Byte or SpecialType.System_SByte => JsonSchemaType.Integer,
            SpecialType.System_Single or SpecialType.System_Double or SpecialType.System_Decimal => JsonSchemaType.Number,
            _ => JsonSchemaType.Object
        };
    }

    private static string GenerateSource(IReadOnlyList<GeneratedTool> tools, IReadOnlyList<GeneratedArgsType> generatedArgsTypes)
    {
        var sb = new StringBuilder(16_384);

        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("namespace NetEngine.Generated;");
        sb.AppendLine();

        if (generatedArgsTypes.Count != 0)
        {
            sb.AppendLine("internal static class LlmToolGeneratedArgs");
            sb.AppendLine("{");

            foreach (var t in generatedArgsTypes)
            {
                sb.Append("    internal sealed class ").Append(t.TypeName).AppendLine();
                sb.AppendLine("    {");
                foreach (var p in t.Properties)
                {
                    if (!string.Equals(p.Identifier, p.JsonName, StringComparison.OrdinalIgnoreCase))
                    {
                        sb.Append("        [global::System.Text.Json.Serialization.JsonPropertyName(")
                            .Append(ToCSharpStringLiteral(p.JsonName))
                            .AppendLine(")]");
                    }

                    sb.Append("        public ").Append(p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)).Append(" ")
                        .Append(p.Identifier).AppendLine(" { get; set; }");
                    sb.AppendLine();
                }
                sb.AppendLine("    }");
                sb.AppendLine();
            }

            sb.AppendLine("}");
            sb.AppendLine();
        }

        sb.AppendLine("internal static class LlmTools");
        sb.AppendLine("{");
        sb.AppendLine("    public static global::System.Collections.Generic.IReadOnlyList<global::Application.Service.LLM.LlmAgentTool> CreateTools(global::System.IServiceProvider serviceProvider)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (serviceProvider is null) throw new global::System.ArgumentNullException(nameof(serviceProvider));");
        sb.AppendLine();

        if (tools.Count == 0)
        {
            sb.AppendLine("        return global::System.Array.Empty<global::Application.Service.LLM.LlmAgentTool>();");
        }
        else
        {
            // Resolve each declaring type once
            var declaringTypes = tools
                .Select(t => t.DeclaringType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            var typeVarNames = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var i = 0; i < declaringTypes.Count; i++)
            {
                var type = declaringTypes[i];
                var v = $"svc{i + 1}";
                typeVarNames[type] = v;
                sb.Append("        var ").Append(v).Append(" = (").Append(type).Append(")(serviceProvider.GetService(typeof(").Append(type).Append("))")
                    .Append(" ?? throw new global::System.InvalidOperationException(\"Service not registered: ").Append(type.Replace("\"", "\\\"")).AppendLine("\"));");
            }

            sb.AppendLine();
            sb.AppendLine("        return new global::System.Collections.Generic.List<global::Application.Service.LLM.LlmAgentTool>");
            sb.AppendLine("        {");

            foreach (var t in tools)
            {
                var declaringTypeName = t.DeclaringType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                var varName = typeVarNames[declaringTypeName];
                sb.Append("            global::Application.Service.LLM.LlmAgentTool.Create<")
                    .Append(t.ArgsTypeFullName)
                    .Append(", ")
                    .Append(t.ResultType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                    .AppendLine(">(");

                sb.Append("                ").Append(ToCSharpStringLiteral(t.Name)).AppendLine(",");
                sb.AppendLine("                " + RenderSchema(t.ParametersSchema) + ",");

                // delegate
                sb.Append("                async (args, ct) => ");
                sb.Append(RenderInvocation(varName, t));
                sb.AppendLine(",");

                sb.Append("                ").Append(ToCSharpStringLiteral(t.Description)).AppendLine("),");
            }

            sb.AppendLine("        };");
        }
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("internal static class LlmToolServiceCollectionExtensions");
        sb.AppendLine("{");
        sb.AppendLine("    internal static global::Microsoft.Extensions.DependencyInjection.IServiceCollection AddGeneratedLlmTools(this global::Microsoft.Extensions.DependencyInjection.IServiceCollection services)");
        sb.AppendLine("    {");
        sb.AppendLine("        global::Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddScoped<global::Application.Service.LLM.ILlmToolProvider, __GeneratedLlmToolProvider>(services);");
        sb.AppendLine("        return services;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private sealed class __GeneratedLlmToolProvider : global::Application.Service.LLM.ILlmToolProvider");
        sb.AppendLine("    {");
        sb.AppendLine("        private readonly global::System.IServiceProvider _sp;");
        sb.AppendLine("        public __GeneratedLlmToolProvider(global::System.IServiceProvider sp) => _sp = sp;");
        sb.AppendLine("        public global::System.Collections.Generic.IReadOnlyList<global::Application.Service.LLM.LlmAgentTool> GetTools() => LlmTools.CreateTools(_sp);");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string RenderInvocation(string serviceVarName, GeneratedTool tool)
    {
        var call = new StringBuilder(256);

        var methodName = tool.Method.Name;

        if (tool.MultiParameterAccessors != null && tool.MultiParameterAccessors.Count != 0)
        {
            call.Append(serviceVarName).Append(".").Append(methodName).Append("(");

            for (var i = 0; i < tool.MultiParameterAccessors.Count; i++)
            {
                if (i != 0)
                {
                    call.Append(", ");
                }

                call.Append("args.").Append(tool.MultiParameterAccessors[i]);
            }

            if (tool.HasCancellationToken)
            {
                call.Append(", ct");
            }

            call.Append(")");
        }
        else
        {
            if (tool.HasCancellationToken)
            {
                call.Append(serviceVarName).Append(".").Append(methodName).Append("(args, ct)");
            }
            else
            {
                call.Append(serviceVarName).Append(".").Append(methodName).Append("(args)");
            }
        }

        if (tool.ReturnsAwaitable)
        {
            return "await " + call;
        }

        return "await global::System.Threading.Tasks.Task.FromResult(" + call + ")";
    }

    private static string RenderSchema(JsonSchemaObject schema)
    {
        var sb = new StringBuilder(2048);
        sb.Append("new global::System.Text.Json.Nodes.JsonObject");
        sb.Append("{");
        sb.Append("[\"type\"] = \"object\", ");

        sb.Append("[\"properties\"] = new global::System.Text.Json.Nodes.JsonObject{");
        for (var i = 0; i < schema.Properties.Count; i++)
        {
            var p = schema.Properties[i];
            sb.Append("[")
                .Append(ToCSharpStringLiteral(p.Name))
                .Append("] = ")
                .Append(RenderPropertySchema(p))
                .Append(", ");
        }
        sb.Append("}, ");

        if (schema.Required.Count != 0)
        {
            sb.Append("[\"required\"] = new global::System.Text.Json.Nodes.JsonArray(");
            for (var i = 0; i < schema.Required.Count; i++)
            {
                if (i != 0) sb.Append(", ");
                sb.Append(ToCSharpStringLiteral(schema.Required[i]));
            }
            sb.Append("), ");
        }

        if (schema.AdditionalPropertiesFalse)
        {
            sb.Append("[\"additionalProperties\"] = false, ");
        }

        sb.Append("}");
        return sb.ToString();
    }

    private static string RenderPropertySchema(JsonSchemaProperty p)
    {
        var sb = new StringBuilder(256);
        sb.Append("new global::System.Text.Json.Nodes.JsonObject{");

        sb.Append("[\"type\"] = ").Append(ToCSharpStringLiteral(p.SchemaType switch
        {
            JsonSchemaType.String => "string",
            JsonSchemaType.Boolean => "boolean",
            JsonSchemaType.Integer => "integer",
            JsonSchemaType.Number => "number",
            JsonSchemaType.Array => "array",
            _ => "object"
        })).Append(", ");

        if (!string.IsNullOrWhiteSpace(p.Description))
        {
            sb.Append("[\"description\"] = ").Append(ToCSharpStringLiteral(p.Description)).Append(", ");
        }

        if (p.EnumValues != null && p.EnumValues.Count != 0)
        {
            sb.Append("[\"enum\"] = new global::System.Text.Json.Nodes.JsonArray(");
            for (var i = 0; i < p.EnumValues.Count; i++)
            {
                if (i != 0) sb.Append(", ");
                sb.Append(ToCSharpStringLiteral(p.EnumValues[i]));
            }
            sb.Append("), ");
        }

        sb.Append("}");
        return sb.ToString();
    }

    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name;
        }

        if (name.Length == 1)
        {
            return name.ToLowerInvariant();
        }

        return char.ToLowerInvariant(name[0]) + name.Substring(1);
    }

    private static string ToCSharpStringLiteral(string? value)
    {
        if (value == null)
        {
            return "null";
        }

        return "@\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
