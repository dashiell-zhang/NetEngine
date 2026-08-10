using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace SourceGenerator.Core;

/// <summary>
/// 按 DbContext 为继承自 CD 的实体生成软删除过滤器配置
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class SoftDeleteFilterGenerator : IIncrementalGenerator
{

    /// <summary>
    /// 逻辑删除基类 CD 的完整元数据名称
    /// </summary>
    private const string CdMetadataName = "Repository.Bases.CD";


    /// <summary>
    /// 当软删除实体或其外层类型无法由顶层生成代码访问时抛出的诊断定义
    /// </summary>
    private static readonly DiagnosticDescriptor InaccessibleEntityDescriptor = new(
        id: "SoftDelete001",
        title: "软删除实体类型无法访问",
        messageFormat: "软删除实体 {0} 或其外层类型无法由顶层生成代码访问",
        category: "SoftDeleteFilterGenerator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);


    /// <summary>
    /// 当 DbContext 无法作为生成扩展方法的参数类型时抛出的诊断定义
    /// </summary>
    private static readonly DiagnosticDescriptor UnsupportedDbContextDescriptor = new(
        id: "SoftDelete002",
        title: "DbContext 类型无法用于软删除生成代码",
        messageFormat: "DbContext 类型 {0} 无法由顶层非泛型生成代码引用",
        category: "SoftDeleteFilterGenerator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);


    /// <summary>
    /// 配置增量生成管道：收集继承自 CD 且出现在 DbSet&lt;T&gt; 中的实体，并为其生成软删除过滤器配置代码
    /// </summary>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 编译级获取 CD 类型符号
        var cdSymbol = context.CompilationProvider.Select((c, _) => c.GetTypeByMetadataName(CdMetadataName));

        // 编译级获取 DbContext 与 DbSet 类型符号
        var dbContextSymbols = context.CompilationProvider.Select(static (c, _) => (
            DbContext: c.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.DbContext"),
            DbSet: c.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.DbSet`1")));

        // 获取生成文件固定导入的 EF Core 命名空间
        var modelBuilderSymbol = context.CompilationProvider.Select(static (c, _) => c.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.ModelBuilder"));

        // 语法级枚举工程中声明的所有类
        var classSymbols = context.SyntaxProvider.CreateSyntaxProvider(
            static (node, _) => node is ClassDeclarationSyntax,
            static (syntaxContext, _) =>
            {
                var symbol = syntaxContext.SemanticModel.GetDeclaredSymbol(syntaxContext.Node);
                return symbol as INamedTypeSymbol;
            })
            .Where(static type => type is not null)!
            .Select(static (type, _) => type!);

        // 按 DbContext 保留其直接声明的 DbSet 实体集合
        var dbContextGroups = classSymbols.Combine(dbContextSymbols)
            .Select(static (tuple, _) => DbContextEntityDiscovery.CreateGroup(tuple.Left, tuple.Right.DbContext, tuple.Right.DbSet))
            .Where(static group => group is not null)!
            .Select(static (group, _) => group!)
            .Collect()
            .Select(static (groups, _) => DbContextEntityDiscovery.NormalizeGroups(groups));

        // 过滤出：继承自 CD 且为具体类的类型符号
        var softDeleteTypes = classSymbols.Combine(cdSymbol)
            .Select(static (tuple, _) =>
            {
                var (type, cd) = tuple;
                if (type is null || cd is null)
                    return null;
                if (!type.Locations.Any(l => l.IsInSource))
                    return null;
                if (type.TypeKind != TypeKind.Class || type.IsAbstract)
                    return null;
                if (SymbolEqualityComparer.Default.Equals(type, cd))
                    return null;
                return InheritsFromCd(type, cd) ? type : null;
            })
            .Where(static t => t is not null)!
            .Select(static (t, _) => t!);

        // 收集软删实体 + CD 类型符号 + DbContext 实体分组
        var collected = softDeleteTypes.Collect().Combine(cdSymbol).Combine(dbContextGroups).Combine(modelBuilderSymbol);

        context.RegisterSourceOutput(collected, static (spc, tuple) =>
        {
            var entities = tuple.Left.Left.Left;
            var cd = tuple.Left.Left.Right;
            var dbContextEntityGroups = tuple.Left.Right;
            var modelBuilder = tuple.Right;

            if (cd is null || modelBuilder is null || dbContextEntityGroups.IsDefaultOrEmpty)
                return;

            var softDeleteEntitySet = new HashSet<INamedTypeSymbol>(entities, SymbolEqualityComparer.Default);
            var reportedInaccessibleEntities = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
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

                var groupEntities = ImmutableArray.CreateBuilder<INamedTypeSymbol>();

                foreach (var entity in group.EntityTypes)
                {
                    if (!softDeleteEntitySet.Contains(entity))
                    {
                        continue;
                    }

                    if (GeneratedCodeAccessibility.IsTypeReferenceAccessible(entity))
                    {
                        groupEntities.Add(entity);
                        continue;
                    }

                    if (reportedInaccessibleEntities.Add(entity))
                    {
                        var diagnosticLocation = entity.Locations.FirstOrDefault(static location => location.IsInSource) ?? Location.None;
                        var entityDisplay = entity.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
                        spc.ReportDiagnostic(Diagnostic.Create(InaccessibleEntityDescriptor, diagnosticLocation, entityDisplay));
                    }
                }

                generatedGroups.Add(new DbContextEntityGroup(group.DbContextType, groupEntities.ToImmutable()));
            }

            if (generatedGroups.Count == 0)
                return;

            var source = BuildSource(generatedGroups.ToImmutable(), modelBuilder.ContainingNamespace);
            spc.AddSource("SoftDeleteFilters.g.cs", source);
        });
    }


    /// <summary>
    /// 判断类型是否在继承链上包含 CD
    /// </summary>
    private static bool InheritsFromCd(INamedTypeSymbol type, INamedTypeSymbol cdSymbol)
    {

        for (var current = type; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, cdSymbol))
            {
                return true;
            }
        }

        return false;

    }


    /// <summary>
    /// 生成完整的软删除过滤器配置源码（配置类 + 扩展方法）
    /// </summary>
    /// <param name="groups">按 DbContext 隔离的软删除实体分组</param>
    /// <param name="fixedImportedNamespace">生成文件固定导入的 EF Core 命名空间</param>
    /// <returns>完整生成源码</returns>
    private static string BuildSource(ImmutableArray<DbContextEntityGroup> groups, INamespaceSymbol? fixedImportedNamespace)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using Microsoft.EntityFrameworkCore;");

        var fixedImportedNamespaces = fixedImportedNamespace is null
            ? Array.Empty<INamespaceSymbol>()
            : new[] { fixedImportedNamespace };
        var entities = groups.SelectMany(static group => group.EntityTypes);
        var typeNameResolver = GeneratedEntityTypeNameResolver.Create(entities, fixedImportedNamespaces);
        typeNameResolver.AppendUsingDirectives(sb);

        sb.AppendLine();
        sb.AppendLine("namespace Repository.Database.Generated;");
        sb.AppendLine();

        sb.AppendLine("/// <summary>")
          .AppendLine("/// 提供按 DbContext 隔离的软删除模型配置")
          .AppendLine("/// </summary>")
          .AppendLine("public static class SoftDeleteModelBuilderExtensions")
          .AppendLine("{");

        foreach (var group in groups)
        {
            var contextDisplay = group.DbContextType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            sb.AppendLine()
              .AppendLine("    /// <summary>")
              .AppendLine("    /// 应用当前 DbContext 直接声明实体的软删除过滤器")
              .AppendLine("    /// </summary>")
              .AppendLine("    /// <param name=\"modelBuilder\">当前 EF Core 模型构建器</param>")
              .AppendLine("    /// <param name=\"context\">用于编译期选择配置重载的 DbContext</param>")
              .Append("    public static void ApplySoftDeleteFilters(this ModelBuilder modelBuilder, ")
              .Append(contextDisplay)
              .AppendLine(" context)")
              .AppendLine("    {")
              .AppendLine();

            foreach (var entity in group.EntityTypes)
            {
                var entityDisplay = typeNameResolver.GetTypeName(entity);

                sb.Append("        modelBuilder.Entity<")
                  .Append(entityDisplay)
                  .AppendLine(">().HasQueryFilter(e => e.DeleteTime == null);");
            }

            sb.AppendLine()
              .AppendLine("    }")
              .AppendLine();
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

}
