using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
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
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 使用 ForAttributeWithMetadataName 直接筛选出带有 [RegisterService] 的类型，避免手动遍历语法树
        var candidates = context.SyntaxProvider.ForAttributeWithMetadataName(
            RegisterServiceAttributeMetadataName,
            static (node, _) => node is ClassDeclarationSyntax,
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

            var usingNamespaces = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);

            static void AddNamespace(System.Collections.Generic.HashSet<string> nsSet, string? nsValue)
            {
                // 收集单个命名空间，避免空字符串污染
                if (!string.IsNullOrWhiteSpace(nsValue))
                {
                    nsSet.Add(nsValue);
                }
            }

            static void CollectNamespaces(System.Collections.Generic.HashSet<string> nsSet, ITypeSymbol symbol)
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

                var hasAutoProxy = HasAutoProxy(typeSymbol, autoProxyAttributeSymbol);
                CollectNamespaces(usingNamespaces, typeSymbol);

                // 如果服务类本身带有 [AutoProxy]，则注册时使用生成的 *Proxy 类型
                var (implDisplay, implNamespace) = hasAutoProxy
                    ? GetProxyDisplay(typeSymbol)
                    : GetMinimalDisplay(typeSymbol);

                var iface = typeSymbol.AllInterfaces.FirstOrDefault();
                if (iface is not null)
                {
                    CollectNamespaces(usingNamespaces, iface);
                }

                // serviceDisplay: 作为泛型 TService 使用的类型
                // 1. 优先使用第一个接口（典型接口编程场景）；
                // 2. 如果没有接口但带 [AutoProxy]，则使用原始类类型，形成
                //    AddScoped<Demo2Service, Demo2Service_Proxy>() 这样的注册；
                // 3. 否则为 null，走 self 注册。
                string? serviceDisplay = iface?.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                string? serviceNamespace = iface?.ContainingNamespace is { IsGlobalNamespace: false }
                    ? iface.ContainingNamespace.ToDisplayString()
                    : null;

                if (serviceDisplay is null && hasAutoProxy)
                {
                    (serviceDisplay, serviceNamespace) = GetMinimalDisplay(typeSymbol);
                }

                AddNamespace(usingNamespaces, serviceNamespace);
                AddNamespace(usingNamespaces, implNamespace);

                var call = BuildRegistrationCall(lifetime, keyExpr, serviceDisplay, implDisplay);
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
                var methodNamesToInvoke = new System.Collections.Generic.List<string>();

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


    private static (string Display, string? Namespace) GetMinimalDisplay(INamedTypeSymbol typeSymbol)
    {
        // 生成最小限定名，配合 using 使用短名
        var display = typeSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        var ns = typeSymbol.ContainingNamespace is { IsGlobalNamespace: false }
            ? typeSymbol.ContainingNamespace.ToDisplayString()
            : null;

        return (display, ns);
    }


    private static (string Display, string Namespace) GetProxyDisplay(INamedTypeSymbol typeSymbol)
    {
        // AutoProxy 场景下的代理类型名称和命名空间
        var proxyNs = typeSymbol.ContainingNamespace is { IsGlobalNamespace: true }
            ? "NetEngine.Generated"
            : typeSymbol.ContainingNamespace.ToDisplayString();

        return ($"{typeSymbol.Name}_Proxy", proxyNs);
    }


    /// <summary>
    /// 根据生命周期、Key、服务接口和实现类型构造 DI 注册调用代码
    /// </summary>
    /// <param name="lifetime">服务生命周期字符串：Singleton / Scoped / Transient</param>
    /// <param name="keyExpr">Key 对应的 C# 表达式（用于 Keyed 服务）</param>
    /// <param name="ifaceDisplay">作为 TService 使用的类型显示名，可为空表示自注册</param>
    /// <param name="implDisplay">实现类型的显示名</param>
    /// <returns>完整的扩展方法调用代码字符串</returns>
    private static string BuildRegistrationCall(string lifetime, string? keyExpr, string? ifaceDisplay, string implDisplay)
    {
        var hasInterface = !string.IsNullOrWhiteSpace(ifaceDisplay);

        var hasKey = !string.IsNullOrWhiteSpace(keyExpr);

        var sb = new StringBuilder("services.");

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

            // 使用 Roslyn 自带的 ToCSharpString 生成常量/typeof/枚举等表达式，
            // 这样可以支持 string、数字、bool、enum、typeof(...) 等所有合法属性值。
            var expr = typedConstant.ToCSharpString();

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
