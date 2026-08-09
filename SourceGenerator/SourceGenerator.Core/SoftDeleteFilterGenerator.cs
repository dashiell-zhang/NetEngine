using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace SourceGenerator.Core;

/// <summary>
/// 为继承自 CD 的实体生成全局软删除过滤器配置
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
    /// 用于对 <see cref="INamedTypeSymbol"/> 做集合去重的比较器
    /// </summary>
    private static readonly IEqualityComparer<INamedTypeSymbol> NamedSymbolComparer = new NamedTypeSymbolEqualityComparer();


    /// <summary>
    /// 配置增量生成管道：收集继承自 CD 且出现在 DbSet&lt;T&gt; 中的实体，并为其生成软删除过滤器配置代码
    /// </summary>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 编译级获取 CD 类型符号
        var cdSymbol = context.CompilationProvider.Select((c, _) => c.GetTypeByMetadataName(CdMetadataName));

        // 编译级收集所有 DbSet<T> 中声明的实体类型
        var dbSetEntities = context.CompilationProvider.Select(static (c, _) => GetDbSetEntityTypes(c));

        // 获取生成文件固定导入的 EF Core 命名空间
        var modelBuilderSymbol = context.CompilationProvider.Select(static (c, _) => c.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.ModelBuilder"));

        // 语法级枚举工程中声明的所有类
        var classSymbols = context.SyntaxProvider.CreateSyntaxProvider(
            static (node, _) => node is ClassDeclarationSyntax,
            static (syntaxContext, _) =>
            {
                var symbol = syntaxContext.SemanticModel.GetDeclaredSymbol(syntaxContext.Node);
                return symbol as INamedTypeSymbol;
            });

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

        // 收集软删实体 + CD 类型符号 + DbSet 声明的实体集合
        var collected = softDeleteTypes.Collect().Combine(cdSymbol).Combine(dbSetEntities).Combine(modelBuilderSymbol);

        context.RegisterSourceOutput(collected, static (spc, tuple) =>
        {
            var entities = tuple.Left.Left.Left;
            var cd = tuple.Left.Left.Right;
            var dbSetTypes = tuple.Left.Right;
            var modelBuilder = tuple.Right;

            if (cd is null || entities.IsDefaultOrEmpty)
                return;

            // 仅对真正出现在 DbSet<T> 中的实体生成过滤器，跳过基础抽象 / 复用类
            var filtered = entities.Where(e => dbSetTypes.Contains(e, SymbolEqualityComparer.Default));

            var candidates = filtered.Distinct(NamedSymbolComparer)
                                     .OrderBy(t => t.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat))
                                     .ToArray();

            var accessibleEntities = new List<INamedTypeSymbol>(candidates.Length);
            foreach (var entity in candidates)
            {
                if (GeneratedCodeAccessibility.IsTypeReferenceAccessible(entity))
                {
                    accessibleEntities.Add(entity);
                    continue;
                }

                var diagnosticLocation = entity.Locations.FirstOrDefault(static location => location.IsInSource) ?? Location.None;
                var entityDisplay = entity.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
                spc.ReportDiagnostic(Diagnostic.Create(InaccessibleEntityDescriptor, diagnosticLocation, entityDisplay));
            }

            var distinct = accessibleEntities.ToArray();

            if (distinct.Length == 0)
                return;

            var source = BuildSource(distinct, modelBuilder?.ContainingNamespace);
            spc.AddSource("SoftDeleteFilters.g.cs", source);
        });
    }


    /// <summary>
    /// 判断类型是否在继承链上包含 CD
    /// </summary>
    private static bool InheritsFromCd(INamedTypeSymbol type, INamedTypeSymbol cdSymbol)
    {
        return InheritsFrom(type, cdSymbol);
    }


    /// <summary>
    /// 生成完整的软删除过滤器配置源码（配置类 + 扩展方法）
    /// </summary>
    /// <param name="entities">需要生成软删除过滤器的实体类型</param>
    /// <param name="fixedImportedNamespace">生成文件固定导入的 EF Core 命名空间</param>
    /// <returns>完整生成源码</returns>
    private static string BuildSource(IReadOnlyList<INamedTypeSymbol> entities, INamespaceSymbol? fixedImportedNamespace)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using Microsoft.EntityFrameworkCore;");

        var fixedImportedNamespaces = fixedImportedNamespace is null
            ? Array.Empty<INamespaceSymbol>()
            : new[] { fixedImportedNamespace };
        var typeNameResolver = GeneratedEntityTypeNameResolver.Create(entities, fixedImportedNamespaces);
        typeNameResolver.AppendUsingDirectives(sb);

        sb.AppendLine();
        sb.AppendLine("namespace Repository.Database.Generated;");
        sb.AppendLine();

        sb.AppendLine("public static class SoftDeleteModelBuilderExtensions")
          .AppendLine("{")
          .AppendLine("    public static void ApplySoftDeleteFilters(this ModelBuilder modelBuilder)")
          .AppendLine("    {");

        foreach (var entity in entities)
        {
            var entityDisplay = typeNameResolver.GetTypeName(entity);

            sb.Append("        modelBuilder.Entity<")
              .Append(entityDisplay)
              .AppendLine(">().HasQueryFilter(e => e.DeleteTime == null);");
        }

        sb.AppendLine("    }")
          .AppendLine("}");

        return sb.ToString();
    }


    /// <summary>
    /// 扫描编译中的所有 DbContext，收集其公开的 DbSet&lt;T&gt; 所引用的实体类型
    /// </summary>
    private static ImmutableHashSet<INamedTypeSymbol> GetDbSetEntityTypes(Compilation compilation)
    {
        var dbContextSymbol = compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.DbContext");
        var dbSetSymbol = compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.DbSet`1");

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
    /// 基于 Roslyn 符号的命名类型比较器，用于去重
    /// </summary>
    private sealed class NamedTypeSymbolEqualityComparer : IEqualityComparer<INamedTypeSymbol>
    {
        public bool Equals(INamedTypeSymbol? x, INamedTypeSymbol? y) => SymbolEqualityComparer.Default.Equals(x, y);

        public int GetHashCode(INamedTypeSymbol obj) => SymbolEqualityComparer.Default.GetHashCode(obj);
    }


    /// <summary>
    /// 判断指定类型是否在继承链上包含指定基类
    /// </summary>
    private static bool InheritsFrom(INamedTypeSymbol type, INamedTypeSymbol baseType)
    {
        for (var t = type; t is not null; t = t.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(t, baseType))
                return true;
        }
        return false;
    }

}
