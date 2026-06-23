using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;

namespace SourceGenerator.Core;

/// <summary>
/// 提供 AutoProxy 目标类型合法性判断
/// </summary>
internal static class AutoProxyEligibility
{

    /// <summary>
    /// 判断目标类型是否可以生成 AutoProxy 代理
    /// </summary>
    /// <param name="type">待检查的目标类型</param>
    /// <returns>如果可以生成代理则返回 true</returns>
    public static bool CanGenerateProxy(INamedTypeSymbol type)
        => Validate(type).CanGenerate;


    /// <summary>
    /// 判断目标类型是否可以生成完整可编译的 AutoProxy 代理
    /// </summary>
    /// <param name="type">待检查的目标类型</param>
    /// <returns>如果类型和方法都可以生成代理则返回 true</returns>
    public static bool CanGenerateCompleteProxy(INamedTypeSymbol type)
        => CanGenerateProxy(type)
           && !GetUnsupportedAsyncByRefMethods(type).Any()
           && !GetUnsupportedDefaultInterfaceMethods(type).Any();


    /// <summary>
    /// 检查目标类型是否可以生成 AutoProxy 代理
    /// </summary>
    /// <param name="type">待检查的目标类型</param>
    /// <returns>代理合法性检查结果</returns>
    public static AutoProxyValidationResult Validate(INamedTypeSymbol type)
    {

        if (type.TypeKind != TypeKind.Class)
            return AutoProxyValidationResult.Invalid("AutoProxy 只能标记在 class 类型上");

        if (type.IsStatic)
            return AutoProxyValidationResult.Invalid("静态类不能生成派生代理");

        if (type.IsSealed)
            return AutoProxyValidationResult.Invalid("sealed 类型不能生成派生代理");

        if (type.IsAbstract)
            return AutoProxyValidationResult.Invalid("abstract 类型不能作为可注册的 AutoProxy 服务实现");

        if (!IsSupportedAccessibility(type))
            return AutoProxyValidationResult.Invalid("类型或外层类型的访问级别不支持生成同级代理");

        if (!type.Constructors.Any(c => c.DeclaredAccessibility == Accessibility.Public))
            return AutoProxyValidationResult.Invalid("类型必须至少包含一个 public 构造函数");

        return AutoProxyValidationResult.Valid();

    }


    /// <summary>
    /// 获取代理类型声明应使用的访问修饰符
    /// </summary>
    /// <param name="type">被代理的目标类型</param>
    /// <returns>代理类型访问修饰符</returns>
    public static string GetProxyAccessibilityText(INamedTypeSymbol type)
        => IsEffectivelyPublic(type) ? "public" : "internal";


    /// <summary>
    /// 获取返回 Task 或 ValueTask 且带 ref out in 参数的不可代理方法
    /// </summary>
    /// <param name="type">待检查的目标类型</param>
    /// <returns>不可代理的方法列表</returns>
    public static IEnumerable<IMethodSymbol> GetUnsupportedAsyncByRefMethods(INamedTypeSymbol type)
    {

        foreach (var method in type.GetMembers().OfType<IMethodSymbol>())
        {
            if (!ShouldGenerateDerivedOverride(method))
                continue;

            if (IsUnsupportedAsyncByRefMethod(method))
                yield return method;
        }

        foreach (var iface in type.AllInterfaces)
        {
            foreach (var member in iface.GetMembers())
            {
                if (member is not IMethodSymbol method)
                    continue;

                if (!ShouldGenerateExplicitInterfaceMethod(type, method, out var impl))
                    continue;

                var diagnosticMethod = impl ?? method;

                if (IsUnsupportedAsyncByRefMethod(diagnosticMethod))
                    yield return diagnosticMethod;
            }
        }

    }


