using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;

namespace SourceGenerator.Core;

/// <summary>
/// 提供 AutoProxy 目标类型合法性判断
/// </summary>
internal static class AutoProxyEligibility
{

    private const string ProxyBehaviorAttributeNamespace = "SourceGenerator.Runtime.Attributes";

    private const string ProxyBehaviorAttributeName = "ProxyBehaviorAttribute";

    private const string InvocationAsyncBehaviorMetadataName = "SourceGenerator.Runtime.Pipeline.IInvocationAsyncBehavior";

    private const string InvocationBehaviorMetadataName = "SourceGenerator.Runtime.Pipeline.IInvocationBehavior";

    private const string CacheableBehaviorMetadataName = "SourceGenerator.Runtime.Pipeline.Behaviors.CacheableBehavior";

    private const string ConcurrencyLimitBehaviorMetadataName = "SourceGenerator.Runtime.Pipeline.Behaviors.ConcurrencyLimitBehavior";

    private const string RetryBehaviorMetadataName = "SourceGenerator.Runtime.Pipeline.Behaviors.RetryBehavior";


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
           && !GetUnsupportedDefaultInterfaceMethods(type).Any()
           && !GetUnsupportedProxyBehaviors(type).Any();


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
    /// 获取目标类型中无法在实际代理路径执行的行为特性
    /// </summary>
    /// <param name="type">待检查的目标类型</param>
    /// <returns>不兼容的代理行为列表</returns>
    public static IEnumerable<UnsupportedProxyBehaviorResult> GetUnsupportedProxyBehaviors(INamedTypeSymbol type)
    {

        foreach (var method in type.GetMembers().OfType<IMethodSymbol>())
        {
            var behaviorAttributes = method.GetAttributes().Where(IsProxyBehaviorAttribute).ToArray();

            if (behaviorAttributes.Length == 0)
                continue;

            if (!ShouldGenerateDerivedOverride(method) && !IsProxiedThroughExplicitInterfaceMethod(type, method))
            {
                foreach (var attribute in behaviorAttributes)
                {
                    yield return CreateUnsupportedBehaviorResult(method, attribute, "该方法既不能生成 override 代理，也不属于可生成显式实现的接口代理路径");
                }

                continue;
            }

            foreach (var result in GetBehaviorCompatibilityResults(method, behaviorAttributes))
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

                if (!ShouldGenerateExplicitInterfaceMethod(type, method, out _))
                {
                    if (IsDefaultInterfaceMethod(method) && !HasClassImplementation(type, method))
                        continue;

                    foreach (var attribute in behaviorAttributes)
                    {
                        yield return CreateUnsupportedBehaviorResult(method, attribute, "该接口方法不会生成显式接口代理实现，请将行为特性标注到实际被重写的实现方法");
                    }

                    continue;
                }

                foreach (var result in GetBehaviorCompatibilityResults(method, behaviorAttributes))
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
    /// <param name="method">待检查的方法</param>
    /// <param name="attributes">待检查的代理行为特性</param>
    /// <returns>不兼容的代理行为列表</returns>
    private static IEnumerable<UnsupportedProxyBehaviorResult> GetBehaviorCompatibilityResults(IMethodSymbol method, IEnumerable<AttributeData> attributes)
    {

        foreach (var attribute in attributes)
        {
            if (!TryGetProxyBehaviorTypes(attribute, out var behaviorType, out _))
                continue;

            if (behaviorType is null)
            {
                yield return CreateUnsupportedBehaviorResult(method, attribute, "行为特性必须通过 ProxyBehaviorAttribute<TBehavior> 或 ProxyBehaviorAttribute<TBehavior, TOptions> 声明行为类型");
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
                    : "方法包含 ref、out、in、ref-like 参数或 ref 返回值，行为类型必须同时实现 IInvocationBehavior";

                yield return CreateUnsupportedBehaviorResult(method, attribute, reason);
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
        }

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
        => method.ReturnsByRef
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

        if (IsType(constructed, "System.Collections.Generic.IAsyncEnumerable"))
            return true;

        if (IsType(constructed, "System.Threading.Tasks.Task") || IsType(constructed, "System.Threading.Tasks.ValueTask"))
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
