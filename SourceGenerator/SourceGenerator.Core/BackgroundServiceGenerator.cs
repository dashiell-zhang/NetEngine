using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SourceGenerator.Core;

/// <summary>
/// 扫描继承自 Microsoft.Extensions.Hosting.BackgroundService 的类型
/// 为每个程序集生成注册后台服务的扩展方法
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class BackgroundServiceGenerator : IIncrementalGenerator
{

    /// <summary>
    /// 初始化增量生成器，配置语法筛选与源代码输出管道
    /// </summary>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 语法阶段只关心类声明，并尽早做语义过滤，减少后续处理量
        var candidateTypes = context.SyntaxProvider.CreateSyntaxProvider(
                static (node, _) => node is ClassDeclarationSyntax,
                static (syntaxContext, _) =>
                {
                    if (syntaxContext.Node is not ClassDeclarationSyntax classDeclaration)
                        return null;

                    if (syntaxContext.SemanticModel.GetDeclaredSymbol(classDeclaration) is not INamedTypeSymbol typeSymbol)
                        return null;

                    // 只处理当前项目源码中的 public 非抽象类（保持与原逻辑一致）
                    if (!typeSymbol.Locations.Any(l => l.IsInSource))
                        return null;
                    if (typeSymbol.TypeKind != TypeKind.Class || typeSymbol.IsAbstract)
                        return null;
                    if (typeSymbol.DeclaredAccessibility != Accessibility.Public)
                        return null;

                    return typeSymbol;
                })
            .Where(static type => type is not null)!
            .Select(static (type, _) => type!)
            .Collect()
            .Combine(context.CompilationProvider);


        context.RegisterSourceOutput(candidateTypes, static (spc, tuple) =>
        {

            var (typeSymbols, compilation) = (tuple.Left, tuple.Right);

            var usingNamespaces = new HashSet<string>(StringComparer.Ordinal);
            var nameCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            var registrationsInfo = new List<DisplayInfo>();

            static void CollectNamespaces(HashSet<string> nsSet, ITypeSymbol symbol)
            {
                // 处理数组类型的元素命名空间
                if (symbol is IArrayTypeSymbol arrayType)
                {
                    CollectNamespaces(nsSet, arrayType.ElementType);
                    return;
                }

                // 处理指针类型的目标命名空间
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

            // 通过 MetadataName 拿到需要用到的框架类型符号
            var bgServiceSymbol = compilation.GetTypeByMetadataName("Microsoft.Extensions.Hosting.BackgroundService");

            var servicesSymbol = compilation.GetTypeByMetadataName("Microsoft.Extensions.DependencyInjection.IServiceCollection");

            if (bgServiceSymbol is null || servicesSymbol is null)
            {
                // 目标项目没有引用 Hosting，直接跳过
                return;
            }

            // registrations 负责收集最终生成代码中的 services.AddHostedService<T>() 调用
            var registrations = new StringBuilder();

            // seenTypes 用于避免同一个符号被重复注册（例如多次局部声明等极端情况）
            var seenTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

            void AddCount(string name)
            {
                if (nameCounts.TryGetValue(name, out var count))
                {
                    nameCounts[name] = count + 1;
                }
                else
                {
                    nameCounts[name] = 1;
                }
            }

            foreach (var typeSymbol in typeSymbols)
            {
                if (!InheritsFrom(typeSymbol, bgServiceSymbol))
                    continue;

                if (!seenTypes.Add(typeSymbol))
                    continue;

                CollectNamespaces(usingNamespaces, typeSymbol);

                var implDisplay = GetDisplay(typeSymbol);
                AddCount(implDisplay.Minimal);
                registrationsInfo.Add(implDisplay);
            }

            foreach (var impl in registrationsInfo)
            {
                var display = nameCounts[impl.Minimal] > 1 ? impl.Full : impl.Minimal;
                var call = BuildBackgroundRegistrationCall(display);
                registrations.Append("        ").AppendLine(call);
            }

            var assemblyName = compilation.AssemblyName ?? "Assembly";

            var safeAssemblyName = SanitizeIdentifier(assemblyName);

            var ns = "NetEngine.Generated";

            var extClassName = "ServiceCollectionExtensions";

            // 每个模块统一生成 RegisterBackgroundServices_{AssemblyName}
            var methodName = "RegisterBackgroundServices_" + safeAssemblyName;

            // 仅在“可作为启动入口”的项目中生成聚合的 BatchRegisterBackgroundServices
            var isStartupLike = compilation.Options.OutputKind is OutputKind.ConsoleApplication
                                or OutputKind.WindowsApplication
                                or OutputKind.WindowsRuntimeApplication;

            // 对于既没有本地后台服务、又不是启动项目的情况，可以直接跳过。
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

            // 每个项目统一生成自己的 RegisterBackgroundServices_{AssemblyName} 扩展方法（仅在本程序集有后台服务时生成）
            if (registrations.Length > 0)
            {
                sb.Append("    public static IServiceCollection ")
                  .Append(methodName)
                  .AppendLine("(this IServiceCollection services)");
                sb.AppendLine("    {");
                sb.Append(registrations);
                sb.AppendLine("        return services;");
                sb.AppendLine("    }");
            }

            // 对于启动项目，额外生成一个聚合的 BatchRegisterBackgroundServices 方法，
            // 自动调用当前项目及所有引用项目的 RegisterBackgroundServices_{AssemblyName}。
            if (isStartupLike)
            {
                var methodNamesToInvoke = new List<string>();

                // 当前程序集如果有后台服务，则优先调用本地的 RegisterBackgroundServices_{AssemblyName}
                if (registrations.Length > 0)
                {
                    methodNamesToInvoke.Add(methodName);
                }

                // 遍历所有引用的程序集，尝试发现它们是否也生成了对应的 RegisterBackgroundServices_xxx 扩展方法
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

                    var refMethodName = "RegisterBackgroundServices_" + referencedSafeName;

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
                        // 把存在注册扩展方法的引用程序集记录下来，稍后统一调用
                        methodNamesToInvoke.Add(refMethodName);
                    }
                }

                sb.AppendLine();
                sb.AppendLine("    public static IServiceCollection BatchRegisterBackgroundServices(this IServiceCollection services)");
                sb.AppendLine("    {");
                foreach (var name in methodNamesToInvoke)
                {
                    sb.Append("        services.").Append(name).AppendLine("();");
                }
                sb.AppendLine("        return services;");
                sb.AppendLine("    }");
            }
            sb.AppendLine("}");

            var hintName = $"{safeAssemblyName}_BackgroundServices_Register.g.cs";
            spc.AddSource(hintName, sb.ToString());
        });
    }


    /// <summary>
    /// 判断给定类型是否继承自指定的基类类型
    /// </summary>
    /// <param name="type">要检查的类型。</param>
    /// <param name="baseType">目标基类类型</param>
    /// <returns>如果 <paramref name="type"/> 沿继承链继承自 <paramref name="baseType"/>，则返回 true</returns>
    private static bool InheritsFrom(INamedTypeSymbol type, INamedTypeSymbol baseType)
    {
        // 沿着 BaseType 链向上查找，判断是否继承自指定基类
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
                return true;
        }
        return false;
    }


    /// <summary>
    /// 构造注册后台服务的 AddHostedService 调用代码片段
    /// </summary>
    /// <param name="implDisplay">实现类型的显示名</param>
    /// <returns>形如 <c>services.AddHostedService&lt;Impl&gt;();</c> 的代码字符串</returns>
    private static string BuildBackgroundRegistrationCall(string implDisplay)
    {
        // 使用 services.AddHostedService<Impl>() 语法注册后台服务
        var sb = new StringBuilder("services.AddHostedService");
        sb.Append("<").Append(implDisplay).Append(">();");
        return sb.ToString();
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

        return new DisplayInfo(minimal, full, ns);
    }


    private sealed class DisplayInfo
    {
        public DisplayInfo(string minimal, string full, string? ns)
        {
            Minimal = minimal;
            Full = full;
            Namespace = ns;
        }

        public string Minimal { get; }

        public string Full { get; }

        public string? Namespace { get; }
    }


    /// <summary>
    /// 将任意字符串转换为合法的 C# 标识符，用于生成方法名等
    /// </summary>
    /// <param name="name">原始名称</param>
    /// <returns>可作为标识符使用的安全名称</returns>
    private static string SanitizeIdentifier(string name)
    {
        // 将任意程序集名称转换为合法的 C# 标识符，用于方法名的一部分
        if (string.IsNullOrEmpty(name))
            return "_";

        var builder = new StringBuilder(name.Length);
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

