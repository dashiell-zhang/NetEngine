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
/// 基于 <c>[AesEncrypted]</c> 特性为实体的 string 属性生成 EFCore ValueConverter 配置代码，替代运行时反射扫描
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class AesEncryptedValueConverterGenerator : IIncrementalGenerator
{
    /// <summary>
    /// AesEncrypted 特性的完整元数据名称
    /// </summary>
    private const string AesEncryptedAttributeMetadataName = "Repository.Attributes.AesEncryptedAttribute";

    /// <summary>
    /// AesValueConverter 的完整元数据名称
    /// </summary>
    private const string AesValueConverterMetadataName = "Repository.ValueConverters.AesValueConverter";

    /// <summary>
    /// DbContext 的完整元数据名称
    /// </summary>
    private const string DbContextMetadataName = "Microsoft.EntityFrameworkCore.DbContext";

    /// <summary>
    /// DbSet&lt;T&gt; 的完整元数据名称
    /// </summary>
    private const string DbSetMetadataName = "Microsoft.EntityFrameworkCore.DbSet`1";

    /// <summary>
    /// 增量生成入口：扫描 DbSet&lt;T&gt; 中的实体属性，找到标记了 [AesEncrypted] 的 string 字段并生成 HasConversion 配置
    /// </summary>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var result = context.CompilationProvider.Select((compilation, _) =>
        {
            var aesEncryptedAttribute = compilation.GetTypeByMetadataName(AesEncryptedAttributeMetadataName);
            var aesValueConverterType = compilation.GetTypeByMetadataName(AesValueConverterMetadataName);
            var modelBuilder = compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.ModelBuilder");

            var dbSetEntities = GetDbSetEntityTypes(compilation);

            // 只有当项目同时引用了 EFCore + AesEncryptedAttribute，并且存在 DbSet 实体时才尝试生成
            var canEmit = aesEncryptedAttribute is not null && modelBuilder is not null && dbSetEntities.Count > 0;

            var diagnostics = new List<Diagnostic>();

            // 如果 [AesEncrypted] 存在但找不到转换器，直接报错（否则生成代码会编译失败）
            if (canEmit && aesValueConverterType is null)
            {
                diagnostics.Add(Diagnostic.Create(
                    MissingConverterDescriptor,
                    Location.None,
                    AesValueConverterMetadataName));
                canEmit = false;
            }

            var builder = ImmutableArray.CreateBuilder<EntityEncryptionConfig>();

            if (canEmit)
            {
                foreach (var entity in dbSetEntities)
                {
                    var encryptedProperties = ImmutableArray.CreateBuilder<string>();

                    foreach (var property in EnumerateProperties(entity))
                    {
                        if (!HasAttribute(property, aesEncryptedAttribute!))
                            continue;

                        // 与原反射逻辑保持一致：仅允许 string 字段标记 [AesEncrypted]
                        if (property.Type.SpecialType != SpecialType.System_String)
                        {
                            diagnostics.Add(Diagnostic.Create(
                                NonStringEncryptedPropertyDescriptor,
                                property.Locations.FirstOrDefault(),
                                property.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                                property.Type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)));
                            continue;
                        }

                        encryptedProperties.Add(property.Name);
                    }

                    if (encryptedProperties.Count > 0)
                    {
                        builder.Add(new EntityEncryptionConfig(entity, encryptedProperties.ToImmutable()));
                    }
                }
            }

            return new GeneratorResult(canEmit, builder.ToImmutable(), diagnostics.ToImmutableArray());
        });

        context.RegisterSourceOutput(result, static (spc, result) =>
        {
            if (!result.Diagnostics.IsDefaultOrEmpty)
            {
                foreach (var diagnostic in result.Diagnostics)
                {
                    spc.ReportDiagnostic(diagnostic);
                }

                if (result.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
                    return;
            }

            if (!result.CanEmit)
                return;

            // 即便没有任何标记字段，也生成一个空方法，避免调用端因为方法不存在而编译失败
            var source = BuildSource(result.Configs);
            spc.AddSource("AesEncryptedConverters.g.cs", source);
        });
    }


    /// <summary>
    /// 生成 ApplyAesEncryptedConverters 扩展方法源码
    /// </summary>
    private static string BuildSource(ImmutableArray<EntityEncryptionConfig> configs)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using Microsoft.EntityFrameworkCore;");
        sb.AppendLine("using Repository.ValueConverters;");

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

        sb.AppendLine("public static class AesEncryptedModelBuilderExtensions")
          .AppendLine("{")
          .AppendLine("    public static void ApplyAesEncryptedConverters(this ModelBuilder modelBuilder)")
          .AppendLine("    {");

        foreach (var config in configs.OrderBy(c => c.EntityType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat), StringComparer.Ordinal))
        {
            if (config.EncryptedProperties.IsDefaultOrEmpty || config.EncryptedProperties.Length == 0)
                continue;

            var entityDisplay = config.EntityType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

            sb.Append("        modelBuilder.Entity<").Append(entityDisplay).AppendLine(">(builder =>");
            sb.AppendLine("        {");

            foreach (var propertyName in config.EncryptedProperties.OrderBy(p => p, StringComparer.Ordinal))
            {
                sb.Append("            builder.Property(p => p.")
                  .Append(EscapeIdentifier(propertyName))
                  .AppendLine(").HasConversion(AesValueConverter.aesConverter);");
            }

            sb.AppendLine("        });");
            sb.AppendLine();
        }

        sb.AppendLine("    }")
          .AppendLine("}");

        return sb.ToString();
    }


    /// <summary>
    /// 扫描编译单元中所有继承自 DbContext 的类型，收集其 DbSet&lt;T&gt; 声明的实体类型
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
    /// 枚举类型及其基类上的所有实例属性（去除重复）
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
                    visited.Add(property.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ?? property.Name))
                {
                    yield return property;
                }
            }
        }
    }


    /// <summary>
    /// 判断属性是否标记了指定特性
    /// </summary>
    private static bool HasAttribute(IPropertySymbol property, INamedTypeSymbol attributeSymbol)
    {
        foreach (var attribute in property.GetAttributes())
        {
            if (attribute.AttributeClass is not null &&
                SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeSymbol))
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

    private sealed class EntityEncryptionConfig
    {
        public EntityEncryptionConfig(INamedTypeSymbol entityType, ImmutableArray<string> encryptedProperties)
        {
            EntityType = entityType;
            EncryptedProperties = encryptedProperties;
        }

        public INamedTypeSymbol EntityType { get; }

        public ImmutableArray<string> EncryptedProperties { get; }
    }


    private sealed class GeneratorResult
    {
        public GeneratorResult(bool canEmit, ImmutableArray<EntityEncryptionConfig> configs, ImmutableArray<Diagnostic> diagnostics)
        {
            CanEmit = canEmit;
            Configs = configs;
            Diagnostics = diagnostics;
        }

        public bool CanEmit { get; }

        public ImmutableArray<EntityEncryptionConfig> Configs { get; }

        public ImmutableArray<Diagnostic> Diagnostics { get; }
    }


    /// <summary>
    /// 当 [AesEncrypted] 标记在非 string 字段上时抛出的诊断定义
    /// </summary>
    private static readonly DiagnosticDescriptor NonStringEncryptedPropertyDescriptor = new(
        id: "AES001",
        title: "AesEncrypted 只能用于 string 字段",
        messageFormat: "属性 {0} 标记了 [AesEncrypted]，但其类型为 {1}；仅允许 string 类型使用 AesEncrypted。",
        category: "AesEncryptedGenerator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);


    /// <summary>
    /// 当 AesValueConverter 不存在时抛出的诊断定义
    /// </summary>
    private static readonly DiagnosticDescriptor MissingConverterDescriptor = new(
        id: "AES002",
        title: "缺少 AesValueConverter",
        messageFormat: "无法找到转换器类型 {0}，无法为 [AesEncrypted] 生成 ValueConverter 配置。",
        category: "AesEncryptedGenerator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
