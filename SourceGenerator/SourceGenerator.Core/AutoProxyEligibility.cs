using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Generic;
using System.Linq;

namespace SourceGenerator.Core;

/// <summary>
/// 提供 AutoProxy 目标类型合法性判断
/// </summary>
internal static class AutoProxyEligibility
{

    private const string AutoProxyAttributeMetadataName = "SourceGenerator.Runtime.Attributes.AutoProxyAttribute";

    private const string ProxyBehaviorAttributeNamespace = "SourceGenerator.Runtime.Attributes";

    private const string ProxyBehaviorAttributeName = "ProxyBehaviorAttribute";

    private const string InvocationAsyncBehaviorMetadataName = "SourceGenerator.Runtime.Pipeline.IInvocationAsyncBehavior";

    private const string InvocationBehaviorMetadataName = "SourceGenerator.Runtime.Pipeline.IInvocationBehavior";

    private const string CacheableBehaviorMetadataName = "SourceGenerator.Runtime.Pipeline.Behaviors.CacheableBehavior";

    private const string ConcurrencyLimitBehaviorMetadataName = "SourceGenerator.Runtime.Pipeline.Behaviors.ConcurrencyLimitBehavior";

    private const string RetryBehaviorMetadataName = "SourceGenerator.Runtime.Pipeline.Behaviors.RetryBehavior";

    private const string SetsRequiredMembersAttributeMetadataName = "System.Diagnostics.CodeAnalysis.SetsRequiredMembersAttribute";


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
    /// <param name="compilation">当前编译上下文</param>
    /// <returns>如果类型和方法都可以生成代理则返回 true</returns>
    public static bool CanGenerateCompleteProxy(INamedTypeSymbol type, Compilation compilation)
        => CanGenerateProxy(type)
           && !GetUnsupportedAsyncByRefMethods(type).Any()
           && !GetUnsupportedPointerMethods(type).Any()
           && !GetUnsupportedPointerProperties(type).Any()
           && !GetUnsupportedRefLikeReturnMethods(type).Any()
           && !GetUnsupportedDefaultInterfaceMethods(type).Any()
           && !GetUnsupportedProxyBehaviors(type, compilation).Any();


    /// <summary>
    /// 获取目标类型需要生成 override 的直接方法和有效继承代理方法
    /// </summary>
    /// <param name="type">待生成代理的目标类型</param>
    /// <returns>需要生成 override 的方法列表</returns>
    public static IEnumerable<IMethodSymbol> GetEffectiveProxyMethods(INamedTypeSymbol type)
    {

        foreach (var method in type.GetMembers().OfType<IMethodSymbol>())
        {
            if (ShouldGenerateDerivedOverride(method))
                yield return method;
        }

        foreach (var method in GetEffectiveInheritedBehaviorMethods(type))
        {
            if (CanGenerateInheritedOverride(type, method, out _))
                yield return method;
        }

    }


