using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SourceGenerator.Core;

/// <summary>
/// 为实体映射生成短类型名称并在发生歧义时创建稳定别名
/// </summary>
internal sealed class GeneratedEntityTypeNameResolver
{

    /// <summary>
    /// 两个实体映射生成文件中已经占用的类型名称
    /// </summary>
    private static readonly string[] ReservedTypeNames =
    {
        "ModelBuilder",
        "SoftDeleteModelBuilderExtensions",
        "JsonColumnModelBuilderExtensions"
    };


    /// <summary>
    /// 生成代码中依赖 using 解析的外部类型名称
    /// </summary>
    private static readonly string[] ImportedTypeNames =
    {
        "ModelBuilder"
    };


    /// <summary>
    /// 实体类型到生成代码引用名称的映射
    /// </summary>
    private readonly Dictionary<INamedTypeSymbol, string> typeReferences;


    /// <summary>
    /// 需要生成的普通命名空间导入
    /// </summary>
    private readonly string[] namespaceImports;


    /// <summary>
    /// 需要生成的类型别名及其目标类型
    /// </summary>
    private readonly KeyValuePair<string, string>[] typeAliases;


    /// <summary>
    /// 使用已经解析的名称映射和 using 信息创建解析器
    /// </summary>
    /// <param name="typeReferences">实体类型到生成代码引用名称的映射</param>
    /// <param name="namespaceImports">普通命名空间导入</param>
    /// <param name="typeAliases">类型别名及其目标类型</param>
    private GeneratedEntityTypeNameResolver(Dictionary<INamedTypeSymbol, string> typeReferences, string[] namespaceImports, KeyValuePair<string, string>[] typeAliases)
    {

        this.typeReferences = typeReferences;
        this.namespaceImports = namespaceImports;
        this.typeAliases = typeAliases;

    }


    /// <summary>
    /// 根据实体集合创建短名称和冲突别名解析结果
    /// </summary>
    /// <param name="entityTypes">生成文件中实际使用的实体类型</param>
    /// <returns>可用于输出 using 和实体名称的解析器</returns>
    public static GeneratedEntityTypeNameResolver Create(IEnumerable<INamedTypeSymbol> entityTypes, IEnumerable<INamespaceSymbol>? fixedImportedNamespaces = null)
    {

        var fixedNamespaces = fixedImportedNamespaces?.ToArray() ?? Array.Empty<INamespaceSymbol>();
        var distinctTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var entityType in entityTypes)
        {
            distinctTypes.Add(entityType);
        }

