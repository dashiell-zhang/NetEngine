using Microsoft.CodeAnalysis;
using System.Linq;

namespace SourceGenerator.Core;

/// <summary>
/// 提供顶层生成代码引用类型时使用的可访问性检查
/// </summary>
internal static class GeneratedCodeAccessibility
{

    /// <summary>
    /// 判断类型引用能否由同程序集的顶层生成代码直接使用
    /// </summary>
    /// <param name="type">待检查的类型引用</param>
    /// <returns>类型及其组成部分都可以访问时返回 true</returns>
    public static bool IsTypeReferenceAccessible(ITypeSymbol type)
    {

        if (type is ITypeParameterSymbol)
            return true;

        if (type is IArrayTypeSymbol arrayType)
            return IsTypeReferenceAccessible(arrayType.ElementType);

        if (type is IPointerTypeSymbol pointerType)
            return IsTypeReferenceAccessible(pointerType.PointedAtType);

        if (type is IFunctionPointerTypeSymbol functionPointerType)
        {
            return IsTypeReferenceAccessible(functionPointerType.Signature.ReturnType)
                   && functionPointerType.Signature.Parameters.All(static parameter => IsTypeReferenceAccessible(parameter.Type));
        }

        if (type is not INamedTypeSymbol namedType)
            return true;

        for (var current = namedType; current is not null; current = current.ContainingType)
        {
            if (current.IsFileLocal || !IsAccessibilitySupported(current.DeclaredAccessibility))
                return false;

            if (current.TypeArguments.Any(static typeArgument => !IsTypeReferenceAccessible(typeArgument)))
                return false;
        }

        return true;

    }


    /// <summary>
    /// 判断实现类型是否具有可由依赖注入激活的 public 实例构造函数
    /// </summary>
    /// <param name="type">待检查的实现类型</param>
    /// <returns>至少存在一个 public 实例构造函数时返回 true</returns>
    public static bool HasPublicInstanceConstructor(INamedTypeSymbol type)
        => type.InstanceConstructors.Any(static constructor => constructor.DeclaredAccessibility == Accessibility.Public);


    /// <summary>
    /// 判断访问级别是否允许同程序集且不依赖继承的顶层代码访问
    /// </summary>
    /// <param name="accessibility">待检查的访问级别</param>
    /// <returns>访问级别受支持时返回 true</returns>
    private static bool IsAccessibilitySupported(Accessibility accessibility)
        => accessibility is Accessibility.Public or Accessibility.Internal or Accessibility.ProtectedOrInternal;

}