    /// <summary>
    /// 获取目标类型未显式实现且无法被代理安全转发的接口默认实现方法
    /// </summary>
    /// <param name="type">待检查的目标类型</param>
    /// <returns>无法代理的接口默认实现方法列表</returns>
    public static IEnumerable<IMethodSymbol> GetUnsupportedDefaultInterfaceMethods(INamedTypeSymbol type)
    {

        foreach (var iface in type.AllInterfaces)
        {
            foreach (var member in iface.GetMembers())
            {
                if (member is not IMethodSymbol method)
                    continue;

                if (!IsDefaultInterfaceMethod(method))
                    continue;

                if (!HasClassImplementation(type, method))
                    yield return method;
            }
        }

    }


    /// <summary>
    /// 判断目标类型及其外层类型是否都是 public
    /// </summary>
    /// <param name="type">待检查的目标类型</param>
    /// <returns>如果目标类型对外表现为 public 则返回 true</returns>
    private static bool IsEffectivelyPublic(INamedTypeSymbol type)
    {

        for (var t = type; t is not null; t = t.ContainingType)
        {
            if (t.DeclaredAccessibility != Accessibility.Public)
                return false;
        }

        return true;

    }


    /// <summary>
    /// 判断目标类型及其外层类型是否支持生成顶层代理类型
    /// </summary>
    /// <param name="type">待检查的目标类型</param>
    /// <returns>如果类型访问级别可被顶层代理访问则返回 true</returns>
    private static bool IsSupportedAccessibility(INamedTypeSymbol type)
    {

        for (var t = type; t is not null; t = t.ContainingType)
        {
            if (t.DeclaredAccessibility is not Accessibility.Public and not Accessibility.Internal)
                return false;
        }

        return true;

    }


    /// <summary>
    /// 判断方法是否需要生成派生类 override 实现
    /// </summary>
    /// <param name="method">待检查的方法</param>
    /// <returns>如果需要生成派生类 override 实现则返回 true</returns>
    private static bool ShouldGenerateDerivedOverride(IMethodSymbol method)
    {

        if (method.MethodKind is MethodKind.PropertyGet or MethodKind.PropertySet or MethodKind.EventAdd or MethodKind.EventRemove or MethodKind.EventRaise or MethodKind.StaticConstructor or MethodKind.Constructor)
            return false;

        if (!IsSupportedOverrideAccessibility(method.DeclaredAccessibility) || method.IsStatic)
            return false;

        if (method.IsSealed)
            return false;

        return method.IsVirtual || method.IsAbstract || method.IsOverride;

    }


    /// <summary>
    /// 判断接口方法是否需要生成显式接口代理实现
    /// </summary>
    /// <param name="type">待检查的目标类型</param>
    /// <param name="method">待检查的接口方法</param>
    /// <param name="impl">接口方法在目标类型中的实现</param>
    /// <returns>如果需要生成显式接口代理实现则返回 true</returns>
    private static bool ShouldGenerateExplicitInterfaceMethod(INamedTypeSymbol type, IMethodSymbol method, out IMethodSymbol? impl)
    {

        impl = null;

        if (method.MethodKind is MethodKind.PropertyGet or MethodKind.PropertySet or MethodKind.EventAdd or MethodKind.EventRemove or MethodKind.EventRaise)
            return false;

        impl = type.FindImplementationForInterfaceMember(method) as IMethodSymbol;

        if (impl is not null && impl.ExplicitInterfaceImplementations.Length > 0)
            return false;

        if (impl is not null && (impl.IsVirtual || impl.IsAbstract || impl.IsOverride))
            return false;

        return true;

    }


    /// <summary>
    /// 判断方法是否是当前不支持的异步 ref out in 签名
    /// </summary>
    /// <param name="method">待检查的方法</param>
    /// <returns>如果方法签名不支持生成代理则返回 true</returns>
    private static bool IsUnsupportedAsyncByRefMethod(IMethodSymbol method)
        => IsTaskOrValueTaskReturn(method.ReturnType)
           && method.Parameters.Any(p => p.RefKind != RefKind.None);


