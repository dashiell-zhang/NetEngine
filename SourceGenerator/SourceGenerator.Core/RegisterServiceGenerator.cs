using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SourceGenerator.Core;

/// <summary>
/// 根据 RegisterServiceAttribute 生成 DI 注册扩展方法
/// 每个项目独立生成一份
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class RegisterServiceGenerator : IIncrementalGenerator
{
    private const string RegisterServiceAttributeMetadataName = "SourceGenerator.Runtime.Attributes.RegisterServiceAttribute";

    private const string AutoProxyAttributeMetadataName = "SourceGenerator.Runtime.Attributes.AutoProxyAttribute";


    /// <summary>
    /// 当服务实现存在多个直接业务接口且未显式选择时抛出的诊断定义
    /// </summary>
    private static readonly DiagnosticDescriptor AmbiguousServiceTypeDescriptor = new(
        id: "RegisterService001",
        title: "RegisterService 服务类型不明确",
        messageFormat: "类型 {0} 实现了多个直接业务接口：{1}，请通过 RegisterServiceAttribute 构造参数显式指定服务类型",
        category: "RegisterServiceGenerator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);


    /// <summary>
    /// 当显式服务类型不是实现类自身或已实现接口时抛出的诊断定义
    /// </summary>
    private static readonly DiagnosticDescriptor InvalidServiceTypeDescriptor = new(
        id: "RegisterService002",
        title: "RegisterService 服务类型无效",
        messageFormat: "显式服务类型 {0} 不是实现类 {1} 自身或其已实现接口",
        category: "RegisterServiceGenerator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private sealed class ServiceCandidate
    {

        public INamedTypeSymbol Type { get; }

        public AttributeData Attribute { get; }


        /// <summary>
        /// 使用类型符号和特性数据初始化服务候选项
        /// </summary>
        /// <param name="type">标记了 RegisterService 的类型</param>
        /// <param name="attribute">该类型上的 RegisterService 特性</param>
        public ServiceCandidate(INamedTypeSymbol type, AttributeData attribute)
        {
            Type = type;
            Attribute = attribute;
        }
    }


    /// <summary>
    /// 判断某个类型是否标记了 AutoProxy 特性
    /// </summary>
    /// <param name="typeSymbol">要检查的类型</param>
    /// <param name="autoProxyAttributeSymbol">AutoProxy 特性的类型符号</param>
    /// <returns>如果类型上存在 AutoProxy 特性则返回 true</returns>
    private static bool HasAutoProxy(INamedTypeSymbol typeSymbol, INamedTypeSymbol? autoProxyAttributeSymbol)
    {

        if (autoProxyAttributeSymbol is null)
            return false;

        foreach (var attr in typeSymbol.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, autoProxyAttributeSymbol))
                return true;
        }

        return false;
    }


    /// <summary>
    /// 初始化增量生成器，配置基于 RegisterService 特性生成 DI 注册代码的管道
    /// </summary>
    /// <param name="context">增量生成器初始化上下文</param>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 使用 ForAttributeWithMetadataName 直接筛选出带有 [RegisterService] 的类型，避免手动遍历语法树
        var candidates = context.SyntaxProvider.ForAttributeWithMetadataName(
            RegisterServiceAttributeMetadataName,
            static (node, _) => node is ClassDeclarationSyntax or RecordDeclarationSyntax,
            static (syntaxContext, _) =>
                new ServiceCandidate(
                    (INamedTypeSymbol)syntaxContext.TargetSymbol!,
                    syntaxContext.Attributes[0])
        );

        // 收集本次编译中所有 [RegisterService] 目标
        var collected = candidates.Collect().Combine(context.CompilationProvider);

        context.RegisterSourceOutput(collected, static (spc, tuple) =>
        {
            // serviceCandidates 为本次编译中所有打了 [RegisterService] 的类型及其特性数据
            var (serviceCandidates, compilation) = (tuple.Left, tuple.Right);

            var usingNamespaces = new HashSet<string>(StringComparer.Ordinal);
            var nameCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            var registrationInfos = new List<RegistrationInfo>();

            static void AddNamespace(HashSet<string> nsSet, string? nsValue)
            {
                // 收集单个命名空间，避免空字符串污染
                if (!string.IsNullOrWhiteSpace(nsValue))
                {
                    nsSet.Add(nsValue!);
                }
            }

            static void CollectNamespaces(HashSet<string> nsSet, ITypeSymbol symbol)
            {
                // 数组类型递归到元素类型
                if (symbol is IArrayTypeSymbol arrayType)
                {
                    CollectNamespaces(nsSet, arrayType.ElementType);
                    return;
                }

                // 指针类型递归到指向类型
                if (symbol is IPointerTypeSymbol pointerType)
                {
                    CollectNamespaces(nsSet, pointerType.PointedAtType);
                    return;
                }

                // 当前符号所在命名空间
                if (symbol.ContainingNamespace is { IsGlobalNamespace: false } ns)
                {
                    nsSet.Add(ns.ToDisplayString());
                }

                // 泛型参数与嵌套类型的命名空间
                if (symbol is INamedTypeSymbol named)
                {
                    foreach (var arg in named.TypeArguments)
                    {
                        CollectNamespaces(nsSet, arg);
                    }

                    if (named.ContainingType is not null)
                    {
                        CollectNamespaces(nsSet, named.ContainingType);
                    }
                }
            }

            // 没有引用依赖注入扩展包时无需生成任何代码
            var servicesSymbol = compilation.GetTypeByMetadataName("Microsoft.Extensions.DependencyInjection.IServiceCollection");

            if (servicesSymbol is null)
                return;

            // 通过 MetadataName 获取 AutoProxy 特性类型符号，用于后续符号级比较
            var autoProxyAttributeSymbol = compilation.GetTypeByMetadataName(AutoProxyAttributeMetadataName);

            var registrations = new StringBuilder();

            void AddCount(string? name)
            {
                if (string.IsNullOrWhiteSpace(name))
                    return;
                var key = name!;
                if (nameCounts.TryGetValue(key, out var count))
                {
                    nameCounts[key] = count + 1;
                }
                else
                {
                    nameCounts[key] = 1;
                }
            }

            foreach (var candidate in serviceCandidates)
            {
                var typeSymbol = candidate.Type;

                // 只处理当前项目源码中的类型，避免跨项目“公共库”被自动注册
                if (!typeSymbol.Locations.Any(l => l.IsInSource))
                    continue;

                if (typeSymbol.TypeKind != TypeKind.Class || typeSymbol.IsAbstract)
                    continue;

                var attrData = candidate.Attribute;
                var lifetime = GetLifetime(attrData) ?? "Transient";
                var keyExpr = GetKeyExpression(attrData);

                if (!TrySelectServiceType(typeSymbol, attrData, out var selectedServiceType, out var diagnostic))
                {
                    spc.ReportDiagnostic(diagnostic!);
                    continue;
                }

                var hasAutoProxy = HasAutoProxy(typeSymbol, autoProxyAttributeSymbol);
                var canUseAutoProxy = hasAutoProxy && AutoProxyEligibility.CanGenerateCompleteProxy(typeSymbol, compilation);
                CollectNamespaces(usingNamespaces, typeSymbol);

                // 如果服务类本身带有 [AutoProxy]，则注册时使用生成的 *Proxy 类型
                var implInfo = canUseAutoProxy
                    ? GetProxyDisplay(typeSymbol)
                    : GetDisplay(typeSymbol);

                if (selectedServiceType is not null)
                {
                    CollectNamespaces(usingNamespaces, selectedServiceType);
                }

                DisplayInfo? serviceInfo = null;
                var registersAsSelf = selectedServiceType is null
                                      || SymbolEqualityComparer.Default.Equals(selectedServiceType, typeSymbol);

                if (!registersAsSelf)
                {
                    serviceInfo = GetDisplay(selectedServiceType!);
                }

                // 自注册的 AutoProxy 服务需要将原始类型作为服务类型并将代理作为实现类型
                if (registersAsSelf && canUseAutoProxy)
                {
                    serviceInfo = GetDisplay(typeSymbol);
                }

                AddNamespace(usingNamespaces, serviceInfo?.Namespace);
                AddNamespace(usingNamespaces, implInfo.Namespace);

                AddCount(implInfo.ConflictKey);
                AddCount(serviceInfo?.ConflictKey);

                registrationInfos.Add(new RegistrationInfo(lifetime, keyExpr, implInfo, serviceInfo));
            }

            foreach (var info in registrationInfos)
            {
                var implDisplay = nameCounts[info.Impl.ConflictKey] > 1 ? info.Impl.Full : info.Impl.Minimal;
                var serviceDisplay = info.Service is null
                    ? null
                    : nameCounts[info.Service.ConflictKey] > 1
                        ? info.Service.Full
                        : info.Service.Minimal;
                var implTypeof = nameCounts[info.Impl.ConflictKey] > 1 ? info.Impl.OpenGenericFullTypeof : info.Impl.OpenGenericMinimalTypeof;
                var serviceTypeof = info.Service is null
                    ? null
                    : nameCounts[info.Service.ConflictKey] > 1
                        ? info.Service.OpenGenericFullTypeof
                        : info.Service.OpenGenericMinimalTypeof;

                var call = BuildRegistrationCall(info.Lifetime, info.KeyExpr, serviceDisplay, implDisplay, serviceTypeof, implTypeof);
                registrations.Append("        ").AppendLine(call);
            }

            var assemblyName = compilation.AssemblyName ?? "Assembly";

            var safeAssemblyName = SanitizeIdentifier(assemblyName);

            // 命名空间统一为 NetEngine.Generated，通过不同的方法名区分不同程序集：
            // RegisterServices_{AssemblyName}
            var ns = "NetEngine.Generated";
            var extClassName = "ServiceCollectionExtensions";
            var methodName = "RegisterServices_" + safeAssemblyName;

            // 启动项目（控制台 / 桌面应用等）才会生成聚合的 BatchRegisterServices
            var isStartupLike = compilation.Options.OutputKind is OutputKind.ConsoleApplication
                                or OutputKind.WindowsApplication
                                or OutputKind.WindowsRuntimeApplication;

            // 对于既没有本地注册、又不是启动项目的情况，可以直接跳过。
            if (!isStartupLike && registrations.Length == 0)
                return;

            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
            
            foreach (var nsToUse in usingNamespaces.OrderBy(x => x, StringComparer.Ordinal))
            {
                if (string.Equals(nsToUse, ns, StringComparison.Ordinal))
                    continue;

                sb.Append("using ").Append(nsToUse).AppendLine(";");
            }
            
            sb.Append("namespace ").Append(ns).AppendLine(";");
            sb.AppendLine();

            sb.Append("public static partial class ").Append(extClassName).AppendLine();
            sb.AppendLine("{");

            var hasLocalRegistrations = registrations.Length > 0;

            if (hasLocalRegistrations)
            {
                // 每个项目统一生成自己的 Add{Assembly}RegisterServices 扩展方法
                sb.Append("    public static IServiceCollection ")
                  .Append(methodName)
                  .AppendLine("(this IServiceCollection services)");
                sb.AppendLine("    {");
                sb.Append(registrations);
                sb.AppendLine("        return services;");
                sb.AppendLine("    }");
            }

            // 对于启动项目，额外生成一个聚合的 BatchRegisterServices 方法，
            // 自动调用当前项目及所有引用项目的 RegisterServices_{AssemblyName}。
            if (isStartupLike)
            {
                var methodNamesToInvoke = new List<string>();

                if (hasLocalRegistrations)
                {
                    methodNamesToInvoke.Add(methodName);
                }

                foreach (var reference in compilation.References)
                {
                    if (compilation.GetAssemblyOrModuleSymbol(reference) is not IAssemblySymbol asm)
                        continue;

                    // 跳过自身程序集
                    if (string.Equals(asm.Name, assemblyName, StringComparison.Ordinal))
                        continue;

                    var extType = asm.GetTypeByMetadataName("NetEngine.Generated.ServiceCollectionExtensions");
                    if (extType is null)
                        continue;

                    var referencedSafeName = SanitizeIdentifier(asm.Name);
                    var refMethodName = "RegisterServices_" + referencedSafeName;

                    var hasMethod = extType
                        .GetMembers(refMethodName)
                        .OfType<IMethodSymbol>()
                        .Any(m =>
                            m.IsStatic &&
                            m.IsExtensionMethod &&
                            m.Parameters.Length == 1 &&
                            SymbolEqualityComparer.Default.Equals(m.Parameters[0].Type, servicesSymbol));

                    if (hasMethod)
                    {
                        methodNamesToInvoke.Add(refMethodName);
                    }
                }

                sb.AppendLine();
                sb.AppendLine("    public static IServiceCollection BatchRegisterServices(this IServiceCollection services)");
                sb.AppendLine("    {");
                foreach (var name in methodNamesToInvoke)
                {
                    sb.Append("        services.").Append(name).AppendLine("();");
                }
                sb.AppendLine("        return services;");
                sb.AppendLine("    }");
            }

            sb.AppendLine("}");

            var hintName = $"{extClassName}_RegisterServices.g.cs";
            spc.AddSource(hintName, sb.ToString());
        });
    }


    /// <summary>
    /// 根据显式配置或直接业务接口选择唯一的 DI 服务类型
    /// </summary>
    /// <param name="implementationType">服务实现类型</param>
    /// <param name="attribute">RegisterService 特性数据</param>
    /// <param name="serviceType">选择出的服务类型，为 null 时表示实现类自注册</param>
    /// <param name="diagnostic">服务类型无法确定时产生的诊断</param>
    /// <returns>成功确定注册规则时返回 true</returns>
    private static bool TrySelectServiceType(INamedTypeSymbol implementationType, AttributeData attribute, out INamedTypeSymbol? serviceType, out Diagnostic? diagnostic)
    {

        serviceType = null;
        diagnostic = null;

        if (attribute.ConstructorArguments.Length > 0)
        {
            var configuredSymbol = attribute.ConstructorArguments[0].Value as ITypeSymbol;
            var configuredType = configuredSymbol as INamedTypeSymbol;
            var matchedType = configuredType is null ? null : FindConfiguredServiceType(implementationType, configuredType);

            if (matchedType is null)
            {
                diagnostic = Diagnostic.Create(
                    InvalidServiceTypeDescriptor,
                    GetAttributeLocation(attribute, implementationType),
                    configuredSymbol?.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat) ?? "null",
                    implementationType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat));

                return false;
            }

            serviceType = matchedType;
            return true;
        }

        var directInterfaces = GetDirectBusinessInterfaces(implementationType);
        if (directInterfaces.Length == 0)
            return true;

        if (directInterfaces.Length == 1)
        {
            serviceType = directInterfaces[0];
            return true;
        }

        diagnostic = Diagnostic.Create(
            AmbiguousServiceTypeDescriptor,
            GetAttributeLocation(attribute, implementationType),
            implementationType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
            string.Join(", ", directInterfaces.Select(static interfaceType => interfaceType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat))));

        return false;

    }


    /// <summary>
    /// 查找显式服务类型对应的实现类自身或已实现接口
    /// </summary>
    /// <param name="implementationType">服务实现类型</param>
    /// <param name="configuredType">特性中显式配置的服务类型</param>
    /// <returns>用于生成注册的实际类型符号，配置无效时返回 null</returns>
    private static INamedTypeSymbol? FindConfiguredServiceType(INamedTypeSymbol implementationType, INamedTypeSymbol configuredType)
    {

        if (MatchesConfiguredType(implementationType, configuredType))
            return implementationType;

        if (configuredType.TypeKind != TypeKind.Interface)
            return null;

        return implementationType.AllInterfaces.FirstOrDefault(interfaceType => MatchesConfiguredType(interfaceType, configuredType));

    }


    /// <summary>
    /// 判断实际类型是否匹配显式配置的普通类型或开放泛型类型
    /// </summary>
    /// <param name="actualType">实现类实际使用的类型</param>
    /// <param name="configuredType">特性中配置的类型</param>
    /// <returns>两个类型可以表示同一个服务契约时返回 true</returns>
    private static bool MatchesConfiguredType(INamedTypeSymbol actualType, INamedTypeSymbol configuredType)
    {

        if (configuredType.IsUnboundGenericType)
        {
            return SymbolEqualityComparer.Default.Equals(actualType.OriginalDefinition, configuredType.OriginalDefinition);
        }

        return SymbolEqualityComparer.Default.Equals(actualType, configuredType);

    }


    /// <summary>
    /// 获取实现类直接声明且最具体的业务接口
    /// </summary>
    /// <param name="implementationType">服务实现类型</param>
    /// <returns>按完整类型名称排序的直接业务接口</returns>
    private static INamedTypeSymbol[] GetDirectBusinessInterfaces(INamedTypeSymbol implementationType)
    {

        var directInterfaces = implementationType.Interfaces
            .Where(static interfaceType => !IsInfrastructureInterface(interfaceType))
            .ToArray();

        return directInterfaces
            .Where(candidate => !directInterfaces.Any(other => !SymbolEqualityComparer.Default.Equals(candidate, other)
                                                               && other.AllInterfaces.Contains(candidate, SymbolEqualityComparer.Default)))
            .OrderBy(static interfaceType => interfaceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), StringComparer.Ordinal)
            .ToArray();

    }


    /// <summary>
    /// 判断接口是否属于不应自动作为业务服务契约的基础设施接口
    /// </summary>
    /// <param name="interfaceType">待检查的接口</param>
    /// <returns>接口为 IDisposable 或 IAsyncDisposable 时返回 true</returns>
    private static bool IsInfrastructureInterface(INamedTypeSymbol interfaceType)
    {

        var metadataName = interfaceType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
        return string.Equals(metadataName, "System.IDisposable", StringComparison.Ordinal)
               || string.Equals(metadataName, "System.IAsyncDisposable", StringComparison.Ordinal);

    }


    /// <summary>
    /// 获取 RegisterService 特性对应的源码位置
    /// </summary>
    /// <param name="attribute">RegisterService 特性数据</param>
    /// <param name="implementationType">特性标记的实现类型</param>
    /// <returns>优先指向特性声明的诊断位置</returns>
    private static Location? GetAttributeLocation(AttributeData attribute, INamedTypeSymbol implementationType)
    {

        return attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation()
               ?? implementationType.Locations.FirstOrDefault();

    }


    private static DisplayInfo GetDisplay(INamedTypeSymbol typeSymbol)
    {
        // 生成最小限定名和命名空间限定名（不加 global::），供冲突时回退
        var minimal = typeSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        var full = typeSymbol.ToDisplayString(
            SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted));
        var ns = typeSymbol.ContainingNamespace is { IsGlobalNamespace: false }
            ? typeSymbol.ContainingNamespace.ToDisplayString()
            : null;

        var openGenericMinimalTypeof = ContainsTypeParameter(typeSymbol)
            ? "typeof(" + BuildOpenGenericTypeName(typeSymbol, includeNamespace: false) + ")"
            : null;
        var openGenericFullTypeof = ContainsTypeParameter(typeSymbol)
            ? "typeof(" + BuildOpenGenericTypeName(typeSymbol, includeNamespace: true) + ")"
            : null;

        return new DisplayInfo(minimal, full, ns, openGenericMinimalTypeof, openGenericFullTypeof);
    }


    private static DisplayInfo GetProxyDisplay(INamedTypeSymbol typeSymbol)
    {
        // AutoProxy 场景下的代理类型名称和命名空间
        var proxyNs = typeSymbol.ContainingNamespace is { IsGlobalNamespace: true }
            ? "NetEngine.Generated"
            : typeSymbol.ContainingNamespace.ToDisplayString();

        var proxyTypeName = AutoProxyEligibility.GetProxyTypeName(typeSymbol);
        var minimal = proxyTypeName;
        var full = $"{proxyNs}.{proxyTypeName}";
        var genericArity = GetAllTypeParameterCount(typeSymbol);
        var openGenericMinimalTypeof = genericArity > 0
            ? "typeof(" + minimal + BuildOpenGenericAritySuffix(genericArity) + ")"
            : null;
        var openGenericFullTypeof = genericArity > 0
            ? "typeof(" + full + BuildOpenGenericAritySuffix(genericArity) + ")"
            : null;

        return new DisplayInfo(minimal, full, proxyNs, openGenericMinimalTypeof, openGenericFullTypeof);
    }


    /// <summary>
    /// 判断类型声明或类型参数中是否包含开放泛型参数
    /// </summary>
    /// <param name="symbol">待检查的类型符号</param>
    /// <returns>如果包含开放泛型参数则返回 true</returns>
    private static bool ContainsTypeParameter(ITypeSymbol symbol)
    {
        if (symbol.TypeKind == TypeKind.TypeParameter)
            return true;

        if (symbol is IArrayTypeSymbol arrayType)
            return ContainsTypeParameter(arrayType.ElementType);

        if (symbol is IPointerTypeSymbol pointerType)
            return ContainsTypeParameter(pointerType.PointedAtType);

        if (symbol is INamedTypeSymbol named)
        {
            foreach (var typeArg in named.TypeArguments)
            {
                if (ContainsTypeParameter(typeArg))
                    return true;
            }

            return named.ContainingType is not null && ContainsTypeParameter(named.ContainingType);
        }

        return false;
    }


    /// <summary>
    /// 构建开放泛型类型在 typeof 表达式中的类型名
    /// </summary>
    /// <param name="typeSymbol">待转换的命名类型符号</param>
    /// <returns>可用于 typeof 的开放泛型类型名</returns>
    private static string BuildOpenGenericTypeName(INamedTypeSymbol typeSymbol, bool includeNamespace)
    {
        var containingTypes = new Stack<INamedTypeSymbol>();
        for (var t = typeSymbol; t is not null; t = t.ContainingType)
        {
            containingTypes.Push(t);
        }

        var sb = new StringBuilder();
        if (includeNamespace && typeSymbol.ContainingNamespace is { IsGlobalNamespace: false } ns)
        {
            sb.Append(ns.ToDisplayString()).Append('.');
        }

        var first = true;
        foreach (var t in containingTypes)
        {
            if (!first)
            {
                sb.Append('.');
            }

            sb.Append(t.Name).Append(BuildOpenGenericAritySuffix(t.Arity));
            first = false;
        }

        return sb.ToString();
    }


    /// <summary>
    /// 构建开放泛型类型参数占位符后缀
    /// </summary>
    /// <param name="arity">泛型参数数量</param>
    /// <returns>开放泛型后缀 例如 &lt;&gt; 或 &lt;,&gt;</returns>
    private static string BuildOpenGenericAritySuffix(int arity)
    {
        if (arity <= 0)
            return string.Empty;

        return "<" + new string(',', arity - 1) + ">";
    }


    /// <summary>
    /// 获取类型及其外层类型链上的泛型参数总数
    /// </summary>
    /// <param name="typeSymbol">待统计的类型符号</param>
    /// <returns>泛型参数总数</returns>
    private static int GetAllTypeParameterCount(INamedTypeSymbol typeSymbol)
    {
        var count = 0;
        for (var t = typeSymbol; t is not null; t = t.ContainingType)
        {
            count += t.TypeParameters.Length;
        }

        return count;
    }


    private sealed class DisplayInfo
    {
        public DisplayInfo(string minimal, string full, string? ns, string? openGenericMinimalTypeof, string? openGenericFullTypeof)
        {
            Minimal = minimal;
            Full = full;
            Namespace = ns;
            OpenGenericMinimalTypeof = openGenericMinimalTypeof;
            OpenGenericFullTypeof = openGenericFullTypeof;
        }

        /// <summary>
        /// 当前命名空间下可用的最短类型显示名
        /// </summary>
        public string Minimal { get; }

        /// <summary>
        /// 包含命名空间的完整类型显示名
        /// </summary>
        public string Full { get; }

        /// <summary>
        /// 类型所在命名空间
        /// </summary>
        public string? Namespace { get; }

        /// <summary>
        /// 用于判断是否需要完整限定名的冲突键
        /// </summary>
        public string ConflictKey => OpenGenericMinimalTypeof ?? Minimal;

        /// <summary>
        /// 开放泛型注册使用的最短 typeof 表达式
        /// </summary>
        public string? OpenGenericMinimalTypeof { get; }

        /// <summary>
        /// 开放泛型注册使用的完整 typeof 表达式
        /// </summary>
        public string? OpenGenericFullTypeof { get; }
    }


    private sealed class RegistrationInfo
    {
        public RegistrationInfo(string lifetime, string? keyExpr, DisplayInfo impl, DisplayInfo? service)
        {
            Lifetime = lifetime;
            KeyExpr = keyExpr;
            Impl = impl;
            Service = service;
        }

        public string Lifetime { get; }

        public string? KeyExpr { get; }

        public DisplayInfo Impl { get; }

        public DisplayInfo? Service { get; }
    }


    /// <summary>
    /// 根据生命周期、Key、服务接口和实现类型构造 DI 注册调用代码
    /// </summary>
    /// <param name="lifetime">服务生命周期字符串：Singleton / Scoped / Transient</param>
    /// <param name="keyExpr">Key 对应的 C# 表达式（用于 Keyed 服务）</param>
    /// <param name="ifaceDisplay">作为 TService 使用的类型显示名，可为空表示自注册</param>
    /// <param name="implDisplay">实现类型的显示名</param>
    /// <returns>完整的扩展方法调用代码字符串</returns>
    private static string BuildRegistrationCall(string lifetime, string? keyExpr, string? ifaceDisplay, string implDisplay, string? ifaceTypeof = null, string? implTypeof = null)
    {
        var hasInterface = !string.IsNullOrWhiteSpace(ifaceDisplay);

        var hasKey = !string.IsNullOrWhiteSpace(keyExpr);

        var useTypeofRegistration = !string.IsNullOrWhiteSpace(ifaceTypeof) || !string.IsNullOrWhiteSpace(implTypeof);

        var sb = new StringBuilder("services.");

        if (useTypeofRegistration)
        {
            var serviceType = hasInterface
                ? ifaceTypeof ?? "typeof(" + ifaceDisplay + ")"
                : implTypeof ?? "typeof(" + implDisplay + ")";
            var implementationType = implTypeof ?? serviceType;

            if (!hasKey)
            {
                sb.Append(lifetime switch
                {
                    "Singleton" => "AddSingleton",
                    "Scoped" => "AddScoped",
                    _ => "AddTransient"
                });

                if (hasInterface)
                {
                    sb.Append("(").Append(serviceType).Append(", ").Append(implementationType).Append(");");
                }
                else
                {
                    sb.Append("(").Append(serviceType).Append(");");
                }
            }
            else
            {
                sb.Append(lifetime switch
                {
                    "Singleton" => "AddKeyedSingleton",
                    "Scoped" => "AddKeyedScoped",
                    _ => "AddKeyedTransient"
                });

                if (hasInterface)
                {
                    sb.Append("(").Append(serviceType).Append(", ").Append(keyExpr).Append(", ").Append(implementationType).Append(");");
                }
                else
                {
                    sb.Append("(").Append(serviceType).Append(", ").Append(keyExpr).Append(");");
                }
            }

            return sb.ToString();
        }

        if (!hasKey)
        {
            // 普通（非 Keyed）注册：AddSingleton/AddScoped/AddTransient
            sb.Append(lifetime switch
            {
                "Singleton" => "AddSingleton",
                "Scoped" => "AddScoped",
                _ => "AddTransient"
            });

            if (hasInterface)
            {
                sb.Append("<").Append(ifaceDisplay).Append(", ").Append(implDisplay).Append(">();");
            }
            else
            {
                sb.Append("<").Append(implDisplay).Append(">();");
            }
        }
        else
        {
            // Keyed 注册：AddKeyedSingleton/AddKeyedScoped/AddKeyedTransient
            sb.Append(lifetime switch
            {
                "Singleton" => "AddKeyedSingleton",
                "Scoped" => "AddKeyedScoped",
                _ => "AddKeyedTransient"
            });

            if (hasInterface)
            {
                // AddKeyedXxx<TService, TImplementation>(services, key)
                sb.Append("<").Append(ifaceDisplay).Append(", ").Append(implDisplay).Append(">(").Append(keyExpr).Append(");");
            }
            else
            {
                // AddKeyedXxx<TService>(services, key)
                sb.Append("<").Append(implDisplay).Append(">(").Append(keyExpr).Append(");");
            }
        }

        return sb.ToString();
    }


    /// <summary>
    /// 从 RegisterService 特性中读取并转换 ServiceLifetime 枚举值
    /// </summary>
    /// <param name="attr">RegisterService 特性数据</param>
    /// <returns>生命周期字符串，或在未指定时返回 null</returns>
    private static string? GetLifetime(AttributeData attr)
    {
        foreach (var pair in attr.NamedArguments)
        {
            var key = pair.Key;
            var value = pair.Value;

            if (key == "Lifetime" && value.Value is int enumValue)
            {
                // ServiceLifetime enum: 0 Singleton, 1 Scoped, 2 Transient
                return enumValue switch
                {
                    0 => "Singleton",
                    1 => "Scoped",
                    _ => "Transient"
                };
            }
        }

        return null;
    }


    /// <summary>
    /// 从特性的命名参数中提取 Key，并生成对应的 C# 表达式字符串
    /// </summary>
    /// <param name="attr">RegisterService 特性数据</param>
    /// <returns>Key 的 C# 表达式字符串；未设置或显式为 null 时返回 null</returns>
    private static string? GetKeyExpression(AttributeData attr)
    {
        foreach (var pair in attr.NamedArguments)
        {
            if (pair.Key != "Key")
                continue;

            var typedConstant = pair.Value;

            // 显式为 null，则视为没有 Key
            if (typedConstant.Value is null)
                return null;

            if (!AutoProxyEligibility.TryFormatAttributeArgument(typedConstant, out var expr))
                return null;

            // 保险起见，防止出现字面量 "null"
            if (string.Equals(expr, "null", StringComparison.Ordinal))
                return null;

            return expr;
        }

        return null;
    }


    /// <summary>
    /// 将给定名称转换为合法的 C# 标识符，用于生成方法名后缀
    /// </summary>
    /// <param name="name">原始名称（通常为程序集名）</param>
    /// <returns>可安全用于标识符的位置的名称</returns>
    private static string SanitizeIdentifier(string name)
    {
        // 将程序集名称转换为合法的 C# 标识符，用于生成方法名后缀
        var builder = new StringBuilder(name.Length);
        if (name.Length == 0)
            return "_";

        if (!SyntaxFacts.IsIdentifierStartCharacter(name[0]))
        {
            builder.Append('_');
        }

        foreach (var ch in name)
        {
            builder.Append(SyntaxFacts.IsIdentifierPartCharacter(ch) ? ch : '_');
        }

        return builder.ToString();
    }

}
