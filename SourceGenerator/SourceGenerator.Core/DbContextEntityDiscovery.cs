using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace SourceGenerator.Core;

/// <summary>
/// 保存单个 DbContext 与其直接声明实体的对应关系
/// </summary>
internal sealed class DbContextEntityGroup
{

    /// <summary>
    /// 创建 DbContext 实体分组
    /// </summary>
    /// <param name="dbContextType">DbContext 类型</param>
    /// <param name="entityTypes">当前 DbContext 直接声明的实体类型</param>
    public DbContextEntityGroup(INamedTypeSymbol dbContextType, ImmutableArray<INamedTypeSymbol> entityTypes)
    {

        DbContextType = dbContextType;
        EntityTypes = entityTypes;

    }


    /// <summary>
    /// DbContext 类型
    /// </summary>
    public INamedTypeSymbol DbContextType { get; }


    /// <summary>
    /// 当前 DbContext 直接声明的实体类型
    /// </summary>
    public ImmutableArray<INamedTypeSymbol> EntityTypes { get; }

}

/// <summary>
/// 提供 DbContext 与直接声明 DbSet 实体的编译期发现能力
/// </summary>
internal static class DbContextEntityDiscovery
{

    /// <summary>
    /// 尝试为源码类型创建 DbContext 实体分组
    /// </summary>
    /// <param name="type">待分析的源码类型</param>
    /// <param name="dbContextSymbol">EF Core DbContext 类型</param>
    /// <param name="dbSetSymbol">EF Core DbSet 泛型类型</param>
    /// <returns>成功识别时返回实体分组 否则返回 null</returns>
    public static DbContextEntityGroup? CreateGroup(INamedTypeSymbol type, INamedTypeSymbol? dbContextSymbol, INamedTypeSymbol? dbSetSymbol)
    {

        if (dbContextSymbol is null || dbSetSymbol is null || !InheritsFrom(type, dbContextSymbol))
        {
            return null;
        }

        var entities = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
        var visited = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        foreach (var property in type.GetMembers().OfType<IPropertySymbol>())
        {
            if (property.IsStatic
                || property.Type is not INamedTypeSymbol namedType
                || !SymbolEqualityComparer.Default.Equals(namedType.OriginalDefinition, dbSetSymbol)
                || namedType.TypeArguments.Length != 1
                || namedType.TypeArguments[0] is not INamedTypeSymbol entityType
                || !visited.Add(entityType))
            {
                continue;
            }

            entities.Add(entityType);
        }

        return new DbContextEntityGroup(type, entities.ToImmutable());

    }


    /// <summary>
    /// 合并分部声明产生的重复上下文分组并稳定排序
    /// </summary>
    /// <param name="groups">待规范化的上下文分组</param>
    /// <returns>按上下文完整名称排序的唯一分组</returns>
    public static ImmutableArray<DbContextEntityGroup> NormalizeGroups(ImmutableArray<DbContextEntityGroup> groups)
    {

        if (groups.IsDefaultOrEmpty)
        {
            return ImmutableArray<DbContextEntityGroup>.Empty;
        }

        var entityMap = new Dictionary<INamedTypeSymbol, HashSet<INamedTypeSymbol>>(SymbolEqualityComparer.Default);

        foreach (var group in groups)
        {
            if (!entityMap.TryGetValue(group.DbContextType, out var entities))
            {
                entities = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
                entityMap.Add(group.DbContextType, entities);
            }

            foreach (var entityType in group.EntityTypes)
            {
                entities.Add(entityType);
            }
        }

        var result = ImmutableArray.CreateBuilder<DbContextEntityGroup>(entityMap.Count);

        foreach (var pair in entityMap.OrderBy(static pair => pair.Key.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), StringComparer.Ordinal))
        {
            var entities = pair.Value
                .OrderBy(static entityType => entityType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), StringComparer.Ordinal)
                .ToImmutableArray();

            result.Add(new DbContextEntityGroup(pair.Key, entities));
        }

        return result.ToImmutable();

    }


    /// <summary>
    /// 判断任一上下文分组是否直接包含指定实体
    /// </summary>
    /// <param name="groups">上下文实体分组</param>
    /// <param name="entityType">待判断的实体类型</param>
    /// <returns>至少一个上下文直接声明该实体时返回 true</returns>
    public static bool ContainsEntity(ImmutableArray<DbContextEntityGroup> groups, INamedTypeSymbol entityType)
    {

        foreach (var group in groups)
        {
            if (group.EntityTypes.Contains(entityType, SymbolEqualityComparer.Default))
            {
                return true;
            }
        }

        return false;

    }


    /// <summary>
    /// 判断上下文类型能否作为顶层生成扩展方法的参数类型
    /// </summary>
    /// <param name="dbContextType">待检查的上下文类型</param>
    /// <returns>类型可访问且不包含开放泛型参数时返回 true</returns>
    public static bool CanReferenceContextType(INamedTypeSymbol dbContextType)
    {

        return GeneratedCodeAccessibility.IsTypeReferenceAccessible(dbContextType)
               && !ContainsTypeParameter(dbContextType);

    }


    /// <summary>
    /// 判断类型或其外层类型是否包含开放泛型参数
    /// </summary>
    /// <param name="type">待检查的类型</param>
    /// <returns>包含开放泛型参数时返回 true</returns>
    private static bool ContainsTypeParameter(INamedTypeSymbol type)
    {

        for (var current = type; current is not null; current = current.ContainingType)
        {
            if (current.TypeArguments.Any(static typeArgument => typeArgument is ITypeParameterSymbol))
            {
                return true;
            }
        }

        return false;

    }


    /// <summary>
    /// 判断类型是否继承自指定基类
    /// </summary>
    /// <param name="type">待检查的类型</param>
    /// <param name="baseType">目标基类</param>
    /// <returns>继承链包含目标基类时返回 true</returns>
    private static bool InheritsFrom(INamedTypeSymbol type, INamedTypeSymbol baseType)
    {

        for (var current = type; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
            {
                return true;
            }
        }

        return false;

    }

}
