using Microsoft.CodeAnalysis;
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
