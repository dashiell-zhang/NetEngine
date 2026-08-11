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
    /// 仅在包含该 DbContext 的项目中输出生成文件 避免在无关项目里产生空的 g.cs
    /// </summary>
    private const string DatabaseContextTypeMetadataName = "Repository.DatabaseContext";

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
    /// NotMapped 特性的完整元数据名称
    /// </summary>
    private const string NotMappedAttributeMetadataName = "System.ComponentModel.DataAnnotations.Schema.NotMappedAttribute";

    /// <summary>
    /// JSON 列内部复杂类型允许的最大递归深度
    /// </summary>
    private const int MaxAnalysisDepth = 128;

    /// <summary>
    /// 增量生成入口 配置对编译对象的扫描与源码输出
    /// </summary>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var isDatabaseProject = context.CompilationProvider.Select(static (compilation, _) =>
        {

            var databaseContextType = compilation.GetTypeByMetadataName(DatabaseContextTypeMetadataName);
            return databaseContextType is not null
                   && SymbolEqualityComparer.Default.Equals(databaseContextType.ContainingAssembly, compilation.Assembly);

        });

        var symbols = context.CompilationProvider.Select(static (compilation, _) =>
        {
            return new GeneratorSymbols(
                compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.ModelBuilder"),
                compilation.GetTypeByMetadataName(DbContextMetadataName),
                compilation.GetTypeByMetadataName(DbSetMetadataName),
                compilation.GetTypeByMetadataName(ListMetadataName),
                compilation.GetTypeByMetadataName(DictionaryMetadataName),
                compilation.GetTypeByMetadataName(NotMappedAttributeMetadataName));
        });

        // 以 DbContext 为入口增量收集直接声明的 DbSet<T> 实体并保留上下文归属
        var dbContextCandidates = context.SyntaxProvider.CreateSyntaxProvider(
                static (node, _) => node is ClassDeclarationSyntax cds && cds.BaseList is not null,
                static (ctx, _) => ctx.SemanticModel.GetDeclaredSymbol((ClassDeclarationSyntax)ctx.Node) as INamedTypeSymbol)
            .Where(static type => type is not null)!
            .Select(static (type, _) => type!);

        var dbContextGroups = dbContextCandidates
            .Combine(symbols)
            .Select(static (tuple, _) => DbContextEntityDiscovery.CreateGroup(tuple.Left, tuple.Right.DbContext, tuple.Right.DbSet))
            .Where(static group => group is not null)!
            .Select(static (group, _) => group!)
            .Collect()
            .Select(static (groups, _) => DbContextEntityDiscovery.NormalizeGroups(groups));

        // 从 [JsonColumn] 属性本身出发增量收集，避免遍历所有实体属性
        var jsonColumnProperties = context.SyntaxProvider.ForAttributeWithMetadataName(
                JsonColumnAttributeMetadataName,
                static (node, _) => node is PropertyDeclarationSyntax or IndexerDeclarationSyntax,
                static (ctx, _) => ctx.TargetSymbol as IPropertySymbol)
            .Where(static p => p is not null)!;

        var jsonColumnAnalyses = jsonColumnProperties
            .Combine(dbContextGroups)
            .Combine(symbols)
            .Select(static (t, _) => AnalyzeJsonColumnProperty(t.Left.Left!, t.Left.Right, t.Right))
            .Where(static r => r is not null)!
            .Select(static (r, _) => r!);

        context.RegisterSourceOutput(jsonColumnAnalyses.Collect().Combine(dbContextGroups).Combine(symbols).Combine(isDatabaseProject), static (spc, t) =>
        {

            if (!t.Right)
                return;

            var analyses = t.Left.Left.Left;
            var dbContextEntityGroups = t.Left.Left.Right;
            var symbols = t.Left.Right;

            if (symbols.ModelBuilder is null || dbContextEntityGroups.IsDefaultOrEmpty)
                return;

            var generatedGroups = ImmutableArray.CreateBuilder<DbContextEntityGroup>(dbContextEntityGroups.Length);
            foreach (var group in dbContextEntityGroups)
            {
                if (!DbContextEntityDiscovery.CanReferenceContextType(group.DbContextType))
                {
                    var contextLocation = group.DbContextType.Locations.FirstOrDefault(static location => location.IsInSource) ?? Location.None;
                    var contextDisplay = group.DbContextType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
                    spc.ReportDiagnostic(Diagnostic.Create(UnsupportedDbContextDescriptor, contextLocation, contextDisplay));
                    continue;
                }

                generatedGroups.Add(group);
            }

            if (generatedGroups.Count == 0)
                return;

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

            var source = BuildSource(configsBuilder.ToImmutable(), generatedGroups.ToImmutable(), symbols.ModelBuilder.ContainingNamespace);
            spc.AddSource("JsonColumnMappings.g.cs", source);
        });
    }


    /// <summary>
    /// 分析单个带 [JsonColumn] 的属性是否能生成映射
    /// </summary>
    /// <param name="property">带 JsonColumn 特性的属性</param>
    /// <param name="dbContextGroups">按 DbContext 隔离的实体分组</param>
    /// <param name="symbols">生成器使用的框架类型符号</param>
    /// <returns>属性分析结果，不属于 DbSet 实体时返回 null</returns>
    private static JsonColumnAnalysis? AnalyzeJsonColumnProperty(IPropertySymbol property, ImmutableArray<DbContextEntityGroup> dbContextGroups, GeneratorSymbols symbols)
    {
        if (property.ContainingType is not INamedTypeSymbol entityType)
            return null;

        if (!DbContextEntityDiscovery.ContainsEntity(dbContextGroups, entityType))
            return null;

        if (!IsTypeAccessibleFromGeneratedCode(entityType))
        {
            var diagnostic = Diagnostic.Create(
                InaccessibleEntityTypeDescriptor,
                entityType.Locations.FirstOrDefault() ?? property.Locations.FirstOrDefault(),
                property.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                entityType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat));

            return new JsonColumnAnalysis(entityType, null, diagnostic);
        }

        var unsupportedPropertyReason = GetUnsupportedPropertyReason(property);
        if (unsupportedPropertyReason is not null)
        {
            var diagnostic = Diagnostic.Create(
                UnsupportedPropertyDescriptor,
                property.Locations.FirstOrDefault(),
                property.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                unsupportedPropertyReason);

            return new JsonColumnAnalysis(entityType, null, diagnostic);
        }

        if (!IsAccessibleFromGeneratedCode(property.DeclaredAccessibility))
        {
            var diagnostic = Diagnostic.Create(
                InaccessiblePropertyDescriptor,
                property.Locations.FirstOrDefault(),
                property.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                $"属性访问级别 {GetAccessibilityText(property.DeclaredAccessibility)} 不受支持");

            return new JsonColumnAnalysis(entityType, null, diagnostic);
        }

        if (property.GetMethod is not null && !IsAccessibleFromGeneratedCode(property.GetMethod.DeclaredAccessibility))
        {
            var diagnostic = Diagnostic.Create(
                InaccessiblePropertyDescriptor,
                property.GetMethod.Locations.FirstOrDefault() ?? property.Locations.FirstOrDefault(),
                property.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                $"getter 访问级别 {GetAccessibilityText(property.GetMethod.DeclaredAccessibility)} 不受支持");

            return new JsonColumnAnalysis(entityType, null, diagnostic);
        }

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

        var nestingAnalysis = AnalyzeCyclicNesting(ownedType, symbols.List, symbols.Dictionary, symbols.NotMappedAttribute);
        if (nestingAnalysis.Status == NestingAnalysisStatus.Cycle)
        {
            var diagnostic = Diagnostic.Create(
                CyclicNestingDescriptor,
                nestingAnalysis.TriggerProperty?.Locations.FirstOrDefault() ?? property.Locations.FirstOrDefault(),
                property.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                ownedType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                nestingAnalysis.Path);

            return new JsonColumnAnalysis(entityType, null, diagnostic);
        }

        if (nestingAnalysis.Status == NestingAnalysisStatus.DepthExceeded)
        {
            var diagnostic = Diagnostic.Create(
                AnalysisDepthExceededDescriptor,
                nestingAnalysis.TriggerProperty?.Locations.FirstOrDefault() ?? property.Locations.FirstOrDefault(),
                property.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                ownedType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                MaxAnalysisDepth,
                nestingAnalysis.Path);

            return new JsonColumnAnalysis(entityType, null, diagnostic);
        }

        return new JsonColumnAnalysis(entityType, new JsonNavigation(property.Name, isCollection), null);
    }


    /// <summary>
    /// 判断实体及其所有外层类型能否由同程序集的顶层生成代码访问
    /// </summary>
    /// <param name="type">待检查的实体类型</param>
    /// <returns>实体类型可访问时返回 true</returns>
    private static bool IsTypeAccessibleFromGeneratedCode(INamedTypeSymbol type)
    {

        for (var current = type; current is not null; current = current.ContainingType)
        {
            if (current.IsFileLocal || !IsAccessibleFromGeneratedCode(current.DeclaredAccessibility))
                return false;
        }

        return true;

    }


    /// <summary>
    /// 获取无法生成属性访问表达式的声明形态原因
    /// </summary>
    /// <param name="property">待检查的 JsonColumn 属性</param>
    /// <returns>不支持原因，属性形态合法时返回 null</returns>
    private static string? GetUnsupportedPropertyReason(IPropertySymbol property)
    {

        if (property.IsStatic)
            return "静态属性不受支持";

        if (property.IsIndexer)
            return "索引器属性不受支持";

        if (!property.ExplicitInterfaceImplementations.IsDefaultOrEmpty)
            return "显式接口实现属性不受支持";

        if (property.RefKind != RefKind.None)
            return "ref 返回属性不受支持";

        if (property.GetMethod is null)
            return "属性必须声明 getter";

        return null;

    }


    /// <summary>
    /// 判断访问级别能否通过同程序集且不依赖继承的生成代码访问
    /// </summary>
    /// <param name="accessibility">待检查的访问级别</param>
    /// <returns>生成代码可以直接访问时返回 true</returns>
    private static bool IsAccessibleFromGeneratedCode(Accessibility accessibility)
    {

        return accessibility is Accessibility.Public
               or Accessibility.Internal
               or Accessibility.ProtectedOrInternal;

    }


    /// <summary>
    /// 获取用于诊断消息的访问级别文本
    /// </summary>
    /// <param name="accessibility">访问级别</param>
    /// <returns>C# 访问修饰符文本</returns>
    private static string GetAccessibilityText(Accessibility accessibility)
    {

        return accessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Internal => "internal",
            Accessibility.Protected => "protected",
            Accessibility.Private => "private",
            Accessibility.ProtectedOrInternal => "protected internal",
            Accessibility.ProtectedAndInternal => "private protected",
            _ => accessibility.ToString()
        };

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


    /// <summary>
    /// 表示 JSON 列内部复杂类型的分析结果
    /// </summary>
    private enum NestingAnalysisStatus
    {

        None,
        Cycle,
        DepthExceeded

    }


    /// <summary>
    /// 分析 JSON 列内部复杂类型是否存在循环或超过递归深度限制
    /// </summary>
    /// <param name="rootType">JSON 列根复杂类型</param>
    /// <param name="listSymbol">List 泛型类型符号</param>
    /// <param name="dictionarySymbol">Dictionary 泛型类型符号</param>
    /// <param name="notMappedAttributeSymbol">NotMapped 特性类型符号</param>
    /// <returns>分析状态、触发属性和分析路径</returns>
    private static (NestingAnalysisStatus Status, IPropertySymbol? TriggerProperty, string? Path) AnalyzeCyclicNesting(INamedTypeSymbol rootType, INamedTypeSymbol? listSymbol, INamedTypeSymbol? dictionarySymbol, INamedTypeSymbol? notMappedAttributeSymbol)
    {

        var visited = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        var recursionStack = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        var parentEdge = new Dictionary<ITypeSymbol, (ITypeSymbol Parent, IPropertySymbol ViaProperty)>(SymbolEqualityComparer.Default);

        IPropertySymbol? triggerProperty = null;
        string? analysisPath = null;

        NestingAnalysisStatus Dfs(INamedTypeSymbol type, int depth)
        {

            visited.Add(type);
            recursionStack.Add(type);

            foreach (var prop in GetAllMappedProperties(type, notMappedAttributeSymbol))
            {
                var next = TryGetNestedComplexType(prop.Type, listSymbol, dictionarySymbol);
                if (next is null)
                    continue;

                if (recursionStack.Contains(next))
                {
                    triggerProperty = prop;
                    analysisPath = BuildCyclePath(type, prop, next, parentEdge);
                    return NestingAnalysisStatus.Cycle;
                }

                if (visited.Contains(next))
                    continue;

                if (depth >= MaxAnalysisDepth)
                {
                    triggerProperty = prop;
                    analysisPath = BuildDepthLimitPath(type, prop, next, parentEdge);
                    return NestingAnalysisStatus.DepthExceeded;
                }

                parentEdge[next] = (type, prop);
                var nestedStatus = Dfs(next, depth + 1);
                if (nestedStatus != NestingAnalysisStatus.None)
                    return nestedStatus;
            }

            recursionStack.Remove(type);
            return NestingAnalysisStatus.None;

        }

        var status = Dfs(rootType, 0);
        return (status, triggerProperty, analysisPath);

    }


    /// <summary>
    /// 构造形成循环的属性路径
    /// </summary>
    /// <param name="currentType">当前分析类型</param>
    /// <param name="triggerProperty">形成回环的属性</param>
    /// <param name="targetType">回环指向的祖先类型</param>
    /// <param name="parentEdge">类型与父级属性的访问关系</param>
    /// <returns>可用于诊断消息的循环路径</returns>
    private static string BuildCyclePath(INamedTypeSymbol currentType, IPropertySymbol triggerProperty, INamedTypeSymbol targetType, Dictionary<ITypeSymbol, (ITypeSymbol Parent, IPropertySymbol ViaProperty)> parentEdge)
    {

        var edges = new List<(INamedTypeSymbol From, IPropertySymbol Property, INamedTypeSymbol To)>
        {
            (currentType, triggerProperty, targetType)
        };

        ITypeSymbol cursor = currentType;
        while (!SymbolEqualityComparer.Default.Equals(cursor, targetType))
        {
            if (!parentEdge.TryGetValue(cursor, out var edge))
                break;

            edges.Add(((INamedTypeSymbol)edge.Parent, edge.ViaProperty, (INamedTypeSymbol)cursor));
            cursor = edge.Parent;
        }

        edges.Reverse();

        return FormatNestingPath(edges);

    }


    /// <summary>
    /// 构造达到分析深度限制时的完整属性路径
    /// </summary>
    /// <param name="currentType">当前分析类型</param>
    /// <param name="triggerProperty">触发深度限制的属性</param>
    /// <param name="targetType">准备继续分析的下一层类型</param>
    /// <param name="parentEdge">类型与父级属性的访问关系</param>
    /// <returns>从根类型到下一层类型的分析路径</returns>
    private static string BuildDepthLimitPath(INamedTypeSymbol currentType, IPropertySymbol triggerProperty, INamedTypeSymbol targetType, Dictionary<ITypeSymbol, (ITypeSymbol Parent, IPropertySymbol ViaProperty)> parentEdge)
    {

        var edges = new List<(INamedTypeSymbol From, IPropertySymbol Property, INamedTypeSymbol To)>
        {
            (currentType, triggerProperty, targetType)
        };

        ITypeSymbol cursor = currentType;
        while (parentEdge.TryGetValue(cursor, out var edge))
        {
            edges.Add(((INamedTypeSymbol)edge.Parent, edge.ViaProperty, (INamedTypeSymbol)cursor));
            cursor = edge.Parent;
        }

        edges.Reverse();

        return FormatNestingPath(edges);

    }


    /// <summary>
    /// 将类型和属性访问边格式化为诊断路径
    /// </summary>
    /// <param name="edges">按访问顺序排列的类型与属性关系</param>
    /// <returns>可读的属性访问路径</returns>
    private static string FormatNestingPath(List<(INamedTypeSymbol From, IPropertySymbol Property, INamedTypeSymbol To)> edges)
    {

        var parts = new List<string>(edges.Count + 1);
        foreach (var edge in edges)
        {
            parts.Add($"{edge.From.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)}.{edge.Property.Name}");
        }

        parts.Add(edges[edges.Count - 1].To.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat));

        return string.Join(" -> ", parts);

    }


    /// <summary>
    /// 枚举当前项目约定下参与 EF Core JSON 映射的属性
    /// </summary>
    /// <param name="type">待分析类型</param>
    /// <param name="notMappedAttributeSymbol">NotMapped 特性类型符号</param>
    /// <returns>需要继续分析的属性集合</returns>
    private static IEnumerable<IPropertySymbol> GetAllMappedProperties(INamedTypeSymbol type, INamedTypeSymbol? notMappedAttributeSymbol)
    {

        for (var current = type; current is not null; current = current.BaseType)
        {
            foreach (var property in current.GetMembers().OfType<IPropertySymbol>())
            {
                if (IsMappedPropertyCandidate(property, notMappedAttributeSymbol))
                    yield return property;
            }
        }

    }


    /// <summary>
    /// 判断属性是否符合当前项目的 EF Core JSON 映射约定
    /// </summary>
    /// <param name="property">待判断属性</param>
    /// <param name="notMappedAttributeSymbol">NotMapped 特性类型符号</param>
    /// <returns>属性应参与循环检测时返回 true</returns>
    private static bool IsMappedPropertyCandidate(IPropertySymbol property, INamedTypeSymbol? notMappedAttributeSymbol)
    {

        if (property.IsStatic || property.IsIndexer || property.DeclaredAccessibility != Accessibility.Public)
            return false;

        if (property.GetMethod is null || property.SetMethod is null)
            return false;

        if (notMappedAttributeSymbol is null)
            return true;

        return !property.GetAttributes().Any(attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, notMappedAttributeSymbol));

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
    /// <param name="configs">需要生成 JSON 列映射的实体配置</param>
    /// <param name="groups">按 DbContext 隔离的实体分组</param>
    /// <param name="fixedImportedNamespace">生成文件固定导入的 EF Core 命名空间</param>
    /// <returns>完整生成源码</returns>
    private static string BuildSource(ImmutableArray<JsonEntityConfig> configs, ImmutableArray<DbContextEntityGroup> groups, INamespaceSymbol? fixedImportedNamespace)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using Microsoft.EntityFrameworkCore;");

        var fixedImportedNamespaces = fixedImportedNamespace is null
            ? Array.Empty<INamespaceSymbol>()
            : new[] { fixedImportedNamespace };
        var typeNameResolver = GeneratedEntityTypeNameResolver.Create(configs.Select(static config => config.EntityType), fixedImportedNamespaces);
        typeNameResolver.AppendUsingDirectives(sb);

        sb.AppendLine();
        sb.AppendLine("namespace Repository.Database.Generated;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// 提供按 DbContext 隔离的 JSON 列模型配置");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static class JsonColumnModelBuilderExtensions");
        sb.AppendLine("{");

        var configMap = new Dictionary<INamedTypeSymbol, JsonEntityConfig>(SymbolEqualityComparer.Default);
        foreach (var config in configs)
        {
            configMap[config.EntityType] = config;
        }

        foreach (var group in groups)
        {
            var contextDisplay = group.DbContextType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            sb.AppendLine();
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// 应用当前 DbContext 直接声明实体的 JSON 列映射");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    /// <param name=\"modelBuilder\">当前 EF Core 模型构建器</param>");
            sb.AppendLine("    /// <param name=\"context\">用于编译期选择配置重载的 DbContext</param>");
            sb.Append("    public static void ApplyJsonColumns(this ModelBuilder modelBuilder, ").Append(contextDisplay).AppendLine(" context)");
            sb.AppendLine("    {");
            sb.AppendLine();

            foreach (var entityType in group.EntityTypes)
            {
                if (configMap.TryGetValue(entityType, out var config))
                {
                    AppendEntityMapping(sb, config, typeNameResolver);
                }
            }

            sb.AppendLine("    }");
            sb.AppendLine();
        }

        sb.AppendLine("}");

        return sb.ToString();
    }


    /// <summary>
    /// 为单个实体生成 Entity 级 JSON 拥有者配置
    /// </summary>
    /// <param name="sb">目标源码构建器</param>
    /// <param name="config">当前实体的 JSON 列配置</param>
    /// <param name="typeNameResolver">实体类型名称解析器</param>
    private static void AppendEntityMapping(StringBuilder sb, JsonEntityConfig config, GeneratedEntityTypeNameResolver typeNameResolver)
    {

        var entityName = typeNameResolver.GetTypeName(config.EntityType);

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


    /// <summary>
    /// 当 JsonColumn 所属实体或外层类型无法被生成代码访问时抛出的诊断定义
    /// </summary>
    private static readonly DiagnosticDescriptor InaccessibleEntityTypeDescriptor = new(
        id: "JsonColumn003",
        title: "JsonColumn 实体类型无法访问",
        messageFormat: "JsonColumn 属性 {0} 所属的实体类型 {1} 或其外层类型无法被生成代码访问",
        category: "JsonColumnGenerator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);


    /// <summary>
    /// 当 JsonColumn 属性声明形态无法生成访问表达式时抛出的诊断定义
    /// </summary>
    private static readonly DiagnosticDescriptor UnsupportedPropertyDescriptor = new(
        id: "JsonColumn004",
        title: "JsonColumn 属性声明不受支持",
        messageFormat: "JsonColumn 属性 {0} 无法生成映射：{1}",
        category: "JsonColumnGenerator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);


    /// <summary>
    /// 当 JsonColumn 属性或 getter 无法被生成代码访问时抛出的诊断定义
    /// </summary>
    private static readonly DiagnosticDescriptor InaccessiblePropertyDescriptor = new(
        id: "JsonColumn005",
        title: "JsonColumn 属性无法访问",
        messageFormat: "JsonColumn 属性 {0} 无法被生成代码访问：{1}",
        category: "JsonColumnGenerator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);


    /// <summary>
    /// 当 DbContext 无法作为生成扩展方法的参数类型时抛出的诊断定义
    /// </summary>
    private static readonly DiagnosticDescriptor UnsupportedDbContextDescriptor = new(
        id: "JsonColumn006",
        title: "DbContext 类型无法用于 JSON 列生成代码",
        messageFormat: "DbContext 类型 {0} 无法由顶层非泛型生成代码引用",
        category: "JsonColumnGenerator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);


    /// <summary>
    /// 当 JsonColumn 内部类型超过分析深度限制时抛出的诊断定义
    /// </summary>
    private static readonly DiagnosticDescriptor AnalysisDepthExceededDescriptor = new(
        id: "JsonColumn007",
        title: "JsonColumn 类型分析超过深度限制",
        messageFormat: "JsonColumn 属性 {0} 的类型 {1} 分析深度超过限制 {2}：{3}",
        category: "JsonColumnGenerator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);


    /// <summary>
    /// 保存生成器分析所需的框架类型符号
    /// </summary>
    private sealed class GeneratorSymbols
    {

        /// <summary>
        /// 创建生成器分析所需的框架类型符号集合
        /// </summary>
        /// <param name="modelBuilder">ModelBuilder 类型符号</param>
        /// <param name="dbContext">DbContext 类型符号</param>
        /// <param name="dbSet">DbSet 泛型类型符号</param>
        /// <param name="list">List 泛型类型符号</param>
        /// <param name="dictionary">Dictionary 泛型类型符号</param>
        /// <param name="notMappedAttribute">NotMapped 特性类型符号</param>
        public GeneratorSymbols(INamedTypeSymbol? modelBuilder, INamedTypeSymbol? dbContext, INamedTypeSymbol? dbSet, INamedTypeSymbol? list, INamedTypeSymbol? dictionary, INamedTypeSymbol? notMappedAttribute)
        {

            ModelBuilder = modelBuilder;
            DbContext = dbContext;
            DbSet = dbSet;
            List = list;
            Dictionary = dictionary;
            NotMappedAttribute = notMappedAttribute;

        }

        public INamedTypeSymbol? ModelBuilder { get; }

        public INamedTypeSymbol? DbContext { get; }

        public INamedTypeSymbol? DbSet { get; }

        public INamedTypeSymbol? List { get; }

        public INamedTypeSymbol? Dictionary { get; }

        /// <summary>
        /// NotMapped 特性类型符号
        /// </summary>
        public INamedTypeSymbol? NotMappedAttribute { get; }

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
