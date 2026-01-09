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
    /// 增量生成入口 配置对编译对象的扫描与源码输出
    /// </summary>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var symbols = context.CompilationProvider.Select(static (compilation, _) =>
        {
            return new GeneratorSymbols(
                compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.ModelBuilder"),
                compilation.GetTypeByMetadataName(DbContextMetadataName),
                compilation.GetTypeByMetadataName(DbSetMetadataName),
                compilation.GetTypeByMetadataName(ListMetadataName),
                compilation.GetTypeByMetadataName(DictionaryMetadataName));
        });

        // 以 DbContext 为入口增量收集 DbSet<T> 中的实体类型，避免每次改动都扫描所有语法树
        var dbContextCandidates = context.SyntaxProvider.CreateSyntaxProvider(
                static (node, _) => node is ClassDeclarationSyntax cds && cds.BaseList is not null,
                static (ctx, _) => ctx.SemanticModel.GetDeclaredSymbol((ClassDeclarationSyntax)ctx.Node) as INamedTypeSymbol)
            .Where(static t => t is not null)!;

        var dbSetEntities = dbContextCandidates
            .Combine(symbols)
            .Select(static (t, _) => GetDbSetEntityTypesFromDbContext(t.Left!, t.Right.DbContext, t.Right.DbSet))
            .Collect()
            .Select(static (arrays, _) => MergeEntityTypes(arrays));

        // 从 [JsonColumn] 属性本身出发增量收集，避免遍历所有实体属性
        var jsonColumnProperties = context.SyntaxProvider.ForAttributeWithMetadataName(
                JsonColumnAttributeMetadataName,
                static (node, _) => node is PropertyDeclarationSyntax,
                static (ctx, _) => ctx.TargetSymbol as IPropertySymbol)
            .Where(static p => p is not null)!;

        var jsonColumnAnalyses = jsonColumnProperties
            .Combine(dbSetEntities)
            .Combine(symbols)
            .Select(static (t, _) => AnalyzeJsonColumnProperty(t.Left.Left!, t.Left.Right, t.Right))
            .Where(static r => r is not null)!
            .Select(static (r, _) => r!);

        context.RegisterSourceOutput(jsonColumnAnalyses.Collect().Combine(symbols), static (spc, t) =>
        {
            var analyses = t.Left;
            var symbols = t.Right;

            if (symbols.ModelBuilder is null)
                return;

            if (analyses.IsDefaultOrEmpty)
            {
                spc.AddSource("JsonColumnMappings.g.cs", BuildSource(ImmutableArray<JsonEntityConfig>.Empty));
                return;
            }

            var entityMap = new Dictionary<INamedTypeSymbol, ImmutableArray<JsonNavigation>.Builder>(SymbolEqualityComparer.Default);
            var entityNavVisited = new Dictionary<INamedTypeSymbol, HashSet<string>>(SymbolEqualityComparer.Default);

            foreach (var analysis in analyses)
            {
                if (analysis.Diagnostic is not null)
                {
                    spc.ReportDiagnostic(analysis.Diagnostic);
                    continue;
                }

                if (analysis.Navigation is null)
                    continue;

                if (!entityMap.TryGetValue(analysis.EntityType, out var navBuilder))
                {
                    navBuilder = ImmutableArray.CreateBuilder<JsonNavigation>();
                    entityMap.Add(analysis.EntityType, navBuilder);
                    entityNavVisited.Add(analysis.EntityType, new HashSet<string>(StringComparer.Ordinal));
                }

                var visited = entityNavVisited[analysis.EntityType];
                if (visited.Add(analysis.Navigation.PropertyName))
                {
                    navBuilder.Add(analysis.Navigation);
                }
            }

            var configsBuilder = ImmutableArray.CreateBuilder<JsonEntityConfig>(entityMap.Count);
            foreach (var kv in entityMap)
                configsBuilder.Add(new JsonEntityConfig(kv.Key, kv.Value.ToImmutable()));

            var source = BuildSource(configsBuilder.ToImmutable());
            spc.AddSource("JsonColumnMappings.g.cs", source);
        });
    }


    /// <summary>
    /// 分析单个带 [JsonColumn] 的属性是否能生成映射
    /// </summary>
    private static JsonColumnAnalysis? AnalyzeJsonColumnProperty(IPropertySymbol property, ImmutableHashSet<INamedTypeSymbol> dbSetEntities, GeneratorSymbols symbols)
    {
        if (property.ContainingType is not INamedTypeSymbol entityType)
            return null;

        if (!dbSetEntities.Contains(entityType))
            return null;

        var (ownedType, isCollection) = GetOwnedType(property.Type, symbols.List, symbols.Dictionary);
        if (ownedType is null || !IsSupportedJsonColumnRootType(ownedType))
        {
            var diagnostic = Diagnostic.Create(
                UnsupportedTypeDescriptor,
                property.Locations.FirstOrDefault(),
                property.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                property.Type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat));

            return new JsonColumnAnalysis(entityType, null, diagnostic);
        }

        if (TryFindCyclicNesting(ownedType, symbols.List, symbols.Dictionary, out var cycle))
        {
            var diagnostic = Diagnostic.Create(
                CyclicNestingDescriptor,
                cycle.TriggerProperty.Locations.FirstOrDefault() ?? property.Locations.FirstOrDefault(),
                property.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                ownedType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                cycle.Path);

            return new JsonColumnAnalysis(entityType, null, diagnostic);
        }

        return new JsonColumnAnalysis(entityType, new JsonNavigation(property.Name, isCollection), null);
    }


    /// <summary>
    /// 判断 JsonColumn 根属性类型是否可作为 ComplexProperty/ComplexCollection 目标
    /// </summary>
    private static bool IsSupportedJsonColumnRootType(INamedTypeSymbol type)
    {
        // int/string/bool/decimal 等基础类型 + string：Roslyn 会给出 SpecialType
        if (type.SpecialType != SpecialType.None)
            return false;

        // ComplexProperty/ComplexCollection 通常用于引用类型；值类型/枚举这里统一不支持
        return type.TypeKind == TypeKind.Class;
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


    private sealed class CycleInfo
    {
        public CycleInfo(IPropertySymbol triggerProperty, string path)
        {
            TriggerProperty = triggerProperty;
            Path = path;
        }

        public IPropertySymbol TriggerProperty { get; }

        public string Path { get; }
    }


    private static bool TryFindCyclicNesting(INamedTypeSymbol rootType, INamedTypeSymbol? listSymbol, INamedTypeSymbol? dictionarySymbol, out CycleInfo cycle)
    {
        // 仅对复杂类型做循环检测：基础类型/值类型/枚举在前面已被过滤
        var visited = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        var recursionStack = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        var parentEdge = new Dictionary<ITypeSymbol, (ITypeSymbol Parent, IPropertySymbol ViaProperty)>(SymbolEqualityComparer.Default);

        var remainingDepth = 128; // 保护：避免极端情况下递归过深
        CycleInfo? foundCycle = null;

        bool Dfs(INamedTypeSymbol type)
        {
            if (remainingDepth-- <= 0)
                return false;

            visited.Add(type);
            recursionStack.Add(type);

            foreach (var prop in GetAllInstanceProperties(type))
            {
                var next = TryGetNestedComplexType(prop.Type, listSymbol, dictionarySymbol);
                if (next is null)
                    continue;

                if (!visited.Contains(next))
                {
                    parentEdge[next] = (type, prop);
                    if (Dfs(next))
                        return true;
                }
                else if (recursionStack.Contains(next))
                {
                    foundCycle = BuildCycleInfo(type, prop, next, parentEdge);
                    return true;
                }
            }

            recursionStack.Remove(type);
            return false;
        }

        if (Dfs(rootType) && foundCycle is not null)
        {
            cycle = foundCycle;
            return true;
        }

        cycle = null!;
        return false;
    }


    private static CycleInfo BuildCycleInfo(
        INamedTypeSymbol currentType,
        IPropertySymbol triggerProperty,
        INamedTypeSymbol targetType,
        Dictionary<ITypeSymbol, (ITypeSymbol Parent, IPropertySymbol ViaProperty)> parentEdge)
    {
        var edges = new List<(INamedTypeSymbol From, IPropertySymbol Property, INamedTypeSymbol To)>();

        // 回边：currentType --triggerProperty--> targetType
        edges.Add((currentType, triggerProperty, targetType));

        // 沿 parentEdge 追溯 currentType 到 targetType 的 DFS 树路径，构造 targetType -> ... -> currentType
        ITypeSymbol cursor = currentType;
        while (!SymbolEqualityComparer.Default.Equals(cursor, targetType))
        {
            if (!parentEdge.TryGetValue(cursor, out var edge))
                break;

            edges.Add(((INamedTypeSymbol)edge.Parent, edge.ViaProperty, (INamedTypeSymbol)cursor));
            cursor = edge.Parent;
        }

        edges.Reverse();

        var parts = new List<string>(edges.Count + 1);
        foreach (var e in edges)
        {
            parts.Add($"{e.From.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)}.{e.Property.Name}");
        }
        parts.Add(edges[edges.Count - 1].To.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat));

        return new CycleInfo(triggerProperty, string.Join(" -> ", parts));
    }


    private static IEnumerable<IPropertySymbol> GetAllInstanceProperties(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            foreach (var prop in current.GetMembers().OfType<IPropertySymbol>())
            {
                if (prop.IsStatic || prop.IsIndexer)
                    continue;

                yield return prop;
            }
        }
    }


    private static INamedTypeSymbol? TryGetNestedComplexType(ITypeSymbol type, INamedTypeSymbol? listSymbol, INamedTypeSymbol? dictionarySymbol)
    {
        if (type is IArrayTypeSymbol arrayType)
        {
            return TryGetNestedComplexType(arrayType.ElementType, listSymbol, dictionarySymbol);
        }

        if (type is not INamedTypeSymbol namedType)
            return null;

        // List<T>：展开元素类型
        if (listSymbol is not null &&
            SymbolEqualityComparer.Default.Equals(namedType.OriginalDefinition, listSymbol) &&
            namedType.TypeArguments.Length == 1)
        {
            return namedType.TypeArguments[0] as INamedTypeSymbol;
        }

        // Dictionary<TKey, TValue>：当前生成器不支持作为 JsonColumn 根类型，这里也不继续展开，避免深入 BCL
        if (dictionarySymbol is not null &&
            SymbolEqualityComparer.Default.Equals(namedType.OriginalDefinition, dictionarySymbol))
        {
            return null;
        }

        if (!IsSupportedJsonColumnNodeType(namedType))
            return null;

        // 避免深入常见集合实现（除了显式支持的 List<T>）
        if (namedType.ContainingNamespace is { IsGlobalNamespace: false } ns)
        {
            var nsName = ns.ToDisplayString();
            if (nsName.StartsWith("System.Collections", StringComparison.Ordinal))
                return null;
        }

        return namedType;
    }


    private static bool IsSupportedJsonColumnNodeType(INamedTypeSymbol type)
    {
        if (type.SpecialType != SpecialType.None)
            return false;

        return type.TypeKind == TypeKind.Class;
    }


    /// <summary>
    /// 根据收集到的实体配置生成 JsonColumn 映射扩展方法源码
    /// </summary>
    private static string BuildSource(ImmutableArray<JsonEntityConfig> configs)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using Microsoft.EntityFrameworkCore;");

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
            AppendNavigation(sb, navigation, "builder", "            ");
        }

        sb.AppendLine("        });");
        sb.AppendLine();
    }


    /// <summary>
    /// 为单个导航属性生成 ComplexProperty 或 ComplexCollection 配置
    /// Json 列根节点仅调用 ToJson，不展开内部层级
    /// </summary>
    private static void AppendNavigation(StringBuilder sb, JsonNavigation navigation, string builderName, string indent)
    {
        var methodName = navigation.IsCollection ? "ComplexCollection" : "ComplexProperty";
        var lambdaParam = navigation.IsCollection ? "collection" : "complex";

        sb.Append(indent).Append(builderName).Append('.').Append(methodName)
          .Append("(p => p.").Append(EscapeIdentifier(navigation.PropertyName)).Append(", ").Append(lambdaParam).AppendLine(" =>");
        sb.Append(indent).AppendLine("{");
        sb.Append(indent).AppendLine("    " + lambdaParam + ".ToJson();");

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
        public JsonNavigation(string propertyName, bool isCollection)
        {
            PropertyName = propertyName;
            IsCollection = isCollection;
        }

        public string PropertyName { get; }

        public bool IsCollection { get; }
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
    /// 从单个 DbContext 类型（及其基类链）中收集 DbSet&lt;T&gt; 声明的实体类型
    /// </summary>
    private static ImmutableArray<INamedTypeSymbol> GetDbSetEntityTypesFromDbContext(INamedTypeSymbol dbContextType, INamedTypeSymbol? dbContextSymbol, INamedTypeSymbol? dbSetSymbol)
    {
        if (dbContextSymbol is null || dbSetSymbol is null)
            return ImmutableArray<INamedTypeSymbol>.Empty;

        if (!InheritsFrom(dbContextType, dbContextSymbol))
            return ImmutableArray<INamedTypeSymbol>.Empty;

        var builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
        var visited = new HashSet<string>(StringComparer.Ordinal);

        for (var current = dbContextType; current is not null; current = current.BaseType)
        {
            foreach (var prop in current.GetMembers().OfType<IPropertySymbol>())
            {
                if (prop.Type is not INamedTypeSymbol namedType)
                    continue;

                if (!SymbolEqualityComparer.Default.Equals(namedType.OriginalDefinition, dbSetSymbol))
                    continue;

                if (namedType.TypeArguments.Length == 1 && namedType.TypeArguments[0] is INamedTypeSymbol entityType)
                {
                    var key = entityType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    if (visited.Add(key))
                    {
                        builder.Add(entityType);
                    }
                }
            }
        }

        return builder.ToImmutable();
    }


    private static ImmutableHashSet<INamedTypeSymbol> MergeEntityTypes(ImmutableArray<ImmutableArray<INamedTypeSymbol>> entityTypeGroups)
    {
        if (entityTypeGroups.IsDefaultOrEmpty)
            return ImmutableHashSet<INamedTypeSymbol>.Empty;

        var builder = ImmutableHashSet.CreateBuilder<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var group in entityTypeGroups)
        {
            foreach (var entityType in group)
            {
                builder.Add(entityType);
            }
        }
        return builder.ToImmutable();
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
        id: "JsonColumn001",
        title: "JsonColumn 属性类型不受支持",
        messageFormat: "JsonColumn 属性 {0} 的类型 {1} 不受支持，仅支持复杂类型或 List<T>",
        category: "JsonColumnGenerator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);


    /// <summary>
    /// 当 JsonColumn 内部类型存在循环嵌套时抛出的诊断定义
    /// </summary>
    private static readonly DiagnosticDescriptor CyclicNestingDescriptor = new(
        id: "JsonColumn002",
        title: "JsonColumn 类型存在循环嵌套",
        messageFormat: "JsonColumn 属性 {0} 的类型 {1} 存在循环嵌套：{2}",
        category: "JsonColumnGenerator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);


    private sealed class GeneratorSymbols
    {
        public GeneratorSymbols(INamedTypeSymbol? modelBuilder, INamedTypeSymbol? dbContext, INamedTypeSymbol? dbSet, INamedTypeSymbol? list, INamedTypeSymbol? dictionary)
        {
            ModelBuilder = modelBuilder;
            DbContext = dbContext;
            DbSet = dbSet;
            List = list;
            Dictionary = dictionary;
        }

        public INamedTypeSymbol? ModelBuilder { get; }

        public INamedTypeSymbol? DbContext { get; }

        public INamedTypeSymbol? DbSet { get; }

        public INamedTypeSymbol? List { get; }

        public INamedTypeSymbol? Dictionary { get; }
    }


    private sealed class JsonColumnAnalysis
    {
        public JsonColumnAnalysis(INamedTypeSymbol entityType, JsonNavigation? navigation, Diagnostic? diagnostic)
        {
            EntityType = entityType;
            Navigation = navigation;
            Diagnostic = diagnostic;
        }

        public INamedTypeSymbol EntityType { get; }

        public JsonNavigation? Navigation { get; }

        public Diagnostic? Diagnostic { get; }
    }
}
