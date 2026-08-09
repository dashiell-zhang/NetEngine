using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SourceGenerator.Core;

/// <summary>
/// 检测生成代码导入命名空间后可能产生的根类型名称冲突
/// </summary>
internal static class GeneratedTypeNameCollisionDetector
{

    /// <summary>
    /// 判断指定类型及其泛型组成类型是否会在导入命名空间之间产生歧义
    /// </summary>
    /// <param name="type">待引用的类型</param>
    /// <param name="importedNamespaces">生成文件导入的业务命名空间</param>
    /// <param name="fixedNamespaces">生成文件固定导入的框架命名空间</param>
    /// <returns>任一组成类型存在两个或更多候选根成员时返回 true</returns>
    public static bool HasConflict(INamedTypeSymbol type, IEnumerable<INamespaceSymbol> importedNamespaces, IEnumerable<INamespaceSymbol> fixedNamespaces)
    {

        var namespaces = importedNamespaces.Concat(fixedNamespaces).ToArray();
        return HasConflictCore(type, namespaces);

    }


    /// <summary>
    /// 判断指定根类型名称是否会在导入命名空间之间产生歧义
    /// </summary>
    /// <param name="type">待引用的类型</param>
    /// <param name="importedNamespaces">生成文件导入的业务命名空间</param>
    /// <param name="fixedNamespaces">生成文件固定导入的框架命名空间</param>
    /// <returns>根类型存在两个或更多候选成员时返回 true</returns>
    public static bool HasRootConflict(INamedTypeSymbol type, IEnumerable<INamespaceSymbol> importedNamespaces, IEnumerable<INamespaceSymbol> fixedNamespaces)
    {

        var rootType = GetRootType(type);
        return CountMatchingNamespaces(rootType.Name, rootType.Arity, importedNamespaces.Concat(fixedNamespaces)) > 1;

    }


    /// <summary>
    /// 判断尚未加入编译的生成类型名称是否会与导入命名空间成员冲突
    /// </summary>
    /// <param name="name">生成类型的根名称</param>
    /// <param name="arity">生成类型的泛型参数数量</param>
    /// <param name="ownNamespace">生成类型所在命名空间</param>
    /// <param name="importedNamespaces">生成文件导入的业务命名空间</param>
    /// <param name="fixedNamespaces">生成文件固定导入的框架命名空间</param>
    /// <returns>生成类型加入自身命名空间后会产生歧义时返回 true</returns>
    public static bool HasGeneratedTypeConflict(string name, int arity, INamespaceSymbol ownNamespace, IEnumerable<INamespaceSymbol> importedNamespaces, IEnumerable<INamespaceSymbol> fixedNamespaces)
    {

        var namespaceGroups = importedNamespaces
            .Concat(fixedNamespaces)
            .GroupBy(static namespaceSymbol => namespaceSymbol.ToDisplayString(), StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.ToArray(), StringComparer.Ordinal);
        var ownNamespaceName = ownNamespace.ToDisplayString();

        if (!namespaceGroups.TryGetValue(ownNamespaceName, out var ownNamespaceSymbols))
        {
            namespaceGroups.Add(ownNamespaceName, new[] { ownNamespace });
        }
        else if (!ownNamespaceSymbols.Any(namespaceSymbol => NamespaceContainsRootMember(namespaceSymbol, name, arity)))
        {
            namespaceGroups[ownNamespaceName] = ownNamespaceSymbols.Concat(new[] { ownNamespace }).ToArray();
        }

        var matchingNamespaces = namespaceGroups.Count(group => string.Equals(group.Key, ownNamespaceName, StringComparison.Ordinal)
                                                               || group.Value.Any(namespaceSymbol => NamespaceContainsRootMember(namespaceSymbol, name, arity)));
        return matchingNamespaces > 1;

    }


    /// <summary>
    /// 获取类型引用中的最外层类型
    /// </summary>
    /// <param name="type">待检查的类型</param>
    /// <returns>类型引用最外层的命名类型</returns>
    public static INamedTypeSymbol GetRootType(INamedTypeSymbol type)
    {

        var current = type;
        while (current.ContainingType is not null)
        {
            current = current.ContainingType;
        }

        return current;

    }


    /// <summary>
    /// 判断命名空间是否包含指定名称和泛型参数数量的根成员
    /// </summary>
    /// <param name="namespaceSymbol">待检查的命名空间</param>
    /// <param name="name">根成员名称</param>
    /// <param name="arity">类型泛型参数数量</param>
    /// <returns>存在同名类型或子命名空间时返回 true</returns>
    public static bool NamespaceContainsRootMember(INamespaceSymbol namespaceSymbol, string name, int arity)
        => namespaceSymbol.GetTypeMembers(name).Any(type => type.Arity == arity)
           || namespaceSymbol.GetNamespaceMembers().Any(childNamespace => string.Equals(childNamespace.Name, name, StringComparison.Ordinal));


    /// <summary>
    /// 递归检查命名类型 外层类型和全部泛型参数的根名称冲突
    /// </summary>
    /// <param name="type">待检查的类型</param>
    /// <param name="namespaces">生成文件实际导入的命名空间</param>
    /// <returns>任一组成类型发生冲突时返回 true</returns>
    private static bool HasConflictCore(ITypeSymbol type, IReadOnlyList<INamespaceSymbol> namespaces)
    {

        if (type is IArrayTypeSymbol arrayType)
            return HasConflictCore(arrayType.ElementType, namespaces);

        if (type is IPointerTypeSymbol pointerType)
            return HasConflictCore(pointerType.PointedAtType, namespaces);

        if (type is not INamedTypeSymbol namedType)
            return false;

        var rootType = GetRootType(namedType);
        if (CountMatchingNamespaces(rootType.Name, rootType.Arity, namespaces) > 1)
            return true;

        for (var current = namedType; current is not null; current = current.ContainingType)
        {
            foreach (var typeArgument in current.TypeArguments)
            {
                if (HasConflictCore(typeArgument, namespaces))
                    return true;
            }
        }

        return false;

    }


    /// <summary>
    /// 统计包含指定根成员的不同命名空间名称数量
    /// </summary>
    /// <param name="name">根成员名称</param>
    /// <param name="arity">类型泛型参数数量</param>
    /// <param name="namespaces">待检查的命名空间集合</param>
    /// <returns>包含匹配根成员的不同命名空间数量</returns>
    private static int CountMatchingNamespaces(string name, int arity, IEnumerable<INamespaceSymbol> namespaces)
        => namespaces
            .GroupBy(static namespaceSymbol => namespaceSymbol.ToDisplayString(), StringComparer.Ordinal)
            .Count(group => group.Any(namespaceSymbol => NamespaceContainsRootMember(namespaceSymbol, name, arity)));

}
