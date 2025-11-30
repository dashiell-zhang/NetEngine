using Microsoft.CodeAnalysis;
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
    private const string JsonColumnAttributeMetadataName = "Repository.Column.Attributes.JsonColumnAttribute";

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
        var configs = context.CompilationProvider.Select((compilation, _) =>
        {
            var jsonAttribute = compilation.GetTypeByMetadataName(JsonColumnAttributeMetadataName);
            var modelBuilder = compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.ModelBuilder");
            var dbSetEntities = GetDbSetEntityTypes(compilation);
            var listSymbol = compilation.GetTypeByMetadataName(ListMetadataName);
            var dictionarySymbol = compilation.GetTypeByMetadataName(DictionaryMetadataName);

            // 只有同时存在 JsonColumn 特性 和 EFCore ModelBuilder 且当前编译单元中存在 DbSet 实体时才尝试生成
            var canEmit = jsonAttribute is not null && modelBuilder is not null && dbSetEntities.Count > 0;

            var builder = ImmutableArray.CreateBuilder<JsonEntityConfig>();
            var diagnostics = new List<Diagnostic>();

            foreach (var entity in dbSetEntities)
            {
                var navigations = GetJsonNavigations(entity, jsonAttribute!, listSymbol, dictionarySymbol, includeAllChildren: false, path: new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default), diagnostics);
                if (!navigations.IsDefaultOrEmpty)
                {
                    builder.Add(new JsonEntityConfig(entity, navigations));
                }
            }

            return new JsonConfigResult(canEmit, builder.ToImmutable(), diagnostics.ToImmutableArray());
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

            var source = BuildSource(configs.Configs);
            spc.AddSource("JsonColumnMappings.g.cs", source);
        });
    }


    /// <summary>
    /// 基于实体类型递归收集 JSON 拥有者导航信息
    /// includeAllChildren 为 false 时只接受带 JsonColumn 的属性
    /// 为 true 时表示在 JSON 根对象内部继续向下收集所有导航
    /// </summary>
    private static ImmutableArray<JsonNavigation> GetJsonNavigations(INamedTypeSymbol type, INamedTypeSymbol jsonAttribute, INamedTypeSymbol? listSymbol, INamedTypeSymbol? dictionarySymbol, bool includeAllChildren, HashSet<INamedTypeSymbol> path, List<Diagnostic> diagnostics)
    {
        var builder = ImmutableArray.CreateBuilder<JsonNavigation>();
        path.Add(type);

        foreach (var property in EnumerateProperties(type))
        {
            var hasJsonColumnAttribute = HasJsonColumnAttribute(property, jsonAttribute);
            var isJsonColumn = includeAllChildren || hasJsonColumnAttribute;
            if (!isJsonColumn)
                continue;

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

            if (ownedType.SpecialType != SpecialType.None)
                continue;

            if (!path.Add(ownedType))
            {
                diagnostics.Add(Diagnostic.Create(
                    CycleDetectedDescriptor,
                    property.Locations.FirstOrDefault(),
                    string.Join(" -> ", path.Select(t => t.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)).Concat(new[] { ownedType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat) }))));
                continue;
            }

            var childNavigations = GetJsonNavigations(ownedType, jsonAttribute, listSymbol, dictionarySymbol, includeAllChildren: true, path, diagnostics);
            path.Remove(ownedType);

            builder.Add(new JsonNavigation(property.Name, ownedType, isCollection, childNavigations));
        }

        path.Remove(type);
        return builder.ToImmutable();
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
          .Append("(\"").Append(navigation.PropertyName).Append("\", ").Append(lambdaParam).AppendLine(" =>");
        sb.Append(indent).AppendLine("{");

        if (isRoot)
        {
            sb.Append(indent).AppendLine("    " + lambdaParam + ".ToJson();");
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
        public JsonNavigation(string propertyName, INamedTypeSymbol ownedType, bool isCollection, ImmutableArray<JsonNavigation> children)
        {
            PropertyName = propertyName;
            OwnedType = ownedType;
            IsCollection = isCollection;
            Children = children;
        }

        public string PropertyName { get; }

        public INamedTypeSymbol OwnedType { get; }

        public bool IsCollection { get; }

        public ImmutableArray<JsonNavigation> Children { get; }
    }


    /// <summary>
    /// Json 映射生成结果 包含是否可生成 标记的实体配置以及诊断信息
    /// </summary>
    private sealed class JsonConfigResult
    {
        public JsonConfigResult(bool canEmit, ImmutableArray<JsonEntityConfig> configs, ImmutableArray<Diagnostic> diagnostics)
        {
            CanEmit = canEmit;
            Configs = configs;
            Diagnostics = diagnostics;
        }

        public bool CanEmit { get; }

        public ImmutableArray<JsonEntityConfig> Configs { get; }

        public ImmutableArray<Diagnostic> Diagnostics { get; }
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
        messageFormat: "JsonColumn 属性 {0} 的类型 {1} 不受支持，仅支持复杂类型或 List<T>。",
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
}