    /// <summary>
    /// 判断方法是否是需要类显式接管的接口默认实现方法
    /// </summary>
    /// <param name="method">待检查的方法</param>
    /// <returns>如果方法是接口默认实现方法则返回 true</returns>
    private static bool IsDefaultInterfaceMethod(IMethodSymbol method)
        => method.ContainingType.TypeKind == TypeKind.Interface
           && method.MethodKind == MethodKind.Ordinary
           && method.DeclaredAccessibility == Accessibility.Public
           && !method.IsAbstract
           && !method.IsStatic;


    /// <summary>
    /// 判断目标类型或其基类是否提供了接口方法的类实现
    /// </summary>
    /// <param name="type">待检查的目标类型</param>
    /// <param name="method">待检查的接口方法</param>
    /// <returns>如果接口方法由类层次结构实现则返回 true</returns>
    private static bool HasClassImplementation(INamedTypeSymbol type, IMethodSymbol method)
    {

        var impl = type.FindImplementationForInterfaceMember(method) as IMethodSymbol;

        return impl is not null
               && impl.ContainingType.TypeKind == TypeKind.Class;

    }


    /// <summary>
    /// 判断返回值是否为 Task 或 ValueTask
    /// </summary>
    /// <param name="returnType">待检查的返回值类型</param>
    /// <returns>如果返回值是 Task 或 ValueTask 则返回 true</returns>
    private static bool IsTaskOrValueTaskReturn(ITypeSymbol returnType)
    {

        if (returnType is not INamedTypeSymbol named)
            return false;

        var type = named.IsGenericType && named.ConstructedFrom is INamedTypeSymbol constructedFrom
            ? constructedFrom
            : named;

        return IsType(type, "System.Threading.Tasks.Task")
               || IsType(type, "System.Threading.Tasks.ValueTask");

    }


    /// <summary>
    /// 判断指定可访问性是否支持生成派生类 override
    /// </summary>
    /// <param name="accessibility">待检查的访问性</param>
    /// <returns>如果支持生成派生类 override 则返回 true</returns>
    private static bool IsSupportedOverrideAccessibility(Accessibility accessibility)
        => accessibility is Accessibility.Public
            or Accessibility.Protected
            or Accessibility.Internal
            or Accessibility.ProtectedOrInternal
            or Accessibility.ProtectedAndInternal;


    /// <summary>
    /// 判断类型是否匹配指定元数据名称
    /// </summary>
    /// <param name="type">待检查的类型</param>
    /// <param name="metadataName">元数据名称</param>
    /// <returns>如果类型匹配则返回 true</returns>
    private static bool IsType(ITypeSymbol type, string metadataName)
    {

        var checkType = type is INamedTypeSymbol { IsGenericType: true, ConstructedFrom: INamedTypeSymbol constructedFrom }
            ? constructedFrom
            : type;
        var actual = checkType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var expected = metadataName.StartsWith("global::", System.StringComparison.Ordinal)
            ? metadataName
            : "global::" + metadataName;

        return string.Equals(actual, expected, System.StringComparison.Ordinal);

    }

}


/// <summary>
/// 表示 AutoProxy 目标类型合法性检查结果
/// </summary>
internal readonly struct AutoProxyValidationResult
{

    /// <summary>
    /// 是否可以生成代理
    /// </summary>
    public bool CanGenerate { get; }


    /// <summary>
    /// 不能生成代理时的原因
    /// </summary>
    public string? Reason { get; }


    /// <summary>
    /// 使用合法性状态和失败原因创建检查结果
    /// </summary>
    /// <param name="canGenerate">是否可以生成代理</param>
    /// <param name="reason">不能生成代理时的原因</param>
    private AutoProxyValidationResult(bool canGenerate, string? reason)
    {

        CanGenerate = canGenerate;
        Reason = reason;

    }


    /// <summary>
    /// 创建合法检查结果
    /// </summary>
    /// <returns>合法检查结果</returns>
    public static AutoProxyValidationResult Valid()
        => new(true, null);


    /// <summary>
    /// 创建非法检查结果
    /// </summary>
    /// <param name="reason">不能生成代理时的原因</param>
    /// <returns>非法检查结果</returns>
    public static AutoProxyValidationResult Invalid(string reason)
        => new(false, reason);

}
