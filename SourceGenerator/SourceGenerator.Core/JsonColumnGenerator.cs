using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace SourceGenerator.Core;

/// <summary>
/// 基于 JsonColumn 特性生成 JSON 列的 ComplexProperty / ComplexCollection 配置，替代运行时反射
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class JsonColumnGenerator : IIncrementalGenerator
{
    /// <summary>
    /// JsonColumn 特性的完整元数据名称
    /// </summary>
    private const string JsonColumnAttributeMetadataName = "Repository.Attributes.JsonColumnAttribute";

    /// <summary>
    /// DbContext 的完整元数据名称
    /// </summary>
    private const string DbContextMetadataName = "Microsoft.EntityFrameworkCore.DbContext";

    /// <summary>
    /// DbSet&lt;T&gt; 的完整元数据名称
    /// </summary>
    private const string DbSetMetadataName = "Microsoft.EntityFrameworkCore.DbSet`1";

    /// <summary>
    /// List&lt;T&gt; 的完整元数据名称
    /// </summary>
    private const string ListMetadataName = "System.Collections.Generic.List`1";

    /// <summary>
    /// Dictionary&lt;TKey,TValue&gt; 的完整元数据名称
    /// </summary>
    private const string DictionaryMetadataName = "System.Collections.Generic.Dictionary`2";

    /// <summary>
    /// AesEncrypted 特性的完整元数据名称
    /// </summary>
    private const string AesEncryptedAttributeMetadataName = "Repository.Attributes.AesEncryptedAttribute";

    /// <summary>
    /// AesValueConverter 的完整元数据名称
    /// </summary>
    private const string AesValueConverterMetadataName = "Repository.ValueConverters.AesValueConverter";

    /// <summary>
    /// 这些类型在 EFCore 中应被视为标量（即便其 SpecialType 为 None），不应在 JSON 拥有者里继续展开为 ComplexProperty
    /// </summary>
    private static readonly ImmutableHashSet<string> EfScalarLikeTypeMetadataNames = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "System.DateTimeOffset",
        "System.Guid",
        "System.TimeSpan",
        "System.DateOnly",
        "System.TimeOnly");

    /// <summary>
    /// 增量生成入口 配置对编译对象的扫描与源码输出
    /// </summary>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var configs = context.CompilationProvider.Select((compilation, _) =>
        {
            var jsonAttribute = compilation.GetTypeByMetadataName(JsonColumnAttributeMetadataName);
            var aesEncryptedAttribute = compilation.GetTypeByMetadataName(AesEncryptedAttributeMetadataName);
            var aesValueConverter = compilation.GetTypeByMetadataName(AesValueConverterMetadataName);
            var modelBuilder = compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.ModelBuilder");
            var dbSetEntities = GetDbSetEntityTypes(compilation);
            var listSymbol = compilation.GetTypeByMetadataName(ListMetadataName);
            var dictionarySymbol = compilation.GetTypeByMetadataName(DictionaryMetadataName);

            // 只有同时存在 JsonColumn 特性 和 EFCore ModelBuilder 且当前编译单元中存在 DbSet 实体时才尝试生成
            var canEmit = jsonAttribute is not null && modelBuilder is not null && dbSetEntities.Count > 0;
            var canEmitAesConversions = aesEncryptedAttribute is not null && aesValueConverter is not null;

            var builder = ImmutableArray.CreateBuilder<JsonEntityConfig>();
            var diagnostics = new List<Diagnostic>();

            foreach (var entity in dbSetEntities)
            {
                var typeConfig = GetJsonTypeConfig(
                    entity,
                    jsonAttribute!,
                    canEmitAesConversions ? aesEncryptedAttribute : null,
                    listSymbol,
                    dictionarySymbol,
                    includeAllChildren: false,
                    path: new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default),
                    diagnostics);

                if (!typeConfig.Navigations.IsDefaultOrEmpty)
                {
                    builder.Add(new JsonEntityConfig(entity, typeConfig.Navigations));
                }
            }

            return new JsonConfigResult(canEmit, builder.ToImmutable(), diagnostics.ToImmutableArray(), canEmitAesConversions);
        });

        context.RegisterSourceOutput(configs, static (spc, configs) =>
        {
            if (!configs.CanEmit)
                return;

            if (!configs.Diagnostics.IsDefaultOrEmpty)
            {
                foreach (var diagnostic in configs.Diagnostics)
                {
                    spc.ReportDiagnostic(diagnostic);
                }

                if (configs.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
                    return;
            }

            var source = BuildSource(configs.Configs, configs.CanEmitAesConversions);
            spc.AddSource("JsonColumnMappings.g.cs", source);
        });
    }


    /// <summary>
    /// 基于实体类型递归收集 JSON 拥有者导航信息
    /// includeAllChildren 为 false 时只接受带 JsonColumn 的属性
    /// 为 true 时表示在 JSON 根对象内部继续向下收集所有导航
    /// </summary>
    private static JsonTypeConfig GetJsonTypeConfig(INamedTypeSymbol type, INamedTypeSymbol jsonAttribute, INamedTypeSymbol? aesEncryptedAttribute, INamedTypeSymbol? listSymbol, INamedTypeSymbol? dictionarySymbol, bool includeAllChildren, HashSet<INamedTypeSymbol> path, List<Diagnostic> diagnostics)
    {
        var builder = ImmutableArray.CreateBuilder<JsonNavigation>();
        path.Add(type);

        var encryptedScalarProperties = ImmutableArray.CreateBuilder<string>();

        foreach (var property in EnumerateProperties(type))
        {
            var hasJsonColumnAttribute = HasJsonColumnAttribute(property, jsonAttribute);
            var isJsonColumn = includeAllChildren || hasJsonColumnAttribute;
            if (!isJsonColumn)
                continue;

            // 仅在 JSON owned graph 内处理加密标记（根实体上的字段由 AesEncryptedValueConverterGenerator 负责）
            if (includeAllChildren && aesEncryptedAttribute is not null && HasAttribute(property, aesEncryptedAttribute))
            {
                // 与原反射逻辑保持一致：仅允许 string 字段标记 [AesEncrypted]
                if (property.Type.SpecialType != SpecialType.System_String)
                {
                    diagnostics.Add(Diagnostic.Create(
                        NonStringEncryptedPropertyDescriptor,
                        property.Locations.FirstOrDefault(),
                        property.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                        property.Type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)));
                }
                else
                {
                    encryptedScalarProperties.Add(property.Name);
                }
            }

            var (ownedType, isCollection) = GetOwnedType(property.Type, listSymbol, dictionarySymbol);
            if (ownedType is null)
            {
                if (hasJsonColumnAttribute)
                {
                    diagnostics.Add(Diagnostic.Create(
                        UnsupportedTypeDescriptor,
                        property.Locations.FirstOrDefault(),
                        property.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                        property.Type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)));
                }
                continue;
            }

            // JSON 列内部的属性可能是标量类型；此时不应生成 ComplexProperty 递归配置
            // 只有复杂类型（可拥有者）才需要继续收集 children
            if (IsScalarLikeJsonPropertyType(ownedType))
            {
                // 根节点（带 JsonColumn 特性）若不是复杂类型，属于不支持的用法：发出诊断提醒
                if (hasJsonColumnAttribute)
                {
                    diagnostics.Add(Diagnostic.Create(
                        UnsupportedTypeDescriptor,
                        property.Locations.FirstOrDefault(),
                        property.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                        property.Type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)));
                }
                continue;
            }

            // 递归类型（如 TreeNode.Children: List<TreeNode>）属于合理用法；此处仅截断展开避免无限递归。
            if (SymbolEqualityComparer.Default.Equals(ownedType, type))
            {
                builder.Add(new JsonNavigation(
                    property.Name,
                    ownedType,
                    isCollection,
                    ImmutableArray<JsonNavigation>.Empty,
                    includeAllChildren ? CollectEncryptedScalarProperties(ownedType, aesEncryptedAttribute, diagnostics) : ImmutableArray<string>.Empty));
                continue;
            }

            if (!path.Add(ownedType))
            {
                diagnostics.Add(Diagnostic.Create(
                    CycleDetectedDescriptor,
                    property.Locations.FirstOrDefault(),
                    string.Join(" -> ", path.Select(t => t.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)).Concat(new[] { ownedType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat) }))));
                continue;
            }

            var childConfig = GetJsonTypeConfig(ownedType, jsonAttribute, aesEncryptedAttribute, listSymbol, dictionarySymbol, includeAllChildren: true, path, diagnostics);
            path.Remove(ownedType);

            builder.Add(new JsonNavigation(property.Name, ownedType, isCollection, childConfig.Navigations, childConfig.EncryptedScalarProperties));
        }

        path.Remove(type);
        return new JsonTypeConfig(builder.ToImmutable(), encryptedScalarProperties.ToImmutable());
    }


    /// <summary>
    /// 当遇到“自引用递归类型”需要截断展开时，仍然需要为该 owned type 收集其直接声明的加密标量字段，
    /// 以便生成的 ComplexProperty/ComplexCollection 能对这些字段正确配置 HasConversion(AesValueConverter)。
    /// </summary>
    /// <remarks>
    /// 该方法只用于 JSON owned graph 内（includeAllChildren=true）的属性扫描；根实体字段的加密由其它生成器负责。
    /// </remarks>
    private static ImmutableArray<string> CollectEncryptedScalarProperties(INamedTypeSymbol type, INamedTypeSymbol? aesEncryptedAttribute, List<Diagnostic> diagnostics)
    {
        if (aesEncryptedAttribute is null)
            return ImmutableArray<string>.Empty;

        var encryptedScalarProperties = ImmutableArray.CreateBuilder<string>();
        foreach (var property in EnumerateProperties(type))
        {
            if (!HasAttribute(property, aesEncryptedAttribute))
                continue;

            if (property.Type.SpecialType != SpecialType.System_String)
            {
                diagnostics.Add(Diagnostic.Create(
                    NonStringEncryptedPropertyDescriptor,
                    property.Locations.FirstOrDefault(),
                    property.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                    property.Type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)));
                continue;
            }

            encryptedScalarProperties.Add(property.Name);
        }

        return encryptedScalarProperties.ToImmutable();
    }


    /// <summary>
    /// 判断某个属性类型是否应被视为“标量/叶子类型”
    /// 这些类型在 EFCore 映射中不会作为 owned/complex 类型展开，因此源生成器也应停止递归
    /// </summary>
    private static bool IsScalarLikeJsonPropertyType(ITypeSymbol type)
    {
        // 处理 Nullable<T>，例如 DateTimeOffset? / Guid? 这种可空标量
        type = UnwrapNullable(type);

        // int/string/bool 等基础类型：Roslyn 会给出 SpecialType，直接视为标量
        if (type.SpecialType != SpecialType.None)
            return true;

        // 枚举：在 EFCore 中通常也是标量存储（底层数值）
        if (type.TypeKind == TypeKind.Enum)
            return true;

        // 其它“常见标量但 SpecialType=None”的类型使用白名单判断
        if (type is INamedTypeSymbol named)
        {
            var metadataName = GetFullyQualifiedMetadataName(named);
            if (EfScalarLikeTypeMetadataNames.Contains(metadataName))
                return true;
        }

        return false;
    }


    /// <summary>
    /// 若类型是 Nullable&lt;T&gt;，则返回其 T；否则原样返回
    /// </summary>
    private static ITypeSymbol UnwrapNullable(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol named &&
            named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T &&
            named.TypeArguments.Length == 1)
        {
            return named.TypeArguments[0];
        }

        return type;
    }


    /// <summary>
    /// 获取类型的完整限定名（不带 global:: 前缀）
    /// 用于与元数据名称白名单进行稳定匹配
    /// </summary>
    private static string GetFullyQualifiedMetadataName(INamedTypeSymbol type)
    {
        // FullyQualifiedFormat 形如：global::System.DateTimeOffset
        var fullName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        const string globalPrefix = "global::";
        // netstandard2.0 目标下避免使用范围运算符/Index/Range，使用 Substring 以兼容旧框架编译
        return fullName.StartsWith(globalPrefix, StringComparison.Ordinal) ? fullName.Substring(globalPrefix.Length) : fullName;
    }


    /// <summary>
    /// 从属性类型解析拥有者类型和是否集合
    /// 目前支持 List&lt;T&gt; 集合类型 与普通引用类型
    /// </summary>
    private static (INamedTypeSymbol? ownedType, bool isCollection) GetOwnedType(ITypeSymbol type, INamedTypeSymbol? listSymbol, INamedTypeSymbol? dictionarySymbol)
    {
        if (listSymbol is not null && type is INamedTypeSymbol named &&
            SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, listSymbol) &&
            named.TypeArguments.Length == 1 &&
            named.TypeArguments[0] is INamedTypeSymbol listElement)
        {
            return (listElement, true);
        }

        if (dictionarySymbol is not null && type is INamedTypeSymbol namedDict &&
            SymbolEqualityComparer.Default.Equals(namedDict.OriginalDefinition, dictionarySymbol))
        {
            return (null, false);
        }

        return type is INamedTypeSymbol namedType ? (namedType, false) : (null, false);
    }


    /// <summary>
    /// 判断属性是否带有 JsonColumn 特性
    /// </summary>
    private static bool HasJsonColumnAttribute(IPropertySymbol property, INamedTypeSymbol jsonAttribute)
    {
        foreach (var attribute in property.GetAttributes())
        {
            var attrClass = attribute.AttributeClass;
            if (attrClass is not null && SymbolEqualityComparer.Default.Equals(attrClass, jsonAttribute))
            {
                return true;
            }
        }

        return false;
    }


    /// <summary>
    /// 枚举类型及其基类上的所有实例属性 去除重复
    /// </summary>
    private static IEnumerable<IPropertySymbol> EnumerateProperties(INamedTypeSymbol type)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);

        for (var current = type; current is not null; current = current.BaseType)
        {
            foreach (var member in current.GetMembers())
            {
                if (member is IPropertySymbol property &&
                    !property.IsStatic &&
                    visited.Add(property.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)))
                {
                    yield return property;
                }
            }
        }
    }


    /// <summary>
    /// 根据收集到的实体配置生成 JsonColumn 映射扩展方法源码
    /// </summary>
    private static string BuildSource(ImmutableArray<JsonEntityConfig> configs, bool canEmitAesConversions)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using Microsoft.EntityFrameworkCore;");

        if (canEmitAesConversions && HasAnyEncryptedScalar(configs))
        {
            sb.AppendLine("using Repository.ValueConverters;");
        }

        var namespaces = new HashSet<string>(StringComparer.Ordinal);
        foreach (var config in configs)
        {
            var ns = config.EntityType.ContainingNamespace?.ToDisplayString();
            if (!string.IsNullOrWhiteSpace(ns))
            {
                namespaces.Add(ns!);
            }
        }

        foreach (var ns in namespaces.OrderBy(n => n, StringComparer.Ordinal))
        {
            sb.Append("using ").Append(ns).AppendLine(";");
        }

        sb.AppendLine();
        sb.AppendLine("namespace Repository.Database.Generated;");
        sb.AppendLine();
        sb.AppendLine("public static class JsonColumnModelBuilderExtensions");
        sb.AppendLine("{");
        sb.AppendLine("    public static void ApplyJsonColumns(this ModelBuilder modelBuilder)");
        sb.AppendLine("    {");

        foreach (var config in configs.OrderBy(c => c.EntityType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat), StringComparer.Ordinal))
        {
            AppendEntityMapping(sb, config);
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }


    /// <summary>
    /// 为单个实体生成 Entity 级 JSON 拥有者配置
    /// </summary>
    private static void AppendEntityMapping(StringBuilder sb, JsonEntityConfig config)
    {
        var entityName = config.EntityType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

        sb.Append("        modelBuilder.Entity<").Append(entityName).AppendLine(">(builder =>");
        sb.AppendLine("        {");

        foreach (var navigation in config.Navigations)
        {
            AppendNavigation(sb, navigation, "builder", "            ", isRoot: true);
        }

        sb.AppendLine("        });");
        sb.AppendLine();
    }


    /// <summary>
    /// 为单个导航属性生成 ComplexProperty 或 ComplexCollection 配置
    /// isRoot 为 true 时表示 Json 列根节点 需要调用 ToJson
    /// </summary>
    private static void AppendNavigation(StringBuilder sb, JsonNavigation navigation, string builderName, string indent, bool isRoot)
    {
        var methodName = navigation.IsCollection ? "ComplexCollection" : "ComplexProperty";
        var lambdaParam = navigation.IsCollection ? "collection" : "complex";

        sb.Append(indent).Append(builderName).Append('.').Append(methodName)
          .Append("(p => p.").Append(EscapeIdentifier(navigation.PropertyName)).Append(", ").Append(lambdaParam).AppendLine(" =>");
        sb.Append(indent).AppendLine("{");

        if (isRoot)
        {
            sb.Append(indent).AppendLine("    " + lambdaParam + ".ToJson();");
        }

        foreach (var encryptedProperty in navigation.EncryptedScalarProperties.OrderBy(p => p, StringComparer.Ordinal))
        {
            sb.Append(indent).Append("    ").Append(lambdaParam).Append(".Property(p => p.")
              .Append(EscapeIdentifier(encryptedProperty))
              .AppendLine(").HasConversion(AesValueConverter.aesConverter);");
        }

        foreach (var child in navigation.Children)
        {
            AppendNavigation(sb, child, lambdaParam, indent + "    ", isRoot: false);
        }

        sb.Append(indent).AppendLine("});");
    }


    private sealed class JsonEntityConfig
    {
        public JsonEntityConfig(INamedTypeSymbol entityType, ImmutableArray<JsonNavigation> navigations)
        {
            EntityType = entityType;
            Navigations = navigations;
        }

        public INamedTypeSymbol EntityType { get; }

        public ImmutableArray<JsonNavigation> Navigations { get; }
    }


    private sealed class JsonNavigation
    {
        public JsonNavigation(string propertyName, INamedTypeSymbol ownedType, bool isCollection, ImmutableArray<JsonNavigation> children, ImmutableArray<string> encryptedScalarProperties)
        {
            PropertyName = propertyName;
            OwnedType = ownedType;
            IsCollection = isCollection;
            Children = children;
            EncryptedScalarProperties = encryptedScalarProperties;
        }

        public string PropertyName { get; }

        public INamedTypeSymbol OwnedType { get; }

        public bool IsCollection { get; }

        public ImmutableArray<JsonNavigation> Children { get; }

        public ImmutableArray<string> EncryptedScalarProperties { get; }
    }


    /// <summary>
    /// Json 映射生成结果 包含是否可生成 标记的实体配置以及诊断信息
    /// </summary>
    private sealed class JsonConfigResult
    {
        public JsonConfigResult(bool canEmit, ImmutableArray<JsonEntityConfig> configs, ImmutableArray<Diagnostic> diagnostics, bool canEmitAesConversions)
        {
            CanEmit = canEmit;
            Configs = configs;
            Diagnostics = diagnostics;
            CanEmitAesConversions = canEmitAesConversions;
        }

        public bool CanEmit { get; }

        public ImmutableArray<JsonEntityConfig> Configs { get; }

        public ImmutableArray<Diagnostic> Diagnostics { get; }

        public bool CanEmitAesConversions { get; }
    }


    private sealed class JsonTypeConfig
    {
        public JsonTypeConfig(ImmutableArray<JsonNavigation> navigations, ImmutableArray<string> encryptedScalarProperties)
        {
            Navigations = navigations;
            EncryptedScalarProperties = encryptedScalarProperties;
        }

        public ImmutableArray<JsonNavigation> Navigations { get; }

        public ImmutableArray<string> EncryptedScalarProperties { get; }
    }


    private static bool HasAnyEncryptedScalar(ImmutableArray<JsonEntityConfig> configs)
    {
        foreach (var config in configs)
        {
            foreach (var navigation in config.Navigations)
            {
                if (HasAnyEncryptedScalar(navigation))
                    return true;
            }
        }

        return false;
    }


    private static bool HasAnyEncryptedScalar(JsonNavigation navigation)
    {
        if (!navigation.EncryptedScalarProperties.IsDefaultOrEmpty && navigation.EncryptedScalarProperties.Length > 0)
            return true;

        foreach (var child in navigation.Children)
        {
            if (HasAnyEncryptedScalar(child))
                return true;
        }

        return false;
    }


    private static bool HasAttribute(IPropertySymbol property, INamedTypeSymbol attributeSymbol)
    {
        foreach (var attribute in property.GetAttributes())
        {
            var attrClass = attribute.AttributeClass;
            if (attrClass is not null && SymbolEqualityComparer.Default.Equals(attrClass, attributeSymbol))
            {
                return true;
            }
        }

        return false;
    }


    private static string EscapeIdentifier(string identifier)
    {
        // 若是关键字/上下文关键字，使用 @ 前缀以避免生成非法代码
        if (SyntaxFacts.GetKeywordKind(identifier) != SyntaxKind.None ||
            SyntaxFacts.GetContextualKeywordKind(identifier) != SyntaxKind.None)
        {
            return "@" + identifier;
        }

        return identifier;
    }


    /// <summary>
    /// 扫描编译单元中所有继承自 DbContext 的类型 收集其 DbSet&lt;T&gt; 声明的实体类型
    /// </summary>
    private static ImmutableHashSet<INamedTypeSymbol> GetDbSetEntityTypes(Compilation compilation)
    {
        var dbContextSymbol = compilation.GetTypeByMetadataName(DbContextMetadataName);
        var dbSetSymbol = compilation.GetTypeByMetadataName(DbSetMetadataName);

        if (dbContextSymbol is null || dbSetSymbol is null)
            return ImmutableHashSet<INamedTypeSymbol>.Empty;

        var result = ImmutableHashSet.CreateBuilder<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        foreach (var tree in compilation.SyntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(tree);
            var root = tree.GetRoot();

            foreach (var classDecl in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (semanticModel.GetDeclaredSymbol(classDecl) is not INamedTypeSymbol typeSymbol)
                    continue;

                if (!InheritsFrom(typeSymbol, dbContextSymbol))
                    continue;

                foreach (var prop in typeSymbol.GetMembers().OfType<IPropertySymbol>())
                {
                    if (prop.Type is not INamedTypeSymbol namedType)
                        continue;

                    if (!SymbolEqualityComparer.Default.Equals(namedType.OriginalDefinition, dbSetSymbol))
                        continue;

                    if (namedType.TypeArguments.Length == 1 && namedType.TypeArguments[0] is INamedTypeSymbol entityType)
                    {
                        result.Add(entityType);
                    }
                }
            }
        }

        return result.ToImmutable();
    }


    /// <summary>
    /// 判断类型是否在继承链上派生自指定基类
    /// </summary>
    private static bool InheritsFrom(INamedTypeSymbol type, INamedTypeSymbol baseType)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
                return true;
        }
        return false;
    }


    /// <summary>
    /// 当 JsonColumn 属性类型不受支持时抛出的诊断定义
    /// </summary>
    private static readonly DiagnosticDescriptor UnsupportedTypeDescriptor = new(
        id: "JSON002",
        title: "JsonColumn 属性类型不受支持",
        messageFormat: "JsonColumn 属性 {0} 的类型 {1} 不受支持，仅支持复杂类型或 List<T>",
        category: "JsonColumnGenerator",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);


    /// <summary>
    /// 当 JsonColumn 导航之间存在循环引用时抛出的诊断定义
    /// messageFormat 中会给出完整类型路径
    /// </summary>
    private static readonly DiagnosticDescriptor CycleDetectedDescriptor = new(
        id: "JSON001",
        title: "JsonColumn 映射存在循环引用",
        messageFormat: "JsonColumn 映射存在循环引用：{0}",
        category: "JsonColumnGenerator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);


    private static readonly DiagnosticDescriptor NonStringEncryptedPropertyDescriptor = new(
        id: "JSON003",
        title: "AesEncrypted 只能用于 string 字段",
        messageFormat: "属性 {0} 标记了 [AesEncrypted]，但其类型为 {1}；仅允许 string 类型使用 AesEncrypted。",
        category: "JsonColumnGenerator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