        var orderedTypes = distinctTypes
            .OrderBy(static type => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), StringComparer.Ordinal)
            .ToArray();

        var rootNameCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var entityType in orderedTypes)
        {
            var rootKey = GetRootTypeKey(entityType);
            rootNameCounts[rootKey] = rootNameCounts.TryGetValue(rootKey, out var count) ? count + 1 : 1;
        }

        var reservedNames = new HashSet<string>(ReservedTypeNames, StringComparer.Ordinal);
        foreach (var entityType in orderedTypes)
        {
            reservedNames.Add(GeneratedTypeNameCollisionDetector.GetRootType(entityType).Name);
        }

        var aliasedTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var entityType in orderedTypes)
        {
            var rootType = GeneratedTypeNameCollisionDetector.GetRootType(entityType);
            if (rootNameCounts[GetRootTypeKey(entityType)] > 1
                || ReservedTypeNames.Contains(rootType.Name, StringComparer.Ordinal)
                || entityType.IsGenericType
                || entityType.ContainingNamespace.IsGlobalNamespace)
            {
                aliasedTypes.Add(entityType);
            }
        }

        foreach (var entityType in orderedTypes.Where(entityType => !aliasedTypes.Contains(entityType)))
        {
            var rootType = GeneratedTypeNameCollisionDetector.GetRootType(entityType);
            if (fixedNamespaces.Any(namespaceSymbol => GeneratedTypeNameCollisionDetector.NamespaceContainsRootMember(namespaceSymbol, rootType.Name, rootType.Arity)))
            {
                aliasedTypes.Add(entityType);
            }
        }

        var importedNamespaces = orderedTypes
            .Where(entityType => !aliasedTypes.Contains(entityType))
            .Select(static entityType => entityType.ContainingNamespace)
            .Where(static namespaceSymbol => !IsImplicitlyImportedNamespace(namespaceSymbol))
            .ToArray();

        foreach (var namespaceSymbol in importedNamespaces)
        {
            if (ImportedTypeNames.Any(importedTypeName => NamespaceContainsRootMember(namespaceSymbol, importedTypeName, 0)))
            {
                var namespaceName = namespaceSymbol.ToDisplayString();
                foreach (var entityType in orderedTypes.Where(entityType => string.Equals(entityType.ContainingNamespace.ToDisplayString(), namespaceName, StringComparison.Ordinal)))
                {
                    aliasedTypes.Add(entityType);
                }
            }
        }

        importedNamespaces = orderedTypes
            .Where(entityType => !aliasedTypes.Contains(entityType))
            .Select(static entityType => entityType.ContainingNamespace)
            .Where(static namespaceSymbol => !IsImplicitlyImportedNamespace(namespaceSymbol))
            .ToArray();

        foreach (var entityType in orderedTypes.Where(entityType => !aliasedTypes.Contains(entityType)))
        {
            if (GeneratedTypeNameCollisionDetector.HasConflict(entityType, importedNamespaces, fixedNamespaces))
            {
                aliasedTypes.Add(entityType);
            }
        }

        var references = new Dictionary<INamedTypeSymbol, string>(SymbolEqualityComparer.Default);
        var imports = new HashSet<string>(StringComparer.Ordinal);
        var aliases = new List<KeyValuePair<string, string>>();

        foreach (var entityType in orderedTypes)
        {
            if (!aliasedTypes.Contains(entityType))
            {
                var namespaceName = entityType.ContainingNamespace.ToDisplayString();
                if (!IsImplicitlyImportedNamespace(entityType.ContainingNamespace))
                {
                    imports.Add(namespaceName);
                }

                references.Add(entityType, entityType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
                continue;
            }

            var alias = CreateAlias(entityType, reservedNames);
            var target = entityType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            references.Add(entityType, alias);
            aliases.Add(new KeyValuePair<string, string>(alias, target));
        }

        return new GeneratedEntityTypeNameResolver(
            references,
            imports.OrderBy(static value => value, StringComparer.Ordinal).ToArray(),
            aliases.OrderBy(static pair => pair.Key, StringComparer.Ordinal).ToArray());

    }


    /// <summary>
    /// 判断命名空间是否已经由生成文件固定导入
    /// </summary>
    /// <param name="namespaceSymbol">待判断的命名空间</param>
    /// <returns>已经固定导入时返回 true</returns>
    private static bool IsImplicitlyImportedNamespace(INamespaceSymbol namespaceSymbol)
    {

        return string.Equals(namespaceSymbol.ToDisplayString(), "Microsoft.EntityFrameworkCore", StringComparison.Ordinal);

    }


    /// <summary>
    /// 判断命名空间是否包含会参与短名称解析的类型或子命名空间
    /// </summary>
    /// <param name="namespaceSymbol">待检查的命名空间</param>
    /// <param name="name">成员短名称</param>
    /// <param name="arity">类型泛型参数数量</param>
    /// <returns>存在同名根成员时返回 true</returns>
    private static bool NamespaceContainsRootMember(INamespaceSymbol namespaceSymbol, string name, int arity)
    {

        return GeneratedTypeNameCollisionDetector.NamespaceContainsRootMember(namespaceSymbol, name, arity);

    }


    /// <summary>
    /// 将实体命名空间导入和冲突别名追加到生成源码
    /// </summary>
    /// <param name="source">目标源码构建器</param>
    public void AppendUsingDirectives(StringBuilder source)
    {

        foreach (var namespaceImport in namespaceImports)
        {
            source.Append("using ").Append(namespaceImport).AppendLine(";");
        }

        foreach (var typeAlias in typeAliases)
        {
            source.Append("using ").Append(typeAlias.Key).Append(" = ").Append(typeAlias.Value).AppendLine(";");
        }

    }


    /// <summary>
    /// 获取指定实体在生成代码中的短名称或冲突别名
    /// </summary>
    /// <param name="entityType">待引用的实体类型</param>
    /// <returns>短名称或类型别名</returns>
    public string GetTypeName(INamedTypeSymbol entityType)
    {

        if (typeReferences.TryGetValue(entityType, out var typeName))
        {
            return typeName;
        }

        throw new InvalidOperationException($"实体类型 {entityType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)} 未参与名称解析");

    }


    /// <summary>
    /// 为需要消歧的实体生成可读且不重复的别名
    /// </summary>
    /// <param name="entityType">需要生成别名的实体类型</param>
    /// <param name="reservedNames">当前文件中已经占用的名称</param>
    /// <returns>稳定的类型别名</returns>
    private static string CreateAlias(INamedTypeSymbol entityType, HashSet<string> reservedNames)
    {

        var typeName = BuildTypeChainName(entityType);
        if (reservedNames.Add(typeName))
        {
            return typeName;
        }

        var namespaceParts = entityType.ContainingNamespace.IsGlobalNamespace
            ? Array.Empty<string>()
            : entityType.ContainingNamespace.ToDisplayString().Split('.');

        for (var depth = 1; depth <= namespaceParts.Length; depth++)
        {
            var prefix = string.Concat(namespaceParts.Skip(namespaceParts.Length - depth).Select(SanitizeIdentifierPart));
            var candidate = prefix + typeName;

            if (reservedNames.Add(candidate))
            {
                return candidate;
            }
        }

        var fallbackBase = typeName + "Entity";
        if (reservedNames.Add(fallbackBase))
        {
            return fallbackBase;
        }

        for (var suffix = 2; ; suffix++)
        {
            var candidate = fallbackBase + suffix.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (reservedNames.Add(candidate))
            {
                return candidate;
            }
        }

    }


    /// <summary>
    /// 获取实体引用时最外层类型的名称和泛型参数数量
    /// </summary>
    /// <param name="entityType">实体类型</param>
    /// <returns>可用于识别短名称冲突的键</returns>
    private static string GetRootTypeKey(INamedTypeSymbol entityType)
    {

        var rootType = GeneratedTypeNameCollisionDetector.GetRootType(entityType);
        return rootType.Name + "`" + rootType.Arity.ToString(System.Globalization.CultureInfo.InvariantCulture);

    }


    /// <summary>
    /// 将实体及其包含类型名称组合为别名主体
    /// </summary>
    /// <param name="entityType">实体类型</param>
    /// <returns>由外到内组合的类型名称</returns>
    private static string BuildTypeChainName(INamedTypeSymbol entityType)
    {

        var typeParts = new Stack<string>();
        for (var current = entityType; current is not null; current = current.ContainingType)
        {
            typeParts.Push(SanitizeIdentifierPart(current.Name));
        }

        return string.Concat(typeParts);

    }


    /// <summary>
    /// 将命名空间或类型名称片段转换为适合组合别名的标识符内容
    /// </summary>
    /// <param name="value">原始名称片段</param>
    /// <returns>可用于别名的名称片段</returns>
    private static string SanitizeIdentifierPart(string value)
    {

        var result = new StringBuilder(value.Length + 1);

        foreach (var character in value)
        {
            if (SyntaxFacts.IsIdentifierPartCharacter(character))
            {
                result.Append(character);
            }
        }

        if (result.Length == 0)
        {
            return "Entity";
        }

        if (!SyntaxFacts.IsIdentifierStartCharacter(result[0]))
        {
            result.Insert(0, '_');
        }

        result[0] = char.ToUpperInvariant(result[0]);
        var identifier = result.ToString();

        return SyntaxFacts.GetKeywordKind(identifier) != SyntaxKind.None
               || SyntaxFacts.GetContextualKeywordKind(identifier) != SyntaxKind.None
            ? "Entity" + identifier
            : identifier;

    }

}