    /// <summary>
    /// 检查目标类型是否可以生成 AutoProxy 代理
    /// </summary>
    /// <param name="type">待检查的目标类型</param>
    /// <returns>代理合法性检查结果</returns>
    public static AutoProxyValidationResult Validate(INamedTypeSymbol type)
    {

        if (type.TypeKind != TypeKind.Class)
            return AutoProxyValidationResult.Invalid("AutoProxy 只能标记在 class 类型上");

        if (type.IsRecord)
            return AutoProxyValidationResult.Invalid("record class 暂不支持生成派生代理");

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

        if (HasDuplicateLiftedTypeParameterNames(type))
            return AutoProxyValidationResult.Invalid("嵌套类型与外层类型不能声明同名泛型参数");

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
    /// 获取目标类型对应的代理类型名称
    /// </summary>
    /// <param name="type">被代理的目标类型</param>
    /// <returns>不会与同命名空间现有类型或其他嵌套代理冲突的名称</returns>
    public static string GetProxyTypeName(INamedTypeSymbol type)
    {

        var conventionalName = type.Name + "_Proxy";
        var genericArity = GetLiftedTypeParameterCount(type);
        var hasDeclaredCollision = type.ContainingNamespace.GetTypeMembers(conventionalName, genericArity).Length > 0;

        if (type.ContainingType is null && !hasDeclaredCollision && !type.ContainingNamespace.IsGlobalNamespace)
            return conventionalName;

        var identity = (type.ContainingAssembly?.Name ?? string.Empty)
            + "|"
            + type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        return conventionalName + "_" + ComputeStableHash(identity);

    }


    /// <summary>
    /// 获取返回 Task 或 ValueTask 且带 ref out in 参数的不可代理方法
    /// </summary>
    /// <param name="type">待检查的目标类型</param>
    /// <returns>不可代理的方法列表</returns>
    public static IEnumerable<IMethodSymbol> GetUnsupportedAsyncByRefMethods(INamedTypeSymbol type)
    {

        foreach (var method in GetEffectiveProxyMethods(type))
        {
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
    /// 获取包含指针或函数指针签名的不可代理方法
    /// </summary>
    /// <param name="type">待检查的目标类型</param>
    /// <returns>不可代理的指针签名方法列表</returns>
    public static IEnumerable<IMethodSymbol> GetUnsupportedPointerMethods(INamedTypeSymbol type)
    {

        foreach (var constructor in type.Constructors)
        {
            if (constructor.DeclaredAccessibility == Accessibility.Public && HasPointerSignature(constructor))
                yield return constructor;
        }

        foreach (var method in GetEffectiveProxyMethods(type))
        {
            if (HasPointerSignature(method))
                yield return method;
        }

        foreach (var iface in type.AllInterfaces)
        {
            foreach (var member in iface.GetMembers())
            {
                if (member is not IMethodSymbol method)
                    continue;

                if (!ShouldGenerateExplicitInterfaceMethod(type, method, out var implementationMethod))
                    continue;

                var diagnosticMethod = implementationMethod ?? method;

                if (HasPointerSignature(diagnosticMethod))
                    yield return diagnosticMethod;
            }
        }

    }


    /// <summary>
    /// 获取包含指针或函数指针签名且需要生成显式实现的接口属性
    /// </summary>
    /// <param name="type">待检查的目标类型</param>
    /// <returns>不可生成显式实现的指针属性列表</returns>
    public static IEnumerable<IPropertySymbol> GetUnsupportedPointerProperties(INamedTypeSymbol type)
    {

        foreach (var iface in type.AllInterfaces)
        {
            foreach (var property in iface.GetMembers().OfType<IPropertySymbol>())
            {
                if (!ShouldGenerateExplicitInterfaceProperty(type, property, out _))
                    continue;

                if (IsPointerType(property.Type) || property.Parameters.Any(parameter => IsPointerType(parameter.Type)))
                    yield return property;
            }
        }

    }


    /// <summary>
    /// 获取返回引用结构且无法进入运行时泛型管道的方法
    /// </summary>
    /// <param name="type">待检查的目标类型</param>
    /// <returns>返回引用结构的不可代理方法列表</returns>
    public static IEnumerable<IMethodSymbol> GetUnsupportedRefLikeReturnMethods(INamedTypeSymbol type)
    {

        foreach (var method in GetEffectiveProxyMethods(type))
        {
            if (HasUnsupportedRefLikeSignature(method))
                yield return method;
        }

        foreach (var iface in type.AllInterfaces)
        {
            foreach (var member in iface.GetMembers())
            {
                if (member is not IMethodSymbol method)
                    continue;

                if (!ShouldGenerateExplicitInterfaceMethod(type, method, out var implementationMethod))
                    continue;

                var diagnosticMethod = implementationMethod ?? method;

                if (HasUnsupportedRefLikeSignature(diagnosticMethod))
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
    /// 获取目标类型中无法在实际代理路径执行的行为特性
    /// </summary>
    /// <param name="type">待检查的目标类型</param>
    /// <param name="compilation">当前编译上下文</param>
    /// <returns>不兼容的代理行为列表</returns>
    public static IEnumerable<UnsupportedProxyBehaviorResult> GetUnsupportedProxyBehaviors(INamedTypeSymbol type, Compilation compilation)
    {

        foreach (var method in type.GetMembers().OfType<IMethodSymbol>())
        {
            var behaviorAttributes = method.GetAttributes().Where(IsProxyBehaviorAttribute).ToArray();

            if (behaviorAttributes.Length == 0)
                continue;

            var proxiedThroughExplicitInterface = IsProxiedThroughExplicitInterfaceMethod(type, method);

            if (!ShouldGenerateDerivedOverride(method) && !proxiedThroughExplicitInterface)
            {
                foreach (var attribute in behaviorAttributes)
                {
                    yield return CreateUnsupportedBehaviorResult(method, attribute, "该方法既不能生成 override 代理，也不属于可生成显式实现的接口代理路径");
                }

                continue;
            }

            if (!proxiedThroughExplicitInterface)
            {
                foreach (var result in GetOptionsTypeConflictResults(method, behaviorAttributes))
                {
                    yield return result;
                }
            }

            foreach (var result in GetBehaviorCompatibilityResults(type, method, behaviorAttributes, compilation))
            {
                yield return result;
            }
        }

        foreach (var method in GetEffectiveInheritedBehaviorMethods(type))
        {
            var behaviorAttributes = method.GetAttributes().Where(IsProxyBehaviorAttribute).ToArray();

            if (!CanGenerateInheritedOverride(type, method, out var reason))
            {
                foreach (var attribute in behaviorAttributes)
                {
                    yield return CreateUnsupportedBehaviorResult(method, attribute, reason);
                }

                continue;
            }

            foreach (var result in GetOptionsTypeConflictResults(method, behaviorAttributes))
            {
                yield return result;
            }

            foreach (var result in GetBehaviorCompatibilityResults(type, method, behaviorAttributes, compilation))
            {
                yield return result;
            }
        }

        foreach (var iface in type.AllInterfaces)
        {
            foreach (var method in iface.GetMembers().OfType<IMethodSymbol>())
            {
                var behaviorAttributes = method.GetAttributes().Where(IsProxyBehaviorAttribute).ToArray();

                if (behaviorAttributes.Length == 0)
                    continue;

                if (!ShouldGenerateExplicitInterfaceMethod(type, method, out var implementationMethod))
                {
                    if (IsDefaultInterfaceMethod(method) && !HasClassImplementation(type, method))
                        continue;

                    foreach (var attribute in behaviorAttributes)
                    {
                        yield return CreateUnsupportedBehaviorResult(method, attribute, "该接口方法不会生成显式接口代理实现，请将行为特性标注到实际被重写的实现方法");
                    }

                    continue;
                }

                var implementationAttributes = implementationMethod?.GetAttributes().Where(IsProxyBehaviorAttribute)
                    ?? Enumerable.Empty<AttributeData>();
                var effectiveBehaviorAttributes = behaviorAttributes.Concat(implementationAttributes).ToArray();

                foreach (var result in GetOptionsTypeConflictResults(method, effectiveBehaviorAttributes))
                {
                    yield return result;
                }

                foreach (var result in GetBehaviorCompatibilityResults(type, method, behaviorAttributes, compilation))
                {
                    yield return result;
                }
            }
        }

    }


    /// <summary>
    /// 从代理行为特性中提取行为类型和配置类型
    /// </summary>
    /// <param name="attribute">待检查的特性</param>
    /// <param name="behaviorType">代理行为类型</param>
    /// <param name="optionsType">代理行为配置类型</param>
    /// <returns>如果特性继承自代理行为基类则返回 true</returns>
    public static bool TryGetProxyBehaviorTypes(AttributeData attribute, out ITypeSymbol? behaviorType, out INamedTypeSymbol? optionsType)
    {

        behaviorType = null;
        optionsType = null;

        for (var type = attribute.AttributeClass; type is not null; type = type.BaseType)
        {
            var constructed = type.ConstructedFrom;

            if (!IsNamedType(constructed, ProxyBehaviorAttributeNamespace, ProxyBehaviorAttributeName))
                continue;

            if (type.TypeArguments.Length >= 1)
            {
                behaviorType = type.TypeArguments[0];
            }

            if (type.TypeArguments.Length >= 2)
            {
                optionsType = type.TypeArguments[1] as INamedTypeSymbol;
            }

            return true;
        }

        return false;

    }


    /// <summary>
    /// 判断代理行为特性是否需要使用参数生成缓存键或锁键
    /// </summary>
    /// <param name="attribute">待检查的代理行为特性</param>
    /// <returns>如果行为需要生成参数键则返回 true</returns>
    public static bool RequiresArgumentsKey(AttributeData attribute)
        => TryGetProxyBehaviorTypes(attribute, out var behaviorType, out _)
           && behaviorType is not null
           && UsesArgumentsKey(behaviorType, attribute);


    /// <summary>
    /// 查找 Options 类型继承链中按 C# 名称隐藏规则生效的配置属性
    /// </summary>
    /// <param name="optionsType">待查找的 Options 类型</param>
    /// <param name="propertyName">配置属性名称</param>
    /// <returns>最近继承层中的同名属性 不存在或被其他成员隐藏时返回 null</returns>
    public static IPropertySymbol? FindOptionsProperty(INamedTypeSymbol optionsType, string propertyName)
    {

        for (var current = optionsType; current is not null; current = current.BaseType)
        {
            var members = current.GetMembers(propertyName);

            if (members.Length == 0)
                continue;

            return members.OfType<IPropertySymbol>().FirstOrDefault(property => !property.IsIndexer);
        }

        return null;

    }


    /// <summary>
    /// 将 Attribute 命名参数转换为可直接写入生成源码的表达式
    /// </summary>
    /// <param name="constant">待转换的 Roslyn 常量</param>
    /// <param name="expression">转换后的 C# 表达式</param>
    /// <returns>如果常量能够安全转换则返回 true</returns>
    public static bool TryFormatAttributeArgument(TypedConstant constant, out string expression)
    {

        if (constant.IsNull)
        {
            expression = "null";
            return true;
        }

        if (constant.Kind == TypedConstantKind.Array)
            return TryFormatAttributeArray(constant, out expression);

        if (constant.Kind == TypedConstantKind.Type)
        {
            if (constant.Value is ITypeSymbol typeValue)
            {
                expression = "typeof(" + typeValue.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + ")";
                return true;
            }

            expression = string.Empty;
            return false;
        }

        if (constant.Kind == TypedConstantKind.Enum)
        {
            if (constant.Type is INamedTypeSymbol enumType
                && TryFormatPrimitiveConstant(constant.Value, out var enumValue))
            {
                expression = "(" + enumType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + ")" + enumValue;
                return true;
            }

            expression = string.Empty;
            return false;
        }

        return TryFormatPrimitiveConstant(constant.Value, out expression);

    }


    /// <summary>
    /// 将 Attribute 数组常量转换为显式数组创建表达式
    /// </summary>
    /// <param name="constant">待转换的数组常量</param>
    /// <param name="expression">转换后的数组表达式</param>
    /// <returns>如果数组及全部元素均可安全转换则返回 true</returns>
    private static bool TryFormatAttributeArray(TypedConstant constant, out string expression)
    {

        if (constant.Type is not IArrayTypeSymbol arrayType || arrayType.Rank != 1)
        {
            expression = string.Empty;
            return false;
        }

        var values = new List<string>(constant.Values.Length);

        foreach (var value in constant.Values)
        {
            if (!TryFormatAttributeArgument(value, out var itemExpression))
            {
                expression = string.Empty;
                return false;
            }

            values.Add(itemExpression);
        }

        expression = "new "
                     + arrayType.ElementType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                     + "[] { "
                     + string.Join(", ", values)
                     + " }";
        return true;

    }


    /// <summary>
    /// 将 Attribute 基元常量转换为保留类型语义的 C# 表达式
    /// </summary>
    /// <param name="value">待转换的常量值</param>
    /// <param name="expression">转换后的基元表达式</param>
    /// <returns>如果常量类型受支持则返回 true</returns>
    private static bool TryFormatPrimitiveConstant(object? value, out string expression)
    {

        switch (value)
        {
            case string stringValue:
                expression = Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(stringValue, quote: true);
                return true;

            case char charValue:
                expression = Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(charValue, quote: true);
                return true;

            case bool boolValue:
                expression = boolValue ? "true" : "false";
                return true;

            case byte byteValue:
                expression = "(byte)" + byteValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
                return true;

            case sbyte sbyteValue:
                expression = "(sbyte)" + sbyteValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
                return true;

            case short shortValue:
                expression = "(short)" + shortValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
                return true;

            case ushort ushortValue:
                expression = "(ushort)" + ushortValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
                return true;

            case int intValue:
                expression = intValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
                return true;

            case uint uintValue:
                expression = uintValue.ToString(System.Globalization.CultureInfo.InvariantCulture) + "U";
                return true;

            case long longValue:
                expression = longValue.ToString(System.Globalization.CultureInfo.InvariantCulture) + "L";
                return true;

            case ulong ulongValue:
                expression = ulongValue.ToString(System.Globalization.CultureInfo.InvariantCulture) + "UL";
                return true;

            case float floatValue when float.IsNaN(floatValue):
                expression = "global::System.Single.NaN";
                return true;

            case float floatValue when float.IsPositiveInfinity(floatValue):
                expression = "global::System.Single.PositiveInfinity";
                return true;

            case float floatValue when float.IsNegativeInfinity(floatValue):
                expression = "global::System.Single.NegativeInfinity";
                return true;

            case float floatValue:
                expression = floatValue.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "F";
                return true;

            case double doubleValue when double.IsNaN(doubleValue):
                expression = "global::System.Double.NaN";
                return true;

            case double doubleValue when double.IsPositiveInfinity(doubleValue):
                expression = "global::System.Double.PositiveInfinity";
                return true;

            case double doubleValue when double.IsNegativeInfinity(doubleValue):
                expression = "global::System.Double.NegativeInfinity";
                return true;

            case double doubleValue:
                expression = doubleValue.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "D";
                return true;

            case decimal decimalValue:
                expression = decimalValue.ToString(System.Globalization.CultureInfo.InvariantCulture) + "M";
                return true;

            default:
                expression = string.Empty;
                return false;
        }

    }


    /// <summary>
    /// 获取 AutoProxy 基类中被当前类型直接继承的代理行为方法
    /// </summary>
    /// <param name="type">当前代理目标类型</param>
    /// <returns>有效继承的代理行为方法列表</returns>
    private static IEnumerable<IMethodSymbol> GetEffectiveInheritedBehaviorMethods(INamedTypeSymbol type)
    {

        var closerMethods = type.GetMembers().OfType<IMethodSymbol>().ToList();

        for (var baseType = type.BaseType; baseType is not null && baseType.SpecialType != SpecialType.System_Object; baseType = baseType.BaseType)
        {
            var baseMethods = baseType.GetMembers().OfType<IMethodSymbol>().ToArray();

            foreach (var method in baseMethods)
            {
                if (closerMethods.Any(closerMethod => BlocksInheritedMethod(type, closerMethod, method)))
                    continue;

                if (!HasAutoProxyAttribute(baseType))
                    continue;

                if (!method.GetAttributes().Any(IsProxyBehaviorAttribute))
                    continue;

                yield return method;
            }

            closerMethods.AddRange(baseMethods);
        }

    }


    /// <summary>
    /// 判断较近继承层的方法是否阻断更远基类方法的行为传播
    /// </summary>
    /// <param name="targetType">当前代理目标类型</param>
    /// <param name="closerMethod">较近继承层中的方法</param>
    /// <param name="inheritedMethod">更远基类中的候选方法</param>
    /// <returns>如果属于同一虚方法槽或形成可访问的同签名隐藏则返回 true</returns>
    private static bool BlocksInheritedMethod(INamedTypeSymbol targetType, IMethodSymbol closerMethod, IMethodSymbol inheritedMethod)
    {

        if (!IsAccessibleFromDerivedProxy(targetType, closerMethod))
            return false;

        if (IsSameVirtualMethodSlot(closerMethod, inheritedMethod))
            return true;

        return HaveSameMethodSignature(closerMethod, inheritedMethod);

    }


    /// <summary>
    /// 判断两个方法是否属于同一虚方法重写链
    /// </summary>
    /// <param name="closerMethod">较近继承层中的方法</param>
    /// <param name="inheritedMethod">更远基类中的候选方法</param>
    /// <returns>如果较近方法重写了候选方法所在的虚方法槽则返回 true</returns>
    private static bool IsSameVirtualMethodSlot(IMethodSymbol closerMethod, IMethodSymbol inheritedMethod)
    {

        for (var current = closerMethod; current is not null; current = current.OverriddenMethod)
        {
            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, inheritedMethod.OriginalDefinition))
                return true;
        }

        return false;

    }


    /// <summary>
    /// 判断继承方法是否可以在当前目标类型的派生代理中生成 override
    /// </summary>
    /// <param name="targetType">当前代理目标类型</param>
    /// <param name="method">待生成的继承方法</param>
    /// <param name="reason">不能生成时的具体原因</param>
    /// <returns>如果可以生成安全 override 则返回 true</returns>
    private static bool CanGenerateInheritedOverride(INamedTypeSymbol targetType, IMethodSymbol method, out string reason)
    {

        if (!ShouldGenerateDerivedOverride(method))
        {
            reason = "继承方法不是可重写的实例 virtual 或 override 方法";
            return false;
        }

        if (method.IsAbstract)
        {
            reason = "继承方法是 abstract 方法，当前代理无法通过 base 调用原始实现";
            return false;
        }

        if (!IsAccessibleFromDerivedProxy(targetType, method))
        {
            reason = "继承方法的访问级别不允许当前生成代理进行 override";
            return false;
        }

        reason = string.Empty;
        return true;

    }


    /// <summary>
    /// 判断类型自身是否显式标注 AutoProxy
    /// </summary>
    /// <param name="type">待检查的类型</param>
    /// <returns>如果类型自身标注 AutoProxy 则返回 true</returns>
    private static bool HasAutoProxyAttribute(INamedTypeSymbol type)
        => type.GetAttributes().Any(attribute =>
            attribute.AttributeClass is not null
            && IsType(attribute.AttributeClass, AutoProxyAttributeMetadataName));


    /// <summary>
    /// 判断继承方法是否能从当前派生代理类型访问
    /// </summary>
    /// <param name="targetType">当前代理目标类型</param>
    /// <param name="method">待访问的继承方法</param>
    /// <returns>如果当前派生代理可以访问并重写则返回 true</returns>
    private static bool IsAccessibleFromDerivedProxy(INamedTypeSymbol targetType, IMethodSymbol method)
    {

        return IsAccessibilityAllowedFromDerivedProxy(targetType, method.DeclaredAccessibility, method.ContainingType);

    }


    /// <summary>
    /// 判断指定访问级别是否允许当前生成代理访问
    /// </summary>
    /// <param name="targetType">当前代理目标类型</param>
    /// <param name="accessibility">待检查的访问级别</param>
    /// <param name="containingType">成员或嵌套类型的声明类型</param>
    /// <returns>如果当前生成代理可以访问则返回 true</returns>
    private static bool IsAccessibilityAllowedFromDerivedProxy(INamedTypeSymbol targetType, Accessibility accessibility, INamedTypeSymbol? containingType)
    {

        var hasInternalAccess = HasInternalAccess(targetType.ContainingAssembly, containingType?.ContainingAssembly);
        var hasProtectedAccess = containingType is not null && IsDerivedFromOrEqual(targetType, containingType);

        return accessibility switch
        {
            Accessibility.Public => true,
            Accessibility.Internal => hasInternalAccess,
            Accessibility.Protected => hasProtectedAccess,
            Accessibility.ProtectedOrInternal => hasProtectedAccess || hasInternalAccess,
            Accessibility.ProtectedAndInternal => hasProtectedAccess && hasInternalAccess,
            _ => false
        };

    }


    /// <summary>
    /// 判断目标程序集是否拥有声明程序集的 internal 访问权限
    /// </summary>
    /// <param name="targetAssembly">当前生成代理所在程序集</param>
    /// <param name="declaringAssembly">成员或类型的声明程序集</param>
    /// <returns>如果程序集相同或声明程序集授予友元访问则返回 true</returns>
    private static bool HasInternalAccess(IAssemblySymbol? targetAssembly, IAssemblySymbol? declaringAssembly)
    {

        if (targetAssembly is null || declaringAssembly is null)
            return false;

        return SymbolEqualityComparer.Default.Equals(targetAssembly, declaringAssembly)
               || declaringAssembly.GivesAccessTo(targetAssembly);

    }


    /// <summary>
    /// 判断目标类型是否等于或派生自指定类型
    /// </summary>
    /// <param name="targetType">当前代理目标类型</param>
    /// <param name="candidateBaseType">待检查的声明类型</param>
    /// <returns>如果目标类型位于指定类型的继承链中则返回 true</returns>
    private static bool IsDerivedFromOrEqual(INamedTypeSymbol targetType, INamedTypeSymbol candidateBaseType)
    {

        for (var current = targetType; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, candidateBaseType.OriginalDefinition))
                return true;
        }

        return false;

    }


    /// <summary>
    /// 判断两个方法是否具有会发生 override 或隐藏关系的相同签名
    /// </summary>
    /// <param name="left">较近继承层中的方法</param>
    /// <param name="right">较远基类中的方法</param>
    /// <returns>如果方法名称和参数签名相同则返回 true</returns>
    private static bool HaveSameMethodSignature(IMethodSymbol left, IMethodSymbol right)
    {

        if (!string.Equals(left.Name, right.Name, System.StringComparison.Ordinal)
            || left.Arity != right.Arity
            || left.Parameters.Length != right.Parameters.Length)
            return false;

        for (var index = 0; index < left.Parameters.Length; index++)
        {
            var leftParameter = left.Parameters[index];
            var rightParameter = right.Parameters[index];

            if ((leftParameter.RefKind == RefKind.None) != (rightParameter.RefKind == RefKind.None)
                || !AreEquivalentSignatureTypes(leftParameter.Type, rightParameter.Type))
                return false;
        }

        return true;

    }


    /// <summary>
    /// 判断两个类型在方法签名中是否等价并归一化方法泛型参数名称
    /// </summary>
    /// <param name="left">左侧参数类型</param>
    /// <param name="right">右侧参数类型</param>
    /// <returns>如果两个签名类型等价则返回 true</returns>
    private static bool AreEquivalentSignatureTypes(ITypeSymbol left, ITypeSymbol right)
    {

        if (SymbolEqualityComparer.Default.Equals(left, right))
            return true;

        if (left.TypeKind == TypeKind.Dynamic && IsType(right, "System.Object"))
            return true;

        if (right.TypeKind == TypeKind.Dynamic && IsType(left, "System.Object"))
            return true;

        var leftTypeParameter = left as ITypeParameterSymbol;
        var rightTypeParameter = right as ITypeParameterSymbol;

        if (leftTypeParameter is not null || rightTypeParameter is not null)
        {
            return leftTypeParameter is not null
                   && rightTypeParameter is not null
                   && leftTypeParameter.TypeParameterKind == TypeParameterKind.Method
                   && rightTypeParameter.TypeParameterKind == TypeParameterKind.Method
                   && leftTypeParameter.Ordinal == rightTypeParameter.Ordinal;
        }

        if (left is IArrayTypeSymbol leftArray && right is IArrayTypeSymbol rightArray)
        {
            return leftArray.Rank == rightArray.Rank
                   && AreEquivalentSignatureTypes(leftArray.ElementType, rightArray.ElementType);
        }

        if (left is IPointerTypeSymbol leftPointer && right is IPointerTypeSymbol rightPointer)
            return AreEquivalentSignatureTypes(leftPointer.PointedAtType, rightPointer.PointedAtType);

        if (left is not INamedTypeSymbol leftNamed || right is not INamedTypeSymbol rightNamed)
            return false;

        if (leftNamed.IsTupleType)
            leftNamed = leftNamed.TupleUnderlyingType ?? leftNamed;

        if (rightNamed.IsTupleType)
            rightNamed = rightNamed.TupleUnderlyingType ?? rightNamed;

        if (!SymbolEqualityComparer.Default.Equals(leftNamed.OriginalDefinition, rightNamed.OriginalDefinition))
            return false;

        if (leftNamed.ContainingType is not null || rightNamed.ContainingType is not null)
        {
            if (leftNamed.ContainingType is null
                || rightNamed.ContainingType is null
                || !AreEquivalentSignatureTypes(leftNamed.ContainingType, rightNamed.ContainingType))
                return false;
        }

        if (leftNamed.TypeArguments.Length != rightNamed.TypeArguments.Length)
            return false;

        for (var index = 0; index < leftNamed.TypeArguments.Length; index++)
        {
            if (!AreEquivalentSignatureTypes(leftNamed.TypeArguments[index], rightNamed.TypeArguments[index]))
                return false;
        }

        return true;

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
    /// 判断提升到代理类的泛型参数是否存在重名
    /// </summary>
    /// <param name="type">待检查的目标类型</param>
    /// <returns>如果外层和内层泛型参数存在同名项则返回 true</returns>
    private static bool HasDuplicateLiftedTypeParameterNames(INamedTypeSymbol type)
    {

        var names = new HashSet<string>(System.StringComparer.Ordinal);
        var containingTypes = new Stack<INamedTypeSymbol>();

        for (var current = type; current is not null; current = current.ContainingType)
        {
            containingTypes.Push(current);
        }

        foreach (var containingType in containingTypes)
        {
            foreach (var typeParameter in containingType.TypeParameters)
            {
                if (!names.Add(typeParameter.Name))
                    return true;
            }
        }

        return false;

    }


    /// <summary>
    /// 获取提升到代理类型声明上的泛型参数数量
    /// </summary>
    /// <param name="type">待检查的目标类型</param>
    /// <returns>目标类型及所有外层类型的泛型参数总数</returns>
    private static int GetLiftedTypeParameterCount(INamedTypeSymbol type)
    {

        var count = 0;

        for (var current = type; current is not null; current = current.ContainingType)
        {
            count += current.TypeParameters.Length;
        }

        return count;

    }


    /// <summary>
    /// 计算用于生成类型名称的稳定哈希
    /// </summary>
    /// <param name="value">待计算的类型标识</param>
    /// <returns>八位十六进制稳定哈希</returns>
    private static string ComputeStableHash(string value)
    {

        const uint offsetBasis = 2166136261;
        const uint prime = 16777619;
        var hash = offsetBasis;

        foreach (var character in value)
        {
            hash ^= character;
            hash = unchecked(hash * prime);
        }

        return hash.ToString("X8", System.Globalization.CultureInfo.InvariantCulture);

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
    public static bool ShouldGenerateExplicitInterfaceMethod(INamedTypeSymbol type, IMethodSymbol method, out IMethodSymbol? impl)
    {

        impl = null;

        if (method.MethodKind != MethodKind.Ordinary
            || method.IsStatic
            || method.DeclaredAccessibility != Accessibility.Public)
            return false;

        impl = type.FindImplementationForInterfaceMember(method) as IMethodSymbol;

        if (impl is not null && impl.ExplicitInterfaceImplementations.Length > 0)
            return false;

        if (impl is not null && (impl.IsVirtual || impl.IsAbstract || impl.IsOverride))
            return false;

        return true;

    }


    /// <summary>
    /// 判断接口属性是否需要由代理生成显式实现
    /// </summary>
    /// <param name="type">待检查的目标类型</param>
    /// <param name="property">待检查的接口属性</param>
    /// <param name="implementation">接口属性在目标类型中的实现</param>
    /// <returns>如果需要生成显式接口实现则返回 true</returns>
    public static bool ShouldGenerateExplicitInterfaceProperty(INamedTypeSymbol type, IPropertySymbol property, out IPropertySymbol? implementation)
    {

        implementation = null;

        if (property.IsStatic || property.DeclaredAccessibility != Accessibility.Public)
            return false;

        implementation = type.FindImplementationForInterfaceMember(property) as IPropertySymbol;

        if (implementation is not null && implementation.ExplicitInterfaceImplementations.Length > 0)
            return false;

        if (implementation is not null)
        {
            var getter = implementation.GetMethod;
            var setter = implementation.SetMethod;

            if ((getter is not null && (getter.IsVirtual || getter.IsAbstract || getter.IsOverride))
                || (setter is not null && (setter.IsVirtual || setter.IsAbstract || setter.IsOverride)))
                return false;
        }

        return true;

    }


    /// <summary>
    /// 判断接口事件是否需要由代理生成显式实现
    /// </summary>
    /// <param name="type">待检查的目标类型</param>
    /// <param name="eventSymbol">待检查的接口事件</param>
    /// <param name="implementation">接口事件在目标类型中的实现</param>
    /// <returns>如果需要生成显式接口实现则返回 true</returns>
    public static bool ShouldGenerateExplicitInterfaceEvent(INamedTypeSymbol type, IEventSymbol eventSymbol, out IEventSymbol? implementation)
    {

        implementation = null;

        if (eventSymbol.IsStatic || eventSymbol.DeclaredAccessibility != Accessibility.Public)
            return false;

        implementation = type.FindImplementationForInterfaceMember(eventSymbol) as IEventSymbol;

        if (implementation is not null && implementation.ExplicitInterfaceImplementations.Length > 0)
            return false;

        if (implementation is not null)
        {
            var addMethod = implementation.AddMethod;
            var removeMethod = implementation.RemoveMethod;

            if ((addMethod is not null && (addMethod.IsVirtual || addMethod.IsAbstract || addMethod.IsOverride))
                || (removeMethod is not null && (removeMethod.IsVirtual || removeMethod.IsAbstract || removeMethod.IsOverride)))
                return false;
        }

        return true;

    }


    /// <summary>
    /// 判断实现方法是否会通过显式接口实现进入代理路径
    /// </summary>
    /// <param name="type">待检查的目标类型</param>
    /// <param name="method">待检查的实现方法</param>
    /// <returns>如果方法会通过显式接口代理实现则返回 true</returns>
    private static bool IsProxiedThroughExplicitInterfaceMethod(INamedTypeSymbol type, IMethodSymbol method)
    {

        foreach (var iface in type.AllInterfaces)
        {
            foreach (var interfaceMethod in iface.GetMembers().OfType<IMethodSymbol>())
            {
                if (!ShouldGenerateExplicitInterfaceMethod(type, interfaceMethod, out var impl))
                    continue;

                if (SymbolEqualityComparer.Default.Equals(impl, method))
                    return true;
            }
        }

        return false;

    }


    /// <summary>
    /// 获取指定方法上代理行为的兼容性检查结果
    /// </summary>
    /// <param name="targetType">当前代理目标类型</param>
    /// <param name="method">待检查的方法</param>
    /// <param name="attributes">待检查的代理行为特性</param>
    /// <param name="compilation">当前编译上下文</param>
    /// <returns>不兼容的代理行为列表</returns>
    private static IEnumerable<UnsupportedProxyBehaviorResult> GetBehaviorCompatibilityResults(INamedTypeSymbol targetType, IMethodSymbol method, IEnumerable<AttributeData> attributes, Compilation compilation)
    {

        foreach (var attribute in attributes)
        {
            if (!TryGetProxyBehaviorTypes(attribute, out var behaviorType, out var optionsType))
                continue;

            if (behaviorType is null)
            {
                yield return CreateUnsupportedBehaviorResult(method, attribute, "行为特性必须通过 ProxyBehaviorAttribute<TBehavior> 或 ProxyBehaviorAttribute<TBehavior, TOptions> 声明行为类型");
                continue;
            }

            if (attribute.ConstructorArguments.Length > 0)
            {
                yield return CreateUnsupportedBehaviorResult(method, attribute, "行为特性的构造函数参数不会映射到运行时行为配置，请改用与 Options 属性同名的命名参数");
                continue;
            }

            if (TryGetUnsupportedConstructibleTypeReason(targetType, behaviorType, "行为类型", out var constructionReason, out var behaviorConstructor))
            {
                yield return CreateUnsupportedBehaviorResult(method, attribute, constructionReason);
                continue;
            }

            if (TryGetUninitializedRequiredMembersReason((INamedTypeSymbol)behaviorType, behaviorConstructor!, new HashSet<string>(System.StringComparer.Ordinal), "行为类型", out var behaviorRequiredReason))
            {
                yield return CreateUnsupportedBehaviorResult(method, attribute, behaviorRequiredReason);
                continue;
            }

            if (optionsType is not null && TryGetUnsupportedOptionsReason(targetType, optionsType, attribute, compilation, out var optionsReason))
            {
                yield return CreateUnsupportedBehaviorResult(method, attribute, optionsReason);
                continue;
            }

            if (!ImplementsInterface(behaviorType, InvocationAsyncBehaviorMetadataName))
            {
                yield return CreateUnsupportedBehaviorResult(method, attribute, "行为类型未实现 IInvocationAsyncBehavior");
                continue;
            }

            if (RequiresSynchronousBehavior(method) && !ImplementsInterface(behaviorType, InvocationBehaviorMetadataName))
            {
                var reason = IsAsyncStreamReturn(method.ReturnType)
                    ? "异步流方法当前使用同步过滤管道，行为类型必须同时实现 IInvocationBehavior"
                    : !IsTaskOrValueTaskReturn(method.ReturnType)
                        ? "同步方法不能执行异步行为管道，行为类型必须同时实现 IInvocationBehavior；需要异步行为时请将方法返回类型改为 Task 或 ValueTask"
                        : "方法包含 ref、out、in、ref-like 参数或 ref 返回值，行为类型必须同时实现 IInvocationBehavior";

                yield return CreateUnsupportedBehaviorResult(method, attribute, reason);
                continue;
            }

            if (IsType(behaviorType, CacheableBehaviorMetadataName) && !HasCacheableReturnValue(method))
            {
                yield return CreateUnsupportedBehaviorResult(method, attribute, "Cacheable 只能标注在具有返回值的方法或 Task<T> ValueTask<T> 方法上");
                continue;
            }

            if (UsesArgumentsKey(behaviorType, attribute))
            {
                foreach (var parameter in method.Parameters)
                {
                    if (!TryGetUnsupportedKeyParameterReason(parameter, out var reason))
                        continue;

                    yield return CreateUnsupportedBehaviorResult(method, attribute, $"参数 {parameter.Name} 的类型 {parameter.Type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)} 无法生成稳定的参数键：{reason}");
                    break;
                }
            }

            if (IsType(behaviorType, RetryBehaviorMetadataName))
            {
                if (TryGetNamedInt32(attribute, "MaxRetries", out var maxRetries) && maxRetries < 0)
                {
                    yield return CreateUnsupportedBehaviorResult(method, attribute, "配置 MaxRetries 不能小于 0");
                }

                if (TryGetNamedInt32(attribute, "DelaySeconds", out var delaySeconds) && delaySeconds < 0)
                {
                    yield return CreateUnsupportedBehaviorResult(method, attribute, "配置 DelaySeconds 不能小于 0");
                }
            }

            if (IsType(behaviorType, CacheableBehaviorMetadataName)
                && TryGetNamedInt32(attribute, "TtlSeconds", out var ttlSeconds)
                && ttlSeconds <= 0)
            {
                yield return CreateUnsupportedBehaviorResult(method, attribute, "配置 TtlSeconds 必须大于 0");
            }
        }

    }


    /// <summary>
    /// 获取同一代理路径中重复使用相同 Options 类型的行为冲突
    /// </summary>
    /// <param name="method">当前代理方法</param>
    /// <param name="attributes">当前代理路径中的全部行为特性</param>
    /// <returns>Options 类型冲突列表</returns>
    private static IEnumerable<UnsupportedProxyBehaviorResult> GetOptionsTypeConflictResults(IMethodSymbol method, IEnumerable<AttributeData> attributes)
    {

        var optionsUsages = new Dictionary<ITypeSymbol, List<AttributeData>>(SymbolEqualityComparer.Default);

        foreach (var attribute in attributes)
        {
            if (!TryGetProxyBehaviorTypes(attribute, out _, out var optionsType) || optionsType is null)
                continue;

            if (!optionsUsages.TryGetValue(optionsType, out var usages))
            {
                usages = new List<AttributeData>();
                optionsUsages.Add(optionsType, usages);
            }

            usages.Add(attribute);
        }

        foreach (var usage in optionsUsages)
        {
            if (usage.Value.Count < 2)
                continue;

            var optionsName = usage.Key.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
            yield return CreateUnsupportedBehaviorResult(method, usage.Value[1], $"配置类型 {optionsName} 在同一代理路径中被多个行为使用，后写入的配置会覆盖前一份配置");
        }

    }


    /// <summary>
    /// 检查行为或配置类型是否能由当前生成代理直接实例化
    /// </summary>
    /// <param name="targetType">当前代理目标类型</param>
    /// <param name="candidateType">待检查的行为或配置类型</param>
    /// <param name="typeRole">诊断中使用的类型职责名称</param>
    /// <param name="reason">不支持时的具体原因</param>
    /// <returns>如果类型无法安全实例化则返回 true</returns>
    private static bool TryGetUnsupportedConstructibleTypeReason(INamedTypeSymbol targetType, ITypeSymbol candidateType, string typeRole, out string reason, out IMethodSymbol? constructor)
    {

        constructor = null;
        var displayName = candidateType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);

        if (candidateType is not INamedTypeSymbol namedType || namedType.TypeKind is not TypeKind.Class and not TypeKind.Struct)
        {
            reason = $"{typeRole} {displayName} 必须是可实例化的 class 或 struct";
            return true;
        }

        if (!IsTypeAccessibleFromDerivedProxy(targetType, namedType))
        {
            reason = $"{typeRole} {displayName} 无法从当前生成代理访问";
            return true;
        }

        if (namedType.IsAbstract)
        {
            reason = $"{typeRole} {displayName} 不能是 abstract 类型";
            return true;
        }

        if (namedType.IsRefLikeType)
        {
            reason = $"{typeRole} {displayName} 不能是 ref-like 类型";
            return true;
        }

        var callableConstructors = namedType.Constructors.Where(candidate =>
            !candidate.IsStatic
            && candidate.Parameters.All(parameter => parameter.IsOptional || parameter.IsParams)
            && IsAccessibleWithoutDerivation(targetType, candidate)).ToArray();
        var parameterlessConstructor = callableConstructors.FirstOrDefault(candidate => candidate.Parameters.Length == 0);

        if (parameterlessConstructor is not null)
        {
            constructor = parameterlessConstructor;
        }
        else if (callableConstructors.Length == 1)
        {
            constructor = callableConstructors[0];
        }
        else if (callableConstructors.Length > 1)
        {
            reason = $"{typeRole} {displayName} 存在多个能无参数调用的构造函数，生成器无法确定唯一调用目标";
            return true;
        }
        else
        {
            reason = $"{typeRole} {displayName} 必须具有当前生成代理可访问且能无参数调用的构造函数";
            return true;
        }

        reason = string.Empty;
        return false;

    }


    /// <summary>
    /// 检查行为配置类型和需要生成的属性赋值是否可访问
    /// </summary>
    /// <param name="targetType">当前代理目标类型</param>
    /// <param name="optionsType">待检查的行为配置类型</param>
    /// <param name="attribute">提供配置命名参数的行为特性</param>
    /// <param name="compilation">当前编译上下文</param>
    /// <param name="reason">不支持时的具体原因</param>
    /// <returns>如果配置类型或属性赋值无法安全生成则返回 true</returns>
    private static bool TryGetUnsupportedOptionsReason(INamedTypeSymbol targetType, INamedTypeSymbol optionsType, AttributeData attribute, Compilation compilation, out string reason)
    {

        if (TryGetUnsupportedConstructibleTypeReason(targetType, optionsType, "配置类型", out reason, out var constructor))
            return true;

        var assignedProperties = new HashSet<string>(System.StringComparer.Ordinal);

        foreach (var argument in attribute.NamedArguments)
        {
            var property = FindOptionsProperty(optionsType, argument.Key);

            if (property is null)
            {
                reason = $"配置命名参数 {argument.Key} 在类型 {optionsType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)} 中没有对应的可写属性";
                return true;
            }

            if (property.IsReadOnly || property.SetMethod is null)
            {
                reason = $"配置属性 {optionsType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)}.{property.Name} 必须具有可写 Setter";
                return true;
            }

            if (property.IsStatic)
            {
                reason = $"配置属性 {optionsType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)}.{property.Name} 不能是 static 属性";
                return true;
            }

            if (!IsAccessibleWithoutDerivation(targetType, property)
                || !IsAccessibleWithoutDerivation(targetType, property.SetMethod))
            {
                reason = $"配置属性 {optionsType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)}.{property.Name} 的 Setter 无法从当前生成代理访问";
                return true;
            }

            if (!IsTypedConstantAccessibleFromDerivedProxy(targetType, argument.Value))
            {
                reason = $"配置命名参数 {argument.Key} 引用了当前生成代理无法访问的类型";
                return true;
            }

            if (!TryFormatAttributeArgument(argument.Value, out _))
            {
                reason = $"配置命名参数 {argument.Key} 的值无法转换为安全的 C# 源码表达式";
                return true;
            }

            if (!HasImplicitOptionsAssignmentConversion(compilation, argument.Value, property.Type))
            {
                var sourceType = argument.Value.Type?.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat) ?? "null";
                var targetPropertyType = property.Type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
                reason = $"配置命名参数 {argument.Key} 的类型 {sourceType} 无法隐式赋值给 Options 属性类型 {targetPropertyType}";
                return true;
            }

            assignedProperties.Add(property.Name);
        }

        if (TryGetUninitializedRequiredMembersReason(optionsType, constructor!, assignedProperties, "配置类型", out reason))
            return true;

        reason = string.Empty;
        return false;

    }


    /// <summary>
    /// 判断 Attribute 常量生成的表达式能否隐式赋值给 Options 属性
    /// </summary>
    /// <param name="compilation">当前编译上下文</param>
    /// <param name="constant">待赋值的 Attribute 常量</param>
    /// <param name="destinationType">Options 属性类型</param>
    /// <returns>如果生成表达式存在合法隐式转换则返回 true</returns>
    private static bool HasImplicitOptionsAssignmentConversion(Compilation compilation, TypedConstant constant, ITypeSymbol destinationType)
    {

        if (constant.IsNull)
        {
            if (destinationType is INamedTypeSymbol nullableType
                && nullableType.IsGenericType
                && IsType(nullableType.ConstructedFrom, "System.Nullable"))
                return true;

            return destinationType.IsReferenceType
                   && destinationType.NullableAnnotation != NullableAnnotation.NotAnnotated;
        }

        if (constant.Type is null)
            return false;

        if (compilation is CSharpCompilation csharpCompilation
            && csharpCompilation.ClassifyConversion(constant.Type, destinationType).IsImplicit)
            return true;

        return HasImplicitConstantExpressionConversion(constant, destinationType);

    }


    /// <summary>
    /// 判断整型常量是否适用 C# 隐式常量表达式转换
    /// </summary>
    /// <param name="constant">待转换常量</param>
    /// <param name="destinationType">目标属性类型</param>
    /// <returns>如果常量值能隐式转换则返回 true</returns>
    private static bool HasImplicitConstantExpressionConversion(TypedConstant constant, ITypeSymbol destinationType)
    {

        if (destinationType is INamedTypeSymbol nullableType
            && nullableType.IsGenericType
            && IsType(nullableType.ConstructedFrom, "System.Nullable"))
        {
            destinationType = nullableType.TypeArguments[0];
        }

        if (constant.Type?.SpecialType == SpecialType.System_Int32 && constant.Value is int intValue)
        {
            if (destinationType.TypeKind == TypeKind.Enum && intValue == 0)
                return true;

            return destinationType.SpecialType switch
            {
                SpecialType.System_SByte => intValue is >= sbyte.MinValue and <= sbyte.MaxValue,
                SpecialType.System_Byte => intValue is >= byte.MinValue and <= byte.MaxValue,
                SpecialType.System_Int16 => intValue is >= short.MinValue and <= short.MaxValue,
                SpecialType.System_UInt16 => intValue is >= ushort.MinValue and <= ushort.MaxValue,
                SpecialType.System_Char => intValue is >= char.MinValue and <= char.MaxValue,
                SpecialType.System_UInt32 => intValue >= 0,
                SpecialType.System_UInt64 => intValue >= 0,
                _ => false
            };
        }

        return constant.Type?.SpecialType == SpecialType.System_Int64
               && constant.Value is long longValue
               && destinationType.SpecialType == SpecialType.System_UInt64
               && longValue >= 0;

    }


    /// <summary>
    /// 判断 Attribute 常量及其引用的类型是否能从当前生成代理访问
    /// </summary>
    /// <param name="targetType">当前代理目标类型</param>
    /// <param name="constant">待检查的 Attribute 常量</param>
    /// <returns>如果常量中涉及的全部类型均可访问则返回 true</returns>
    private static bool IsTypedConstantAccessibleFromDerivedProxy(INamedTypeSymbol targetType, TypedConstant constant)
    {

        if (constant.Type is not null && !IsTypeAccessibleFromDerivedProxy(targetType, constant.Type))
            return false;

        if (constant.Kind == TypedConstantKind.Type)
        {
            return constant.Value is ITypeSymbol typeValue
                   && IsTypeAccessibleFromDerivedProxy(targetType, typeValue);
        }

        if (constant.Kind == TypedConstantKind.Array)
            return constant.Values.All(value => IsTypedConstantAccessibleFromDerivedProxy(targetType, value));

        return true;

    }


    /// <summary>
    /// 检查对象创建表达式是否遗漏 required 成员初始化
    /// </summary>
    /// <param name="type">待实例化的类型</param>
    /// <param name="constructor">生成代码将调用的构造函数</param>
    /// <param name="assignedMembers">对象初始化器中已经赋值的成员名称</param>
    /// <param name="typeRole">诊断中使用的类型职责名称</param>
    /// <param name="reason">不支持时的具体原因</param>
    /// <returns>如果仍有 required 成员未初始化则返回 true</returns>
    private static bool TryGetUninitializedRequiredMembersReason(INamedTypeSymbol type, IMethodSymbol constructor, ISet<string> assignedMembers, string typeRole, out string reason)
    {

        if (constructor.GetAttributes().Any(attribute =>
                attribute.AttributeClass is not null
                && IsType(attribute.AttributeClass, SetsRequiredMembersAttributeMetadataName)))
        {
            reason = string.Empty;
            return false;
        }

        var requiredMembers = new HashSet<string>(System.StringComparer.Ordinal);

        for (var current = type; current is not null && current.SpecialType != SpecialType.System_Object; current = current.BaseType)
        {
            foreach (var member in current.GetMembers())
            {
                if (member is IPropertySymbol { IsRequired: true } requiredProperty)
                    requiredMembers.Add(requiredProperty.Name);

                if (member is IFieldSymbol { IsRequired: true } requiredField)
                    requiredMembers.Add(requiredField.Name);
            }
        }

        requiredMembers.ExceptWith(assignedMembers);

        if (requiredMembers.Count == 0)
        {
            reason = string.Empty;
            return false;
        }

        reason = $"{typeRole} {type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)} 的 required 成员 {string.Join(", ", requiredMembers.OrderBy(name => name, System.StringComparer.Ordinal))} 未被初始化";
        return true;

    }


    /// <summary>
    /// 判断类型在当前生成代理的声明位置是否可访问
    /// </summary>
    /// <param name="targetType">当前代理目标类型</param>
    /// <param name="type">待检查的类型</param>
    /// <returns>如果类型及其外层类型和泛型参数均可访问则返回 true</returns>
    private static bool IsTypeAccessibleFromDerivedProxy(INamedTypeSymbol targetType, ITypeSymbol type)
    {

        if (type is ITypeParameterSymbol || type.TypeKind == TypeKind.Dynamic)
            return true;

        if (type is IArrayTypeSymbol arrayType)
            return IsTypeAccessibleFromDerivedProxy(targetType, arrayType.ElementType);

        if (type is IPointerTypeSymbol pointerType)
            return IsTypeAccessibleFromDerivedProxy(targetType, pointerType.PointedAtType);

        if (type is not INamedTypeSymbol namedType)
            return true;

        if (namedType.ContainingType is null)
        {
            if (namedType.DeclaredAccessibility == Accessibility.Internal)
            {
                if (!HasInternalAccess(targetType.ContainingAssembly, namedType.ContainingAssembly))
                    return false;
            }
            else if (namedType.DeclaredAccessibility != Accessibility.Public)
            {
                return false;
            }
        }
        else
        {
            if (!IsTypeAccessibleFromDerivedProxy(targetType, namedType.ContainingType)
                || !IsAccessibilityAllowedFromDerivedProxy(targetType, namedType.DeclaredAccessibility, namedType.ContainingType))
                return false;
        }

        return namedType.IsUnboundGenericType
               || namedType.TypeArguments.All(typeArgument => IsTypeAccessibleFromDerivedProxy(targetType, typeArgument));

    }


    /// <summary>
    /// 判断成员是否能在不依赖继承访问的情况下由当前生成代理使用
    /// </summary>
    /// <param name="targetType">当前代理目标类型</param>
    /// <param name="member">待检查的成员</param>
    /// <returns>如果成员具有 public 或可用的 internal 访问路径则返回 true</returns>
    private static bool IsAccessibleWithoutDerivation(INamedTypeSymbol targetType, ISymbol member)
    {

        if (member.DeclaredAccessibility == Accessibility.Public)
            return true;

        if (member.DeclaredAccessibility is not Accessibility.Internal and not Accessibility.ProtectedOrInternal)
            return false;

        return HasInternalAccess(targetType.ContainingAssembly, member.ContainingAssembly);

    }


    /// <summary>
    /// 尝试读取代理行为特性中的 Int32 命名参数
    /// </summary>
    /// <param name="attribute">待读取的代理行为特性</param>
    /// <param name="name">命名参数名称</param>
    /// <param name="value">读取到的参数值</param>
    /// <returns>如果存在合法 Int32 参数值则返回 true</returns>
    private static bool TryGetNamedInt32(AttributeData attribute, string name, out int value)
    {

        foreach (var argument in attribute.NamedArguments)
        {
            if (string.Equals(argument.Key, name, System.StringComparison.Ordinal) && argument.Value.Value is int intValue)
            {
                value = intValue;
                return true;
            }
        }

        value = default;
        return false;

    }


    /// <summary>
    /// 判断代理行为是否会使用方法参数生成缓存键或锁键
    /// </summary>
    /// <param name="behaviorType">代理行为类型</param>
    /// <param name="attribute">代理行为特性</param>
    /// <returns>如果行为需要使用参数键则返回 true</returns>
    private static bool UsesArgumentsKey(ITypeSymbol behaviorType, AttributeData attribute)
    {

        if (IsType(behaviorType, CacheableBehaviorMetadataName))
            return true;

        if (!IsType(behaviorType, ConcurrencyLimitBehaviorMetadataName))
            return false;

        return attribute.NamedArguments.Any(argument =>
            string.Equals(argument.Key, "IsUseParameter", System.StringComparison.Ordinal)
            && argument.Value.Value is true);

    }


    /// <summary>
    /// 判断参数类型是否明确不适合生成稳定参数键
    /// </summary>
    /// <param name="parameter">待检查的参数</param>
    /// <param name="reason">不支持原因</param>
    /// <returns>如果参数类型明确不支持则返回 true</returns>
    private static bool TryGetUnsupportedKeyParameterReason(IParameterSymbol parameter, out string reason)
    {

        var type = parameter.Type;
        reason = string.Empty;

        if (IsType(type, "System.Threading.CancellationToken"))
            return false;

        if (parameter.RefKind == RefKind.Out || type.IsRefLikeType || type.TypeKind == TypeKind.Pointer || type is IFunctionPointerTypeSymbol)
        {
            reason = "该参数不能安全装箱并序列化";
            return true;
        }

        if (IsType(type, "System.Threading.CancellationTokenSource"))
        {
            reason = "取消控制对象不应参与业务调用身份";
            return true;
        }

        if (type.TypeKind == TypeKind.Delegate)
        {
            reason = "委托没有稳定的跨进程序列化表示";
            return true;
        }

        if (IsOrDerivedFrom(type, "System.IO.Stream")
            || IsOrDerivedFrom(type, "System.IO.TextReader")
            || IsOrDerivedFrom(type, "System.IO.TextWriter"))
        {
            reason = "流和读写器没有稳定的参数值表示";
            return true;
        }

        if (IsOrDerivedFrom(type, "System.IO.Pipelines.PipeReader")
            || IsOrDerivedFrom(type, "System.IO.Pipelines.PipeWriter")
            || IsOrDerivedFromGeneric(type, "System.Threading.Channels", "ChannelReader", 1)
            || IsOrDerivedFromGeneric(type, "System.Threading.Channels", "ChannelWriter", 1))
        {
            reason = "管道和通道对象没有稳定的参数值表示";
            return true;
        }

        if (IsInNamespaceHierarchy(type, "Microsoft.AspNetCore.Http"))
        {
            reason = "HTTP 上下文对象包含请求期状态";
            return true;
        }

        if (IsTypeOrImplementsInterface(type, "System.IServiceProvider")
            || IsTypeOrImplementsInterface(type, "Microsoft.Extensions.Logging.ILogger"))
        {
            reason = "基础设施服务对象不应参与业务调用身份";
            return true;
        }

        if (IsType(type, "System.Security.Claims.ClaimsPrincipal")
            || IsTypeOrImplementsInterface(type, "System.Security.Principal.IPrincipal"))
        {
            reason = "安全主体对象没有稳定且安全的参数键表示";
            return true;
        }

        if (IsOrDerivedFrom(type, "System.Data.Common.DbConnection")
            || IsTypeOrImplementsInterface(type, "System.Data.IDbConnection")
            || IsOrDerivedFrom(type, "System.Data.Common.DbTransaction")
            || IsOrDerivedFrom(type, "System.Data.Common.DbCommand"))
        {
            reason = "数据库连接对象不应参与业务调用身份";
            return true;
        }

        if (IsType(type, "System.Net.Http.HttpClient")
            || IsType(type, "System.Net.Http.HttpRequestMessage")
            || IsType(type, "System.Net.Http.HttpResponseMessage"))
        {
            reason = "HTTP 通信对象没有稳定的参数值表示";
            return true;
        }

        if (IsOrDerivedFrom(type, "System.Linq.Expressions.Expression"))
        {
            reason = "表达式树没有稳定的跨进程序列化表示";
            return true;
        }

        return false;

    }


    /// <summary>
    /// 创建不兼容代理行为检查结果
    /// </summary>
    /// <param name="method">行为所在方法</param>
    /// <param name="attribute">代理行为特性</param>
    /// <param name="reason">不兼容原因</param>
    /// <returns>不兼容代理行为检查结果</returns>
    private static UnsupportedProxyBehaviorResult CreateUnsupportedBehaviorResult(IMethodSymbol method, AttributeData attribute, string reason)
    {

        TryGetProxyBehaviorTypes(attribute, out var behaviorType, out _);

        var behaviorName = behaviorType?.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)
            ?? attribute.AttributeClass?.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)
            ?? "<unknown>";

        return new UnsupportedProxyBehaviorResult(method, attribute, behaviorName, reason);

    }


    /// <summary>
    /// 判断特性是否继承自代理行为基类
    /// </summary>
    /// <param name="attribute">待检查的特性</param>
    /// <returns>如果是代理行为特性则返回 true</returns>
    private static bool IsProxyBehaviorAttribute(AttributeData attribute)
        => TryGetProxyBehaviorTypes(attribute, out _, out _);


    /// <summary>
    /// 判断方法是否必须使用同步行为接口执行
    /// </summary>
    /// <param name="method">待检查的方法</param>
    /// <returns>如果方法必须使用同步行为接口则返回 true</returns>
    private static bool RequiresSynchronousBehavior(IMethodSymbol method)
        => !IsTaskOrValueTaskReturn(method.ReturnType)
           || method.ReturnsByRef
           || method.ReturnsByRefReadonly
           || method.Parameters.Any(parameter => parameter.RefKind != RefKind.None || parameter.Type.IsRefLikeType)
           || IsAsyncStreamReturn(method.ReturnType);


    /// <summary>
    /// 判断返回值是否为异步流或包装异步流的任务类型
    /// </summary>
    /// <param name="returnType">待检查的返回值类型</param>
    /// <returns>如果返回值包含异步流则返回 true</returns>
    private static bool IsAsyncStreamReturn(ITypeSymbol returnType)
    {

        if (returnType is not INamedTypeSymbol namedType || !namedType.IsGenericType)
            return false;

        var constructed = namedType.ConstructedFrom;

        if (IsNamedType(constructed, "System.Collections.Generic", "IAsyncEnumerable", 1))
            return true;

        if (IsNamedType(constructed, "System.Threading.Tasks", "Task") || IsNamedType(constructed, "System.Threading.Tasks", "ValueTask"))
            return IsAsyncStreamReturn(namedType.TypeArguments[0]);

        return false;

    }


    /// <summary>
    /// 判断类型是否实现指定接口
    /// </summary>
    /// <param name="type">待检查的类型</param>
    /// <param name="metadataName">接口元数据名称</param>
    /// <returns>如果类型实现指定接口则返回 true</returns>
    private static bool ImplementsInterface(ITypeSymbol type, string metadataName)
        => type.AllInterfaces.Any(interfaceType => IsType(interfaceType, metadataName));


    /// <summary>
    /// 判断类型本身或其继承链是否匹配指定类型
    /// </summary>
    /// <param name="type">待检查的类型</param>
    /// <param name="metadataName">目标类型元数据名称</param>
    /// <returns>如果类型本身或继承链匹配则返回 true</returns>
    private static bool IsOrDerivedFrom(ITypeSymbol type, string metadataName)
    {

        for (var current = type; current is not null; current = current.BaseType)
        {
            if (IsType(current, metadataName))
                return true;
        }

        return false;

    }


    /// <summary>
    /// 判断类型本身或其继承链是否匹配指定泛型类型
    /// </summary>
    /// <param name="type">待检查的类型</param>
    /// <param name="namespaceName">目标命名空间</param>
    /// <param name="typeName">目标类型名称</param>
    /// <param name="arity">目标泛型参数数量</param>
    /// <returns>如果类型本身或继承链匹配则返回 true</returns>
    private static bool IsOrDerivedFromGeneric(ITypeSymbol type, string namespaceName, string typeName, int arity)
    {

        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current is INamedTypeSymbol namedType
                && namedType.IsGenericType
                && IsNamedType(namedType.ConstructedFrom, namespaceName, typeName, arity))
                return true;
        }

        return false;

    }


    /// <summary>
    /// 判断类型本身或实现的接口是否匹配指定类型
    /// </summary>
    /// <param name="type">待检查的类型</param>
    /// <param name="metadataName">目标类型元数据名称</param>
    /// <returns>如果类型本身或接口匹配则返回 true</returns>
    private static bool IsTypeOrImplementsInterface(ITypeSymbol type, string metadataName)
        => IsType(type, metadataName) || ImplementsInterface(type, metadataName);


    /// <summary>
    /// 判断类型或其基类是否位于指定命名空间层级
    /// </summary>
    /// <param name="type">待检查的类型</param>
    /// <param name="namespacePrefix">目标命名空间前缀</param>
    /// <returns>如果类型位于指定命名空间层级则返回 true</returns>
    private static bool IsInNamespaceHierarchy(ITypeSymbol type, string namespacePrefix)
    {

        for (var current = type; current is not null; current = current.BaseType)
        {
            var namespaceName = current.ContainingNamespace.ToDisplayString();

            if (string.Equals(namespaceName, namespacePrefix, System.StringComparison.Ordinal)
                || namespaceName.StartsWith(namespacePrefix + ".", System.StringComparison.Ordinal))
                return true;
        }

        return false;

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
    /// 判断方法是否包含无法安全装箱或进入运行时泛型管道的引用结构签名
    /// </summary>
    /// <param name="method">待检查的方法</param>
    /// <returns>如果返回值是引用结构或参数允许引用结构则返回 true</returns>
    private static bool HasUnsupportedRefLikeSignature(IMethodSymbol method)
        => IsRefLikeOrAllowsRefLikeType(method.ReturnType)
           || method.Parameters.Any(parameter => parameter.Type is ITypeParameterSymbol { AllowsRefLikeType: true });


    /// <summary>
    /// 判断类型是否是引用结构或允许使用引用结构的类型参数
    /// </summary>
    /// <param name="type">待检查的类型</param>
    /// <returns>如果类型具有引用结构语义则返回 true</returns>
    private static bool IsRefLikeOrAllowsRefLikeType(ITypeSymbol type)
        => type.IsRefLikeType || type is ITypeParameterSymbol { AllowsRefLikeType: true };


    /// <summary>
    /// 判断方法返回值或参数是否包含当前不支持的指针签名
    /// </summary>
    /// <param name="method">待检查的方法</param>
    /// <returns>如果方法使用指针或函数指针则返回 true</returns>
    private static bool HasPointerSignature(IMethodSymbol method)
        => IsPointerType(method.ReturnType)
           || method.Parameters.Any(parameter => IsPointerType(parameter.Type));


    /// <summary>
    /// 判断类型是否是指针或函数指针
    /// </summary>
    /// <param name="type">待检查的类型</param>
    /// <returns>如果类型是指针或函数指针则返回 true</returns>
    private static bool IsPointerType(ITypeSymbol type)
        => type.TypeKind == TypeKind.Pointer || type is IFunctionPointerTypeSymbol;


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

        return IsNamedType(named, "System.Threading.Tasks", "Task")
               || IsNamedType(named, "System.Threading.Tasks", "ValueTask");

    }


    /// <summary>
    /// 判断方法是否具有可缓存的返回值
    /// </summary>
    /// <param name="method">待检查的方法</param>
    /// <returns>如果方法能够向缓存行为提供返回值则返回 true</returns>
    private static bool HasCacheableReturnValue(IMethodSymbol method)
    {

        if (method.ReturnsVoid)
            return false;

        if (method.ReturnType is not INamedTypeSymbol namedType)
            return true;

        return namedType.IsGenericType
               || (!IsType(namedType, "System.Threading.Tasks.Task")
                   && !IsType(namedType, "System.Threading.Tasks.ValueTask"));

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


    /// <summary>
    /// 判断命名类型是否匹配指定命名空间和类型名称
    /// </summary>
    /// <param name="type">待检查的命名类型</param>
    /// <param name="namespaceName">目标命名空间</param>
    /// <param name="typeName">目标类型名称</param>
    /// <returns>如果命名空间和类型名称均匹配则返回 true</returns>
    private static bool IsNamedType(INamedTypeSymbol type, string namespaceName, string typeName, int? arity = null)
        => string.Equals(type.ContainingNamespace.ToDisplayString(), namespaceName, System.StringComparison.Ordinal)
           && string.Equals(type.Name, typeName, System.StringComparison.Ordinal)
           && (!arity.HasValue || type.Arity == arity.Value);

}


/// <summary>
/// 表示无法在实际代理路径执行的行为特性
/// </summary>
internal readonly struct UnsupportedProxyBehaviorResult
{

    /// <summary>
    /// 行为所在方法
    /// </summary>
    public IMethodSymbol Method { get; }


    /// <summary>
    /// 代理行为特性
    /// </summary>
    public AttributeData Attribute { get; }


    /// <summary>
    /// 代理行为名称
    /// </summary>
    public string BehaviorName { get; }


    /// <summary>
    /// 不兼容原因
    /// </summary>
    public string Reason { get; }


    /// <summary>
    /// 创建不兼容代理行为检查结果
    /// </summary>
    /// <param name="method">行为所在方法</param>
    /// <param name="attribute">代理行为特性</param>
    /// <param name="behaviorName">代理行为名称</param>
    /// <param name="reason">不兼容原因</param>
    public UnsupportedProxyBehaviorResult(IMethodSymbol method, AttributeData attribute, string behaviorName, string reason)
    {

        Method = method;
        Attribute = attribute;
        BehaviorName = behaviorName;
        Reason = reason;

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
