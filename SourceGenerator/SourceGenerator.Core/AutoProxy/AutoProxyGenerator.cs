using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SourceGenerator.Core.Shared;

namespace SourceGenerator.Core.AutoProxy;

/// <summary>
/// 根据 AutoProxy 特性为目标类型生成派生代理类 支持拦截调用并注入行为管道
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class AutoProxyGenerator : IIncrementalGenerator
{

    private const string AutoProxyAttributeMetadataName = "SourceGenerator.Runtime.Attributes.AutoProxyAttribute";


    /// <summary>
    /// 配置增量生成管道 注册对标记 AutoProxy 特性的类型的处理逻辑
    /// </summary>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider.ForAttributeWithMetadataName(
            AutoProxyAttributeMetadataName,
            static (node, _) => node is ClassDeclarationSyntax or RecordDeclarationSyntax,
            static (syntaxContext, _) => syntaxContext
        );

        var combined = candidates.Combine(context.CompilationProvider);

        context.RegisterSourceOutput(combined, static (spc, tuple) =>
        {
            var (ctx, compilation) = (tuple.Left, tuple.Right);
            if (ctx.TargetSymbol is not INamedTypeSymbol typeSymbol)
                return;

            if (typeSymbol.TypeKind == TypeKind.Class)
            {
                var analysis = AutoProxyEligibility.Analyze(typeSymbol, compilation);
                var validation = analysis.Validation;

                if (!validation.CanGenerate)
                {
                    spc.ReportDiagnostic(Diagnostic.Create(
                        InvalidAutoProxyTargetDescriptor,
                        typeSymbol.Locations.FirstOrDefault(),
                        typeSymbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                        validation.Reason ?? "目标类型不满足代理生成条件"));

                    return;
                }

                foreach (var method in analysis.UnsupportedAsyncByRefMethods)
                {
                    spc.ReportDiagnostic(Diagnostic.Create(
                        UnsupportedAsyncByRefMethodDescriptor,
                        method.Locations.FirstOrDefault() ?? typeSymbol.Locations.FirstOrDefault(),
                        method.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)));
                }

                foreach (var method in analysis.UnsupportedPointerMethods)
                {
                    spc.ReportDiagnostic(Diagnostic.Create(
                        UnsupportedPointerMethodDescriptor,
                        method.Locations.FirstOrDefault() ?? typeSymbol.Locations.FirstOrDefault(),
                        method.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)));
                }

                foreach (var method in analysis.UnsupportedRefLikeReturnMethods)
                {
                    spc.ReportDiagnostic(Diagnostic.Create(
                        UnsupportedRefLikeReturnMethodDescriptor,
                        method.Locations.FirstOrDefault() ?? typeSymbol.Locations.FirstOrDefault(),
                        method.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)));
                }

                foreach (var method in analysis.UnsupportedDefaultInterfaceMethods)
                {
                    spc.ReportDiagnostic(Diagnostic.Create(
                        UnsupportedDefaultInterfaceMethodDescriptor,
                        method.Locations.FirstOrDefault() ?? typeSymbol.Locations.FirstOrDefault(),
                        typeSymbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                        method.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)));
                }

                foreach (var result in analysis.UnsupportedProxyBehaviors)
                {
                    var location = result.Attribute.ApplicationSyntaxReference?.GetSyntax(spc.CancellationToken).GetLocation()
                        ?? result.Method.Locations.FirstOrDefault()
                        ?? typeSymbol.Locations.FirstOrDefault();

                    spc.ReportDiagnostic(Diagnostic.Create(
                        UnsupportedProxyBehaviorDescriptor,
                        location,
                        typeSymbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                        result.Method.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                        result.BehaviorName,
                        result.Reason));
                }

                if (!analysis.CanGenerate)
                {
                    return;
                }

                var classHandler = new ClassProxyHandler(compilation);
                classHandler.Execute(spc, typeSymbol, analysis);
            }
        });
    }


    /// <summary>
    /// 当 AutoProxy 目标类型不支持生成代理时抛出的诊断定义
    /// </summary>
    private static readonly DiagnosticDescriptor InvalidAutoProxyTargetDescriptor = new(
        id: "AutoProxy001",
        title: "AutoProxy 目标类型不支持生成代理",
        messageFormat: "类型 {0} 无法生成 AutoProxy 代理：{1}",
        category: "AutoProxyGenerator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);


    /// <summary>
    /// 当 AutoProxy 方法签名不支持生成代理时抛出的诊断定义
    /// </summary>
    private static readonly DiagnosticDescriptor UnsupportedAsyncByRefMethodDescriptor = new(
        id: "AutoProxy002",
        title: "AutoProxy 方法签名不支持生成代理",
        messageFormat: "方法 {0} 不能生成 AutoProxy 代理：Task 或 ValueTask 返回值的方法不支持 ref、out 或 in 参数",
        category: "AutoProxyGenerator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);


    /// <summary>
    /// 当 AutoProxy 方法使用指针或函数指针签名时抛出的诊断定义
    /// </summary>
    private static readonly DiagnosticDescriptor UnsupportedPointerMethodDescriptor = new(
        id: "AutoProxy005",
        title: "AutoProxy 指针成员签名不支持代理",
        messageFormat: "成员 {0} 不能生成 AutoProxy 代理：当前代理代码不支持指针或函数指针参数及返回值",
        category: "AutoProxyGenerator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);


    /// <summary>
    /// 当 AutoProxy 方法按值返回引用结构时抛出的诊断定义
    /// </summary>
    private static readonly DiagnosticDescriptor UnsupportedRefLikeReturnMethodDescriptor = new(
        id: "AutoProxy006",
        title: "AutoProxy 引用结构签名不支持代理",
        messageFormat: "方法 {0} 不能生成 AutoProxy 代理：当前代理运行时管道不支持 ref struct 返回值或 allows ref struct 泛型参数",
        category: "AutoProxyGenerator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);


    /// <summary>
    /// 当 AutoProxy 目标类型继承了无法拦截的接口默认实现方法时抛出的诊断定义
    /// </summary>
    private static readonly DiagnosticDescriptor UnsupportedDefaultInterfaceMethodDescriptor = new(
        id: "AutoProxy003",
        title: "AutoProxy 接口默认实现方法不支持代理",
        messageFormat: "类型 {0} 不能生成 AutoProxy 代理：接口默认实现方法 {1} 无法被代理安全转发，请在目标类型中提供可代理的 public virtual 实现",
        category: "AutoProxyGenerator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);


    /// <summary>
    /// 当代理行为无法在目标方法的实际代理路径执行时抛出的诊断定义
    /// </summary>
    private static readonly DiagnosticDescriptor UnsupportedProxyBehaviorDescriptor = new(
        id: "AutoProxy004",
        title: "AutoProxy 行为与方法代理路径不兼容",
        messageFormat: "类型 {0} 的方法 {1} 上的代理行为 {2} 无法执行：{3}",
        category: "AutoProxyGenerator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);


    /// <summary>
    /// 针对类类型的代理生成处理器 负责生成派生代理类源码
    /// </summary>
    private sealed class ClassProxyHandler
    {

        /// <summary>
        /// 代理方法返回类型类别
        /// </summary>
        private enum ProxyReturnTypeKind
        {
            Void,
            Synchronous,
            Task,
            TaskOfT,
            ValueTask,
            ValueTaskOfT,
            AsyncEnumerable,
            TaskOfAsyncEnumerable,
            ValueTaskOfAsyncEnumerable
        }


        /// <summary>
        /// 保存代理方法返回类型的统一分析结果
        /// </summary>
        private readonly struct ProxyReturnTypeInfo
        {

            /// <summary>
            /// 返回类型类别
            /// </summary>
            public ProxyReturnTypeKind Kind { get; }


            /// <summary>
            /// Task 或 ValueTask 的泛型结果类型
            /// </summary>
            public ITypeSymbol? ResultType { get; }


            /// <summary>
            /// 异步流元素类型
            /// </summary>
            public ITypeSymbol? StreamItemType { get; }


            /// <summary>
            /// 是否属于可等待返回类型
            /// </summary>
            public bool IsAwaitable => Kind is ProxyReturnTypeKind.Task or ProxyReturnTypeKind.TaskOfT or ProxyReturnTypeKind.ValueTask or ProxyReturnTypeKind.ValueTaskOfT or ProxyReturnTypeKind.TaskOfAsyncEnumerable or ProxyReturnTypeKind.ValueTaskOfAsyncEnumerable;


            /// <summary>
            /// 是否属于异步流返回类型
            /// </summary>
            public bool IsAsyncStream => Kind is ProxyReturnTypeKind.AsyncEnumerable or ProxyReturnTypeKind.TaskOfAsyncEnumerable or ProxyReturnTypeKind.ValueTaskOfAsyncEnumerable;


            /// <summary>
            /// 是否包含需要传递给行为管道的返回值
            /// </summary>
            public bool HasReturnValue => Kind is not ProxyReturnTypeKind.Void and not ProxyReturnTypeKind.Task and not ProxyReturnTypeKind.ValueTask;


            /// <summary>
            /// 创建代理方法返回类型分析结果
            /// </summary>
            /// <param name="kind">返回类型类别</param>
            /// <param name="resultType">Task 或 ValueTask 的泛型结果类型</param>
            /// <param name="streamItemType">异步流元素类型</param>
            public ProxyReturnTypeInfo(ProxyReturnTypeKind kind, ITypeSymbol? resultType = null, ITypeSymbol? streamItemType = null)
            {

                Kind = kind;
                ResultType = resultType;
                StreamItemType = streamItemType;

            }

        }


        /// <summary>
        /// 保存单个代理行为及其配置的生成信息
        /// </summary>
        private readonly struct ProxyBehaviorSpec
        {

            /// <summary>
            /// 行为类型名称
            /// </summary>
            public string BehaviorTypeName { get; }


            /// <summary>
            /// 行为配置类型名称
            /// </summary>
            public string? OptionsTypeName { get; }


            /// <summary>
            /// 行为配置初始化表达式
            /// </summary>
            public string? OptionsInitializer { get; }


            /// <summary>
            /// 是否为生成器确认无状态的内置行为
            /// </summary>
            public bool IsBuiltIn { get; }


            /// <summary>
            /// 创建代理行为生成信息
            /// </summary>
            /// <param name="behaviorTypeName">行为类型名称</param>
            /// <param name="optionsTypeName">行为配置类型名称</param>
            /// <param name="optionsInitializer">行为配置初始化表达式</param>
            /// <param name="isBuiltIn">是否为生成器确认无状态的内置行为</param>
            public ProxyBehaviorSpec(string behaviorTypeName, string? optionsTypeName, string? optionsInitializer, bool isBuiltIn)
            {

                BehaviorTypeName = behaviorTypeName;
                OptionsTypeName = optionsTypeName;
                OptionsInitializer = optionsInitializer;
                IsBuiltIn = isBuiltIn;

            }

        }


        private const string ProxyBehaviorAttributeMetadataName = "SourceGenerator.Runtime.Attributes.ProxyBehaviorAttribute";

        private const string LoggingBehaviorMetadataName = "SourceGenerator.Runtime.Pipeline.Behaviors.LoggingBehavior";

        private const string RetryBehaviorMetadataName = "SourceGenerator.Runtime.Pipeline.Behaviors.RetryBehavior";

        private const string ConcurrencyLimitBehaviorMetadataName = "SourceGenerator.Runtime.Pipeline.Behaviors.ConcurrencyLimitBehavior";

        private const string CacheableBehaviorMetadataName = "SourceGenerator.Runtime.Pipeline.Behaviors.CacheableBehavior";

        private const string RetryOptionsMetadataName = "SourceGenerator.Runtime.Options.RetryOptions";

        private const string ConcurrencyLimitOptionsMetadataName = "SourceGenerator.Runtime.Options.ConcurrencyLimitOptions";

        private const string CacheableOptionsMetadataName = "SourceGenerator.Runtime.Options.CacheableOptions";


        private static readonly SymbolDisplayFormat SourceTypeDisplayFormat = SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
            | SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);

        private static readonly SymbolDisplayFormat MethodKeyTypeDisplayFormat = new(globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included, typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces, genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters, miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);


        /// <summary>
        /// 类型文本会裁剪为相对名称的固定导入命名空间
        /// </summary>
        private static readonly string[] ShortenedNamespacePrefixes =
        {
            "System.Collections.Generic",
            "System.Net.Http",
            "System.Threading",
            "System.Threading.Tasks",
            "SourceGenerator.Runtime.Options",
            "SourceGenerator.Runtime.Pipeline",
            "SourceGenerator.Runtime.Pipeline.Behaviors",
            "SourceGenerator.Runtime.Serialization"
        };


        /// <summary>
        /// 当前生成任务使用的编译上下文
        /// </summary>
        private readonly Compilation compilation;


        /// <summary>
        /// 代理生成文件固定导入的命名空间
        /// </summary>
        private readonly INamespaceSymbol[] fixedImportedNamespaces;


        /// <summary>
        /// 当前生成代理所在的命名空间符号
        /// </summary>
        private INamespaceSymbol? generatedNamespace;


        /// <summary>
        /// 代理作用域中可能遮蔽类型引用的业务声明名称
        /// </summary>
        private readonly HashSet<string> scopedTypeNames = new(StringComparer.Ordinal);


        /// <summary>
        /// 当前代理类中已生成的内置行为管道缓存数量
        /// </summary>
        private int cachedBehaviorPipelineCount;


        /// <summary>
        /// 创建类代理生成处理器
        /// </summary>
        /// <param name="compilation">当前编译上下文</param>
        public ClassProxyHandler(Compilation compilation)
        {

            this.compilation = compilation;
            fixedImportedNamespaces = new[]
                {
                    "Microsoft.Extensions.DependencyInjection",
                    "Microsoft.Extensions.Logging",
                    "System",
                    "System.Collections.Generic",
                    "System.Net.Http",
                    "System.Threading",
                    "System.Threading.Tasks",
                    "SourceGenerator.Runtime.Pipeline",
                    "SourceGenerator.Runtime.Pipeline.Behaviors",
                    "SourceGenerator.Runtime.Options",
                    "SourceGenerator.Runtime.Serialization"
                }
                .Select(namespaceName => FindNamespace(compilation.GlobalNamespace, namespaceName))
                .Where(static namespaceSymbol => namespaceSymbol is not null)
                .Select(static namespaceSymbol => namespaceSymbol!)
                .ToArray();

        }


        /// <summary>
        /// 生成指定类型的代理源码并输出到编译上下文
        /// </summary>
        /// <param name="context">源码生成输出上下文</param>
        /// <param name="type">目标类型</param>
        /// <param name="analysis">目标类型的代理生成分析结果</param>
        public void Execute(SourceProductionContext context, INamedTypeSymbol type, AutoProxyAnalysisResult analysis)
        {
            var src = GenerateDerivedProxy(type, analysis);
            var hint = GetSafeHintName(type) + ".g.cs";
            context.AddSource(hint, src);
        }


        /// <summary>
        /// 为指定类生成派生代理类的完整源码
        /// </summary>
        /// <param name="cls">目标类型</param>
        /// <param name="analysis">目标类型的代理生成分析结果</param>
        /// <returns>生成的代理类源码</returns>
        private string GenerateDerivedProxy(INamedTypeSymbol cls, AutoProxyAnalysisResult analysis)
        {
            var ns = cls.ContainingNamespace.IsGlobalNamespace
                ? "NetEngine.Generated"
                : cls.ContainingNamespace.ToDisplayString();
            generatedNamespace = FindNamespace(compilation.GlobalNamespace, ns);
            CollectScopedTypeNames(cls, analysis);

            // 使用包含和不包含 global:: 前缀的完全限定类型名
            var classFull = FormatType(cls).Replace("global::", string.Empty);
            var classLocal = FormatType(cls, ns);
            var proxyName = AutoProxyEligibility.GetProxyTypeName(cls);
            var typeParamsDecl = BuildTypeParametersDecl(cls);
            var typeParamConstraints = BuildTypeParameterConstraints(cls, ns);

            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
            sb.AppendLine("using Microsoft.Extensions.Logging;");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Net.Http;");
            sb.AppendLine("using System.Threading;");
            sb.AppendLine("using System.Threading.Tasks;");
            sb.AppendLine("using SourceGenerator.Runtime.Pipeline;");
            sb.AppendLine("using SourceGenerator.Runtime.Pipeline.Behaviors;");
            sb.AppendLine("using SourceGenerator.Runtime.Options;");
            sb.AppendLine("using SourceGenerator.Runtime.Serialization;");
            sb.AppendLine();
            sb.Append("namespace ").Append(ns).AppendLine(";");
            sb.AppendLine();


            // 代理类继承原始实现类型 只列出需要在代理中显式实现的接口
            var minimalInterfaces = analysis.ExplicitInterfaceMethods
                .Select(static item => item.InterfaceType)
                .Distinct<INamedTypeSymbol>(SymbolEqualityComparer.Default)
                .ToArray();
            var ifaceList = minimalInterfaces.Length == 0 ? string.Empty : ", " + string.Join(", ", minimalInterfaces.Select(interfaceType => FormatType(interfaceType, ns)));
            var proxyAccessibility = AutoProxyEligibility.GetProxyAccessibilityText(cls);
            
            // 使用完全限定的基类类型 避免全局命名空间或嵌套类型的解析问题
            sb.Append(proxyAccessibility).Append(" sealed class ").Append(proxyName).Append(typeParamsDecl).Append(" : ").Append(classLocal).Append(ifaceList).AppendLine();
            
            if (!string.IsNullOrWhiteSpace(typeParamConstraints)) sb.Append(typeParamConstraints);
            
            sb.AppendLine("{")
              .AppendLine("    private readonly " + FormatFrameworkType("System.IServiceProvider") + "? __sp;")
              .AppendLine("    private readonly " + FormatFrameworkType("Microsoft.Extensions.Logging.ILogger") + "? __logger;")
              .AppendLine();

            // 构造函数生成规则 镜像基类公开构造函数 并在必要时添加以 IServiceProvider 开头的重载
            var publicConstructors = cls.Constructors.Where(ctor => ctor.DeclaredAccessibility == Accessibility.Public).ToArray();
            var generatedConstructorSignatures = new HashSet<string>(StringComparer.Ordinal);
            var mirroredConstructorSignatures = new HashSet<string>(
                publicConstructors.Select(ctor => BuildConstructorSignatureKey(ctor.Parameters, prependServiceProvider: false)),
                StringComparer.Ordinal);

            foreach (var ctor in publicConstructors)
            {
                if (!generatedConstructorSignatures.Add(BuildConstructorSignatureKey(ctor.Parameters, prependServiceProvider: false)))
                    continue;

                var paramList = string.Join(", ", ctor.Parameters.Select(p => FormatParameter(p, includeDefault: true, ns)));
                var argList = string.Join(", ", ctor.Parameters.Select(FormatArgument));
                var firstIsSp = ctor.Parameters.FirstOrDefault() is IParameterSymbol { RefKind: RefKind.None } fp && IsType(fp.Type, "System.IServiceProvider");
                var firstSpName = firstIsSp ? EscapeIdentifier(ctor.Parameters[0].Name) : null;
                var canGenerateServiceProviderOverload = !firstIsSp
                    && !mirroredConstructorSignatures.Contains(BuildConstructorSignatureKey(ctor.Parameters, prependServiceProvider: true));

                // 纯粹镜像基类构造函数
                AppendConstructorAttributes(sb, ctor, includeActivatorUtilitiesConstructor: !canGenerateServiceProviderOverload);
                sb.Append("    public ").Append(proxyName).Append('(').Append(paramList).Append(')').AppendLine()
                  .AppendLine("        : base(" + argList + ")")
                  .AppendLine("    {");

                if (firstSpName is not null)
                {
                    sb.AppendLine("        __sp = " + firstSpName + ";");
                    sb.AppendLine("        __logger = __sp?.GetService<" + FormatFrameworkType("Microsoft.Extensions.Logging.ILoggerFactory") + ">()?.CreateLogger(\"ProxyRuntime\");");
                }

                sb.AppendLine("    }")
                  .AppendLine()
                  .AppendLine();
            }

            foreach (var ctor in publicConstructors)
            {
                var firstIsSp = ctor.Parameters.FirstOrDefault() is IParameterSymbol { RefKind: RefKind.None } fp && IsType(fp.Type, "System.IServiceProvider");

                // 如果第一个参数不是 IServiceProvider 则生成以 IServiceProvider 作为首参的重载构造函数
                if (firstIsSp
                    || !generatedConstructorSignatures.Add(BuildConstructorSignatureKey(ctor.Parameters, prependServiceProvider: true)))
                    continue;

                var paramList = string.Join(", ", ctor.Parameters.Select(p => FormatParameter(p, includeDefault: true, ns)));
                var argList = string.Join(", ", ctor.Parameters.Select(FormatArgument));
                var withSpParams = ctor.Parameters.Length == 0
                    ? FormatFrameworkType("System.IServiceProvider") + " __serviceProvider"
                    : FormatFrameworkType("System.IServiceProvider") + " __serviceProvider, " + paramList;

                AppendConstructorAttributes(sb, ctor, includeActivatorUtilitiesConstructor: true);
                sb.Append("    public ").Append(proxyName).Append('(').Append(withSpParams).Append(')').AppendLine()
                  .AppendLine("        : base(" + argList + ")")
                  .AppendLine("    {")
                  .AppendLine("        __sp = __serviceProvider;")
                  .AppendLine("        __logger = __sp.GetService<" + FormatFrameworkType("Microsoft.Extensions.Logging.ILoggerFactory") + ">()?.CreateLogger(\"ProxyRuntime\");")
                  .AppendLine("    }")
                  .AppendLine()
                  .AppendLine();
            }

            // 为当前类型直接声明的方法和有效继承代理方法生成重写实现
            foreach (var method in analysis.EffectiveProxyMethods)
            {
                AppendDerivedOverride(sb, cls, method, classFull, callTarget: "base", ns);
            }

            // 为接口成员生成显式实现 使通过接口调用时也能被拦截
            foreach (var explicitInterfaceMethod in analysis.ExplicitInterfaceMethods)
            {
                AppendExplicitInterfaceMethod(sb, cls, explicitInterfaceMethod.InterfaceType, explicitInterfaceMethod.Method, explicitInterfaceMethod.ImplementationMethod, classFull, ns);
            }

            sb.AppendLine("}");

            return sb.ToString();
        }


        /// <summary>
        /// 生成能够在完整枚举和提前释放时正确结束同步行为生命周期的异步流包装器
        /// </summary>
        /// <param name="sb">目标源码构建器</param>
        /// <param name="itemType">异步流元素类型</param>
        /// <param name="sourceExpression">被包装异步流表达式</param>
        /// <param name="sourceIsParameter">是否通过包装器参数接收异步流</param>
        private void AppendAsyncStreamWrapper(StringBuilder sb, string itemType, string sourceExpression, bool sourceIsParameter)
        {

            var sourceParameter = sourceIsParameter ? FormatFrameworkType("System.Collections.Generic.IAsyncEnumerable`1") + "<" + itemType + "> __s, " : string.Empty;

            sb.AppendLine("        async " + FormatFrameworkType("System.Collections.Generic.IAsyncEnumerable`1") + "<" + itemType + "> __streamWrapper(" + sourceParameter + "[global::System.Runtime.CompilerServices.EnumeratorCancellation] " + FormatFrameworkType("System.Threading.CancellationToken") + " __enumerationCancellationToken = default){");
            sb.AppendLine("            var __ctx = __createContext();");
            var validation = new StringBuilder();
            AppendSynchronousBehaviorValidation(validation);
            foreach (var line in validation.ToString().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                sb.Append("    ").AppendLine(line);
            sb.AppendLine("            long __enumeratedCount = 0;");
            sb.AppendLine("            var __completedNaturally = false;");

            sb.AppendLine("            try { " + FormatFrameworkType("SourceGenerator.Runtime.Pipeline.ProxyRuntime") + ".InvokeSynchronousBefore(__ctx, ref __enteredBehaviorCount); } catch (" + FormatFrameworkType("System.Exception") + " __ex) { " + FormatFrameworkType("SourceGenerator.Runtime.Pipeline.ProxyRuntime") + ".InvokeSynchronousException(__ctx, ref __enteredBehaviorCount, __ex); throw; }");

            sb.AppendLine("            " + FormatFrameworkType("System.Collections.Generic.List`1") + "<object?>? __capturedItems = null;");
            sb.AppendLine("            try");
            sb.AppendLine("            {");
            sb.AppendLine("                for (var __behaviorIndex = 0; __behaviorIndex < __filters.Count; __behaviorIndex++)");
            sb.AppendLine("                {");
            sb.AppendLine("                    var __f = __filters[__behaviorIndex];");
            sb.AppendLine("                    if (__f is " + FormatFrameworkType("SourceGenerator.Runtime.Pipeline.IAsyncStreamResultCaptureBehavior") + " __captureBehavior && __captureBehavior.ShouldCaptureAsyncStreamItems(__ctx))");
            sb.AppendLine("                    {");
            sb.AppendLine("                        __capturedItems = new " + FormatFrameworkType("System.Collections.Generic.List`1") + "<object?>(" + FormatFrameworkType("SourceGenerator.Runtime.Pipeline.AsyncStreamResultSnapshot") + ".DefaultCaptureLimit);");
            sb.AppendLine("                        break;");
            sb.AppendLine("                    }");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            sb.AppendLine("            catch (" + FormatFrameworkType("System.Exception") + " __ex)");
            sb.AppendLine("            {");
            sb.AppendLine("                " + FormatFrameworkType("SourceGenerator.Runtime.Pipeline.ProxyRuntime") + ".InvokeSynchronousException(__ctx, ref __enteredBehaviorCount, __ex);");
            sb.AppendLine("                throw;");
            sb.AppendLine("            }");

            sb.AppendLine("            " + FormatFrameworkType("System.Collections.Generic.IAsyncEnumerator`1") + "<" + itemType + "> __e;");
            sb.AppendLine("            try { __e = " + sourceExpression + ".GetAsyncEnumerator(__enumerationCancellationToken); } catch (" + FormatFrameworkType("System.Exception") + " __ex) { " + FormatFrameworkType("SourceGenerator.Runtime.Pipeline.ProxyRuntime") + ".InvokeSynchronousException(__ctx, ref __enteredBehaviorCount, __ex); throw; }");
            sb.AppendLine("            var __faulted = false;");
            sb.AppendLine("            " + FormatFrameworkType("System.Exception") + "? __primaryException = null;");
            sb.AppendLine("            try");
            sb.AppendLine("            {");
            sb.AppendLine("                while (true)");
            sb.AppendLine("                {");
            sb.AppendLine("                    bool __moved;");
            sb.AppendLine("                    try { __moved = await __e.MoveNextAsync(); } catch (" + FormatFrameworkType("System.Exception") + " __ex) { __primaryException = __ex; __faulted = true; " + FormatFrameworkType("SourceGenerator.Runtime.Pipeline.ProxyRuntime") + ".InvokeSynchronousException(__ctx, ref __enteredBehaviorCount, __ex); throw; }");
            sb.AppendLine("                    if (!__moved) { __completedNaturally = true; break; }");
            sb.AppendLine("                    " + itemType + " __item;");
            sb.AppendLine("                    try { __item = __e.Current; } catch (" + FormatFrameworkType("System.Exception") + " __ex) { __primaryException = __ex; __faulted = true; " + FormatFrameworkType("SourceGenerator.Runtime.Pipeline.ProxyRuntime") + ".InvokeSynchronousException(__ctx, ref __enteredBehaviorCount, __ex); throw; }");
            sb.AppendLine("                    __enumeratedCount++;");
            sb.AppendLine("                    if (__capturedItems is not null && __capturedItems.Count < " + FormatFrameworkType("SourceGenerator.Runtime.Pipeline.AsyncStreamResultSnapshot") + ".DefaultCaptureLimit)");
            sb.AppendLine("                    {");
            sb.AppendLine("                        __capturedItems.Add(" + FormatFrameworkType("SourceGenerator.Runtime.Serialization.JsonUtil") + ".CreateSnapshotValue(__item));");
            sb.AppendLine("                    }");
            sb.AppendLine("                    yield return __item;");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            sb.AppendLine("            finally");
            sb.AppendLine("            {");
            sb.AppendLine("                try { await __e.DisposeAsync(); }");
            sb.AppendLine("                catch (" + FormatFrameworkType("System.Exception") + " __disposeException)");
            sb.AppendLine("                {");
            sb.AppendLine("                    if (__primaryException is null)");
            sb.AppendLine("                    {");
            sb.AppendLine("                        __faulted = true;");
            sb.AppendLine("                        " + FormatFrameworkType("SourceGenerator.Runtime.Pipeline.ProxyRuntime") + ".InvokeSynchronousException(__ctx, ref __enteredBehaviorCount, __disposeException);");
            sb.AppendLine("                        throw;");
            sb.AppendLine("                    }");
            sb.AppendLine("                    __ctx.Logger?.LogError(__disposeException, \"异步流枚举器释放失败，已保留原始枚举异常 {PrimaryExceptionType}\", __primaryException.GetType().FullName);");
            sb.AppendLine("                }");
            sb.AppendLine("                if (!__faulted)");
            sb.AppendLine("                {");
            sb.AppendLine("                    var __capturedItemCount = __capturedItems?.Count ?? 0;");
            sb.AppendLine("                    var __capturedItemSnapshot = __capturedItems is null ? (" + FormatFrameworkType("System.Collections.Generic.IReadOnlyList`1") + "<object?>)" + FormatFrameworkType("System.Array") + ".Empty<object?>() : __capturedItems;");
            sb.AppendLine("                    var __streamResult = new " + FormatFrameworkType("SourceGenerator.Runtime.Pipeline.AsyncStreamResultSnapshot") + " { EnumeratedCount = __enumeratedCount, CompletedNaturally = __completedNaturally, Truncated = __enumeratedCount > __capturedItemCount, CapturedItems = __capturedItemSnapshot };");
            sb.AppendLine("                    try { " + FormatFrameworkType("SourceGenerator.Runtime.Pipeline.ProxyRuntime") + ".InvokeSynchronousAfter(__ctx, ref __enteredBehaviorCount, __streamResult); } catch (" + FormatFrameworkType("System.Exception") + " __ex) { " + FormatFrameworkType("SourceGenerator.Runtime.Pipeline.ProxyRuntime") + ".InvokeSynchronousException(__ctx, ref __enteredBehaviorCount, __ex); throw; }");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            sb.AppendLine("        }");

        }


        /// <summary>
        /// 等待异步准备结果并返回在实际枚举时启动行为的流包装器
        /// </summary>
        /// <param name="sb">目标源码构建器</param>
        /// <param name="itemType">异步流元素类型</param>
        /// <param name="callExpression">返回异步流的实际方法调用表达式</param>
        /// <param name="returnsValueTask">是否返回 ValueTask</param>
        private void AppendAsyncStreamPreparationWrapper(StringBuilder sb, string itemType, string callExpression, bool returnsValueTask)
        {

            var awaitableType = returnsValueTask ? FormatFrameworkType("System.Threading.Tasks.ValueTask`1") : FormatFrameworkType("System.Threading.Tasks.Task`1");
            var wrapperName = returnsValueTask ? "__valueTaskWrapper" : "__taskWrapper";

            sb.Append("        async ").Append(awaitableType).Append("<" + FormatFrameworkType("System.Collections.Generic.IAsyncEnumerable`1") + "<").Append(itemType).Append(">> ").Append(wrapperName).AppendLine("()");
            sb.AppendLine("        {");
            sb.AppendLine("            var __s = await " + callExpression + ";");
            sb.AppendLine("            return __streamWrapper(__s);");
            sb.AppendLine("        }");
            sb.AppendLine("        return " + wrapperName + "();");

        }


        /// <summary>
        /// 生成同步行为兼容性验证并直接复用原行为列表
        /// </summary>
        /// <param name="sb">目标源码构建器</param>
        private void AppendSynchronousBehaviorValidation(StringBuilder sb)
        {

            sb.AppendLine("        " + FormatFrameworkType("System.Collections.Generic.IReadOnlyList`1") + "<" + FormatFrameworkType("SourceGenerator.Runtime.Pipeline.IInvocationAsyncBehavior") + "> __filters = __ctx.Behaviors;");
            sb.AppendLine("        for (var __behaviorIndex = 0; __behaviorIndex < __filters.Count; __behaviorIndex++)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (__filters[__behaviorIndex] is not " + FormatFrameworkType("SourceGenerator.Runtime.Pipeline.IInvocationBehavior") + ")");
            sb.AppendLine("                throw new " + FormatFrameworkType("System.InvalidOperationException") + "($\"行为 {__filters[__behaviorIndex].GetType().FullName} 不支持当前代理方法\");");
            sb.AppendLine("        }");
            sb.AppendLine("        var __enteredBehaviorCount = 0;");

        }


        /// <summary>
        /// 分析代理方法的返回类型
        /// </summary>
        /// <param name="method">待分析的代理方法</param>
        /// <returns>统一的返回类型分析结果</returns>
        private static ProxyReturnTypeInfo GetProxyReturnTypeInfo(IMethodSymbol method)
        {

            if (method.ReturnsVoid)
                return new ProxyReturnTypeInfo(ProxyReturnTypeKind.Void);

            if (method.ReturnType is not INamedTypeSymbol returnType)
                return new ProxyReturnTypeInfo(ProxyReturnTypeKind.Synchronous);

            if (TryGetAsyncEnumerableItemType(returnType, out var streamItemType))
                return new ProxyReturnTypeInfo(ProxyReturnTypeKind.AsyncEnumerable, streamItemType: streamItemType);

            if (IsNamedType(returnType, "System.Threading.Tasks", "Task", 0))
                return new ProxyReturnTypeInfo(ProxyReturnTypeKind.Task);

            if (IsNamedType(returnType, "System.Threading.Tasks", "Task", 1))
            {
                var resultType = returnType.TypeArguments[0];
                return TryGetAsyncEnumerableItemType(resultType, out streamItemType)
                    ? new ProxyReturnTypeInfo(ProxyReturnTypeKind.TaskOfAsyncEnumerable, resultType, streamItemType)
                    : new ProxyReturnTypeInfo(ProxyReturnTypeKind.TaskOfT, resultType);
            }

            if (IsNamedType(returnType, "System.Threading.Tasks", "ValueTask", 0))
                return new ProxyReturnTypeInfo(ProxyReturnTypeKind.ValueTask);

            if (IsNamedType(returnType, "System.Threading.Tasks", "ValueTask", 1))
            {
                var resultType = returnType.TypeArguments[0];
                return TryGetAsyncEnumerableItemType(resultType, out streamItemType)
                    ? new ProxyReturnTypeInfo(ProxyReturnTypeKind.ValueTaskOfAsyncEnumerable, resultType, streamItemType)
                    : new ProxyReturnTypeInfo(ProxyReturnTypeKind.ValueTaskOfT, resultType);
            }

            return new ProxyReturnTypeInfo(ProxyReturnTypeKind.Synchronous);

        }


        /// <summary>
        /// 尝试获取异步流的元素类型
        /// </summary>
        /// <param name="type">待检查的类型</param>
        /// <param name="itemType">识别到的异步流元素类型</param>
        /// <returns>如果类型是异步流则返回 true</returns>
        private static bool TryGetAsyncEnumerableItemType(ITypeSymbol type, out ITypeSymbol? itemType)
        {

            itemType = null;

            if (type is not INamedTypeSymbol namedType || !IsNamedType(namedType, "System.Collections.Generic", "IAsyncEnumerable", 1))
                return false;

            itemType = namedType.TypeArguments[0];
            return true;

        }


        /// <summary>
        /// 为可重写的实例方法生成派生类中的 override 方法实现 并注入日志和行为管道
        /// </summary>
        /// <param name="sb">目标源码构建器</param>
        /// <param name="targetType">当前代理目标类型</param>
        /// <param name="method">待生成 override 的方法</param>
        /// <param name="typeFullName">当前代理目标类型完整名称</param>
        /// <param name="callTarget">原始方法调用目标</param>
        /// <param name="currentNamespace">当前生成代码命名空间</param>
        private void AppendDerivedOverride(StringBuilder sb, INamedTypeSymbol targetType, IMethodSymbol method, string typeFullName, string callTarget, string currentNamespace)
        {

            var returnTypeInfo = GetProxyReturnTypeInfo(method);
            var returnType = FormatType(method.ReturnType, currentNamespace);
            var methodName = EscapeIdentifier(method.Name);
            var typeParams = method.TypeParameters.Length > 0 ? "<" + string.Join(", ", method.TypeParameters.Select(tp => EscapeIdentifier(tp.Name))) + ">" : string.Empty;
            var paramList = string.Join(", ", method.Parameters.Select(p => FormatParameter(p, includeDefault: true, currentNamespace)));
            var argList = string.Join(", ", method.Parameters.Select(FormatArgument));
            var isByRefReturn = method.ReturnsByRef || method.ReturnsByRefReadonly;
            var hasByRefAny = isByRefReturn || method.Parameters.Any(p => p.RefKind != RefKind.None || p.Type.IsRefLikeType);
            var needsAsync = hasByRefAny && returnTypeInfo.IsAwaitable;
            var effectiveAttributes = method.GetAttributes();
            var behaviorSpecs = GetBehaviorSpecs(effectiveAttributes);
            PrepareBehaviorPipeline(sb, behaviorSpecs, out var behaviorExpression, out var optionsSetters);

            var sigReturnType = method.ReturnsVoid
                ? "void"
                : isByRefReturn
                    ? (method.ReturnsByRefReadonly ? "ref readonly " : "ref ") + returnType
                    : returnType;
            var accessibilityText = GetOverrideAccessibilityText(targetType, method);
            sb.Append("    ").Append(accessibilityText).Append(" override ").Append(needsAsync ? "async " : string.Empty).Append(sigReturnType).Append(' ').Append(methodName).Append(typeParams)
              .Append('(').Append(paramList).Append(')').AppendLine();
            AppendMethodNullableConstraints(sb, method);
            sb.AppendLine("    {");

            var callExpression = callTarget + "." + methodName + typeParams + "(" + argList + ")";
            AppendProxyMethodBody(sb, targetType, method, typeFullName, currentNamespace, returnTypeInfo, effectiveAttributes, behaviorExpression, optionsSetters, callExpression);

        }


        /// <summary>
        /// 获取方法在当前派生代理中需要使用的 override 访问修饰符
        /// </summary>
        /// <param name="targetType">当前代理目标类型</param>
        /// <param name="method">待重写的方法</param>
        /// <returns>适用于生成 override 的访问修饰符</returns>
        private static string GetOverrideAccessibilityText(INamedTypeSymbol targetType, IMethodSymbol method)
        {

            if (method.DeclaredAccessibility == Accessibility.ProtectedOrInternal
                && !SymbolEqualityComparer.Default.Equals(targetType.ContainingAssembly, method.ContainingAssembly))
                return "protected";

            return method.DeclaredAccessibility switch
            {
                Accessibility.Public => "public",
                Accessibility.Protected => "protected",
                Accessibility.Internal => "internal",
                Accessibility.ProtectedOrInternal => "protected internal",
                Accessibility.ProtectedAndInternal => "private protected",
                _ => "public"
            };

        }


        /// <summary>
        /// 为接口方法生成在代理类中的显式接口实现 并注入行为管道和日志逻辑
        /// </summary>
        private void AppendExplicitInterfaceMethod(StringBuilder sb, INamedTypeSymbol cls, INamedTypeSymbol iface, IMethodSymbol method, IMethodSymbol? impl, string typeFullName, string currentNamespace)
        {

            var returnTypeInfo = GetProxyReturnTypeInfo(method);
            var returnType = FormatType(method.ReturnType, currentNamespace);
            var ifaceDisplay = FormatType(iface, currentNamespace);
            var methodName = EscapeIdentifier(method.Name);
            var typeParams = method.TypeParameters.Length > 0 ? "<" + string.Join(", ", method.TypeParameters.Select(tp => EscapeIdentifier(tp.Name))) + ">" : string.Empty;
            var paramList = string.Join(", ", method.Parameters.Select(p => FormatParameter(p, includeDefault: false, currentNamespace)));
            var argList = string.Join(", ", method.Parameters.Select(FormatArgument));
            var isByRefReturn = method.ReturnsByRef || method.ReturnsByRefReadonly;
            var hasByRefAny = isByRefReturn || method.Parameters.Any(p => p.RefKind != RefKind.None || p.Type.IsRefLikeType);
            var needsAsync = hasByRefAny && returnTypeInfo.IsAwaitable;
            IEnumerable<AttributeData> implementationAttributes = impl is null ? Array.Empty<AttributeData>() : impl.GetAttributes();
            var effectiveAttributes = method.GetAttributes().Concat(implementationAttributes).ToArray();
            var behaviorSpecs = GetBehaviorSpecs(effectiveAttributes);
            PrepareBehaviorPipeline(sb, behaviorSpecs, out var behaviorExpression, out var optionsSetters);
            var sigReturnType = method.ReturnsVoid
                ? "void"
                : isByRefReturn
                    ? (method.ReturnsByRefReadonly ? "ref readonly " : "ref ") + returnType
                    : returnType;
            sb.Append("    ").Append(needsAsync ? "async " : string.Empty).Append(sigReturnType).Append(' ').Append(ifaceDisplay).Append('.').Append(methodName).Append(typeParams)
              .Append('(').Append(paramList).Append(')').AppendLine();
            AppendMethodNullableConstraints(sb, method);
            sb.AppendLine("    {");

            var callExpression = "base." + methodName + typeParams + "(" + argList + ")";
            AppendProxyMethodBody(sb, cls, method, typeFullName, currentNamespace, returnTypeInfo, effectiveAttributes, behaviorExpression, optionsSetters, callExpression);

        }


        /// <summary>
        /// 从当前方法的有效特性中收集代理行为生成信息
        /// </summary>
        /// <param name="attributes">当前方法实际生效的特性列表</param>
        /// <returns>按声明顺序排列的代理行为生成信息</returns>
        private IReadOnlyList<ProxyBehaviorSpec> GetBehaviorSpecs(IEnumerable<AttributeData> attributes)
        {

            var behaviorSpecs = new List<ProxyBehaviorSpec>();

            foreach (var attribute in attributes)
            {
                if (TryGetBehaviorSpec(attribute, out var behaviorSpec))
                    behaviorSpecs.Add(behaviorSpec);
            }

            return behaviorSpecs;

        }


        /// <summary>
        /// 生成派生重写和显式接口实现共用的代理方法调用体
        /// </summary>
        /// <param name="sb">目标源码构建器</param>
        /// <param name="targetType">当前代理目标类型</param>
        /// <param name="method">待生成代理调用体的方法</param>
        /// <param name="typeFullName">当前代理目标类型完整名称</param>
        /// <param name="currentNamespace">当前生成代码命名空间</param>
        /// <param name="returnTypeInfo">代理方法返回类型分析结果</param>
        /// <param name="effectiveAttributes">当前方法实际生效的特性列表</param>
        /// <param name="behaviorExpression">代理方法使用的行为列表表达式</param>
        /// <param name="optionsSetters">代理方法使用的配置写入代码</param>
        /// <param name="callExpression">原始方法调用表达式</param>
        private void AppendProxyMethodBody(StringBuilder sb, INamedTypeSymbol targetType, IMethodSymbol method, string typeFullName, string currentNamespace, ProxyReturnTypeInfo returnTypeInfo, IReadOnlyList<AttributeData> effectiveAttributes, string behaviorExpression, IReadOnlyList<string> optionsSetters, string callExpression)
        {

            var contextBuilder = returnTypeInfo.IsAsyncStream ? new StringBuilder() : sb;
            var requiresArgumentsSnapshot = effectiveAttributes.Any(AutoProxyEligibility.RequiresArgumentsSnapshot);
            AppendArgumentsSnapshot(contextBuilder, method, currentNamespace, requiresArgumentsSnapshot);

            var requiresArgumentsKey = effectiveAttributes.Any(AutoProxyEligibility.RequiresArgumentsKey);
            AppendArgumentsKeySnapshot(contextBuilder, method, requiresArgumentsKey);

            contextBuilder.AppendLine("        var __logMethod = \"" + typeFullName + "\" + \"." + method.Name + "\";");
            AppendMethodKey(contextBuilder, targetType, method, typeFullName);
            contextBuilder.AppendLine("        var __behaviors = " + behaviorExpression + ";");

            var hasReturnValue = returnTypeInfo.HasReturnValue;
            var allowReturnSerialization = hasReturnValue && IsAllowReturnSerialization(method, returnTypeInfo);
            var cancellationTokenExpression = GetCancellationTokenExpression(method);

            contextBuilder.AppendLine("        var __ctx = new " + FormatFrameworkType("SourceGenerator.Runtime.Pipeline.InvocationContext") + " { Method = __logMethod, MethodKey = __methodKey, Args = __argsObj, ArgumentsKey = __argumentsKey, IsArgumentsKeyComplete = __isArgumentsKeyComplete, CancellationToken = " + cancellationTokenExpression + ", TraceId = " + FormatFrameworkType("System.Guid") + ".CreateVersion7(), HasReturnValue = " + (hasReturnValue ? "true" : "false") + ", AllowReturnSerialization = " + (allowReturnSerialization ? "true" : "false") + ", ServiceProvider = __sp, Logger = __logger, Behaviors = __behaviors };");

            if (optionsSetters.Count > 0)
                contextBuilder.AppendLine("        " + string.Join("\n        ", optionsSetters));

            if (returnTypeInfo.IsAsyncStream)
            {
                sb.AppendLine("        " + FormatFrameworkType("SourceGenerator.Runtime.Pipeline.InvocationContext") + " __createContext()");
                sb.AppendLine("        {");
                foreach (var line in contextBuilder.ToString().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                    sb.Append("    ").AppendLine(line);
                sb.AppendLine("            return __ctx;");
                sb.AppendLine("        }");
            }

            AppendProxyInvocation(sb, method, currentNamespace, returnTypeInfo, callExpression, requiresArgumentsSnapshot);
            sb.AppendLine("    }").AppendLine().AppendLine();

        }


        /// <summary>
        /// 按返回类型和参数签名生成统一的目标方法调用与行为生命周期代码
        /// </summary>
        /// <param name="sb">目标源码构建器</param>
        /// <param name="method">待生成代理调用的方法</param>
        /// <param name="currentNamespace">当前生成代码命名空间</param>
        /// <param name="returnTypeInfo">代理方法返回类型分析结果</param>
        /// <param name="callExpression">原始方法调用表达式</param>
        /// <param name="requiresArgumentsSnapshot">是否需要维护参数快照</param>
        private void AppendProxyInvocation(StringBuilder sb, IMethodSymbol method, string currentNamespace, ProxyReturnTypeInfo returnTypeInfo, string callExpression, bool requiresArgumentsSnapshot)
        {

            var isTask = returnTypeInfo.Kind == ProxyReturnTypeKind.Task;
            var isGenericTask = returnTypeInfo.Kind is ProxyReturnTypeKind.TaskOfT or ProxyReturnTypeKind.TaskOfAsyncEnumerable;
            var isValueTask = returnTypeInfo.Kind == ProxyReturnTypeKind.ValueTask;
            var isGenericValueTask = returnTypeInfo.Kind is ProxyReturnTypeKind.ValueTaskOfT or ProxyReturnTypeKind.ValueTaskOfAsyncEnumerable;
            var isAsyncEnumerable = returnTypeInfo.Kind == ProxyReturnTypeKind.AsyncEnumerable;
            var isTaskOfAsyncEnumerable = returnTypeInfo.Kind == ProxyReturnTypeKind.TaskOfAsyncEnumerable;
            var isValueTaskOfAsyncEnumerable = returnTypeInfo.Kind == ProxyReturnTypeKind.ValueTaskOfAsyncEnumerable;
            var isByRefReturn = method.ReturnsByRef || method.ReturnsByRefReadonly;
            var hasByRefSignature = isByRefReturn || method.Parameters.Any(parameter => parameter.RefKind != RefKind.None || parameter.Type.IsRefLikeType);

            if (hasByRefSignature || returnTypeInfo.IsAsyncStream)
            {
                if (!returnTypeInfo.IsAsyncStream)
                    AppendSynchronousBehaviorValidation(sb);

                if (isAsyncEnumerable)
                {
                    var itemType = FormatType(returnTypeInfo.StreamItemType!, currentNamespace);
                    AppendAsyncStreamWrapper(sb, itemType, callExpression, sourceIsParameter: false);
                    sb.AppendLine("        return __streamWrapper();");
                    return;
                }

                if (isTask)
                {
                    var updateSnippet = BuildArgsUpdateSnippet(method, requiresArgumentsSnapshot, includeOutParameters: true);
                    var exceptionUpdateSnippet = BuildArgsUpdateSnippet(method, requiresArgumentsSnapshot, includeOutParameters: false);

                    sb.AppendLine("        try");
                    sb.AppendLine("        {");
                    sb.AppendLine("            " + FormatFrameworkType("SourceGenerator.Runtime.Pipeline.ProxyRuntime") + ".InvokeSynchronousBefore(__ctx, ref __enteredBehaviorCount);");
                    sb.AppendLine("            await " + callExpression + ";");
                    if (!string.IsNullOrEmpty(updateSnippet)) sb.AppendLine("            " + updateSnippet);
                    sb.AppendLine("            " + FormatFrameworkType("SourceGenerator.Runtime.Pipeline.ProxyRuntime") + ".InvokeSynchronousAfter(__ctx, ref __enteredBehaviorCount, null);");
                    sb.AppendLine("        }");
                    sb.AppendLine("        catch (" + FormatFrameworkType("System.Exception") + " __ex)");
                    sb.AppendLine("        {");
                    if (!string.IsNullOrEmpty(exceptionUpdateSnippet)) sb.AppendLine("            " + exceptionUpdateSnippet);
                    sb.AppendLine("            " + FormatFrameworkType("SourceGenerator.Runtime.Pipeline.ProxyRuntime") + ".InvokeSynchronousException(__ctx, ref __enteredBehaviorCount, __ex);");
                    sb.AppendLine("            throw;");
                    sb.AppendLine("        }");
                }
                else if (isTaskOfAsyncEnumerable)
                {
                    var itemType = FormatType(returnTypeInfo.StreamItemType!, currentNamespace);
                    AppendAsyncStreamWrapper(sb, itemType, "__s", sourceIsParameter: true);
                    AppendAsyncStreamPreparationWrapper(sb, itemType, callExpression, returnsValueTask: false);
                }
                else if (isGenericTask)
                {
                    var updateSnippet = BuildArgsUpdateSnippet(method, requiresArgumentsSnapshot, includeOutParameters: true);
                    var exceptionUpdateSnippet = BuildArgsUpdateSnippet(method, requiresArgumentsSnapshot, includeOutParameters: false);

                    sb.AppendLine("        try");
                    sb.AppendLine("        {");
                    sb.AppendLine("            " + FormatFrameworkType("SourceGenerator.Runtime.Pipeline.ProxyRuntime") + ".InvokeSynchronousBefore(__ctx, ref __enteredBehaviorCount);");
                    sb.AppendLine("            var __res = await " + callExpression + ";");
                    if (!string.IsNullOrEmpty(updateSnippet)) sb.AppendLine("            " + updateSnippet);
                    sb.AppendLine("            " + FormatFrameworkType("SourceGenerator.Runtime.Pipeline.ProxyRuntime") + ".InvokeSynchronousAfter(__ctx, ref __enteredBehaviorCount, __res);");
                    sb.AppendLine("            return __res;");
                    sb.AppendLine("        }");
                    sb.AppendLine("        catch (" + FormatFrameworkType("System.Exception") + " __ex)");
                    sb.AppendLine("        {");
                    if (!string.IsNullOrEmpty(exceptionUpdateSnippet)) sb.AppendLine("            " + exceptionUpdateSnippet);
                    sb.AppendLine("            " + FormatFrameworkType("SourceGenerator.Runtime.Pipeline.ProxyRuntime") + ".InvokeSynchronousException(__ctx, ref __enteredBehaviorCount, __ex);");
                    sb.AppendLine("            throw;");
                    sb.AppendLine("        }");
                }
                else if (isValueTaskOfAsyncEnumerable)
                {
                    var itemType = FormatType(returnTypeInfo.StreamItemType!, currentNamespace);
                    AppendAsyncStreamWrapper(sb, itemType, "__s", sourceIsParameter: true);
                    AppendAsyncStreamPreparationWrapper(sb, itemType, callExpression, returnsValueTask: true);
                }
                else if (isValueTask)
                {
                    var updateSnippet = BuildArgsUpdateSnippet(method, requiresArgumentsSnapshot, includeOutParameters: true);
                    var exceptionUpdateSnippet = BuildArgsUpdateSnippet(method, requiresArgumentsSnapshot, includeOutParameters: false);

                    sb.AppendLine("        try");
                    sb.AppendLine("        {");
                    sb.AppendLine("            " + FormatFrameworkType("SourceGenerator.Runtime.Pipeline.ProxyRuntime") + ".InvokeSynchronousBefore(__ctx, ref __enteredBehaviorCount);");
                    sb.AppendLine("            await " + callExpression + ";");
                    if (!string.IsNullOrEmpty(updateSnippet)) sb.AppendLine("            " + updateSnippet);
                    sb.AppendLine("            " + FormatFrameworkType("SourceGenerator.Runtime.Pipeline.ProxyRuntime") + ".InvokeSynchronousAfter(__ctx, ref __enteredBehaviorCount, null);");
                    sb.AppendLine("            return;");
                    sb.AppendLine("        }");
                    sb.AppendLine("        catch (" + FormatFrameworkType("System.Exception") + " __ex)");
                    sb.AppendLine("        {");
                    if (!string.IsNullOrEmpty(exceptionUpdateSnippet)) sb.AppendLine("            " + exceptionUpdateSnippet);
                    sb.AppendLine("            " + FormatFrameworkType("SourceGenerator.Runtime.Pipeline.ProxyRuntime") + ".InvokeSynchronousException(__ctx, ref __enteredBehaviorCount, __ex);");
                    sb.AppendLine("            throw;");
                    sb.AppendLine("        }");
                }
                else if (isGenericValueTask)
                {
                    var updateSnippet = BuildArgsUpdateSnippet(method, requiresArgumentsSnapshot, includeOutParameters: true);
                    var exceptionUpdateSnippet = BuildArgsUpdateSnippet(method, requiresArgumentsSnapshot, includeOutParameters: false);

                    sb.AppendLine("        try");
                    sb.AppendLine("        {");
                    sb.AppendLine("            " + FormatFrameworkType("SourceGenerator.Runtime.Pipeline.ProxyRuntime") + ".InvokeSynchronousBefore(__ctx, ref __enteredBehaviorCount);");
                    sb.AppendLine("            var __res = await " + callExpression + ";");
                    if (!string.IsNullOrEmpty(updateSnippet)) sb.AppendLine("            " + updateSnippet);
                    sb.AppendLine("            " + FormatFrameworkType("SourceGenerator.Runtime.Pipeline.ProxyRuntime") + ".InvokeSynchronousAfter(__ctx, ref __enteredBehaviorCount, __res);");
                    sb.AppendLine("            return __res;");
                    sb.AppendLine("        }");
                    sb.AppendLine("        catch (" + FormatFrameworkType("System.Exception") + " __ex)");
                    sb.AppendLine("        {");
                    if (!string.IsNullOrEmpty(exceptionUpdateSnippet)) sb.AppendLine("            " + exceptionUpdateSnippet);
                    sb.AppendLine("            " + FormatFrameworkType("SourceGenerator.Runtime.Pipeline.ProxyRuntime") + ".InvokeSynchronousException(__ctx, ref __enteredBehaviorCount, __ex);");
                    sb.AppendLine("            throw;");
                    sb.AppendLine("        }");
                }
                else if (isByRefReturn)
                {
                    var updateSnippet = BuildArgsUpdateSnippet(method, requiresArgumentsSnapshot, includeOutParameters: true);
                    var exceptionUpdateSnippet = BuildArgsUpdateSnippet(method, requiresArgumentsSnapshot, includeOutParameters: false);
                    var refLocalModifier = method.ReturnsByRefReadonly ? "ref readonly var" : "ref var";
                    sb.AppendLine("        try { " + FormatFrameworkType("SourceGenerator.Runtime.Pipeline.ProxyRuntime") + ".InvokeSynchronousBefore(__ctx, ref __enteredBehaviorCount); " + refLocalModifier + " __ret = ref " + callExpression + "; var __snap = __ret; " + updateSnippet + " " + FormatFrameworkType("SourceGenerator.Runtime.Pipeline.ProxyRuntime") + ".InvokeSynchronousAfter(__ctx, ref __enteredBehaviorCount, __snap); return ref __ret; } catch (" + FormatFrameworkType("System.Exception") + " __ex) { " + exceptionUpdateSnippet + " " + FormatFrameworkType("SourceGenerator.Runtime.Pipeline.ProxyRuntime") + ".InvokeSynchronousException(__ctx, ref __enteredBehaviorCount, __ex); throw; }");
                }
                else if (method.ReturnsVoid)
                {
                    var updateSnippet = BuildArgsUpdateSnippet(method, requiresArgumentsSnapshot, includeOutParameters: true);
                    var exceptionUpdateSnippet = BuildArgsUpdateSnippet(method, requiresArgumentsSnapshot, includeOutParameters: false);
                    sb.AppendLine("        try { " + FormatFrameworkType("SourceGenerator.Runtime.Pipeline.ProxyRuntime") + ".InvokeSynchronousBefore(__ctx, ref __enteredBehaviorCount); " + callExpression + "; " + updateSnippet + " " + FormatFrameworkType("SourceGenerator.Runtime.Pipeline.ProxyRuntime") + ".InvokeSynchronousAfter(__ctx, ref __enteredBehaviorCount, null); } catch (" + FormatFrameworkType("System.Exception") + " __ex) { " + exceptionUpdateSnippet + " " + FormatFrameworkType("SourceGenerator.Runtime.Pipeline.ProxyRuntime") + ".InvokeSynchronousException(__ctx, ref __enteredBehaviorCount, __ex); throw; }");
                    sb.AppendLine("        return;");
                }
                else
                {
                    var updateSnippet = BuildArgsUpdateSnippet(method, requiresArgumentsSnapshot, includeOutParameters: true);
                    var exceptionUpdateSnippet = BuildArgsUpdateSnippet(method, requiresArgumentsSnapshot, includeOutParameters: false);
                    sb.AppendLine("        try { " + FormatFrameworkType("SourceGenerator.Runtime.Pipeline.ProxyRuntime") + ".InvokeSynchronousBefore(__ctx, ref __enteredBehaviorCount); var __ret = " + callExpression + "; " + updateSnippet + " " + FormatFrameworkType("SourceGenerator.Runtime.Pipeline.ProxyRuntime") + ".InvokeSynchronousAfter(__ctx, ref __enteredBehaviorCount, __ret); return __ret; } catch (" + FormatFrameworkType("System.Exception") + " __ex) { " + exceptionUpdateSnippet + " " + FormatFrameworkType("SourceGenerator.Runtime.Pipeline.ProxyRuntime") + ".InvokeSynchronousException(__ctx, ref __enteredBehaviorCount, __ex); throw; }");
                }

                return;
            }

            var runtime = FormatFrameworkType("SourceGenerator.Runtime.Pipeline.ProxyRuntime");

            if (isTask)
            {
                sb.AppendLine("        return " + runtime + ".ExecuteTask(__ctx, () => " + callExpression + ");");
            }
            else if (isGenericTask)
            {
                var resultType = FormatType(returnTypeInfo.ResultType!);
                sb.AppendLine("        return " + runtime + ".ExecuteAsync<" + resultType + ">(__ctx, () => " + callExpression + ");");
            }
            else if (isValueTask)
            {
                sb.AppendLine("        return " + runtime + ".ExecuteTask(__ctx, () => " + callExpression + ");");
            }
            else if (isGenericValueTask)
            {
                var resultType = FormatType(returnTypeInfo.ResultType!);
                sb.AppendLine("        return " + runtime + ".ExecuteAsync<" + resultType + ">(__ctx, () => " + callExpression + " );");
            }
            else if (method.ReturnsVoid)
            {
                sb.AppendLine("        " + runtime + ".Execute<object?>(__ctx, () => { " + callExpression + "; return null; });");
                sb.AppendLine("        return;");
            }
            else
            {
                var returnType = FormatType(method.ReturnType, currentNamespace);
                sb.AppendLine("        return " + runtime + ".Execute<" + returnType + ">(__ctx, () => " + callExpression + ");");
            }

        }


        /// <summary>
        /// 为当前代理方法准备行为实例列表和配置写入代码
        /// </summary>
        /// <param name="sb">目标源码构建器</param>
        /// <param name="behaviorSpecs">当前代理方法的行为生成信息</param>
        /// <param name="behaviorExpression">代理方法使用的行为列表表达式</param>
        /// <param name="optionsSetters">代理方法使用的配置写入代码</param>
        private void PrepareBehaviorPipeline(StringBuilder sb, IReadOnlyList<ProxyBehaviorSpec> behaviorSpecs, out string behaviorExpression, out IReadOnlyList<string> optionsSetters)
        {

            if (behaviorSpecs.Count == 0)
            {
                behaviorExpression = FormatFrameworkType("System.Array") + ".Empty<" + FormatFrameworkType("SourceGenerator.Runtime.Pipeline.IInvocationAsyncBehavior") + ">()";
                optionsSetters = Array.Empty<string>();
                return;
            }

            if (behaviorSpecs.Any(static behaviorSpec => !behaviorSpec.IsBuiltIn))
            {
                behaviorExpression = "new " + FormatFrameworkType("SourceGenerator.Runtime.Pipeline.IInvocationAsyncBehavior") + "[] { " + string.Join(", ", behaviorSpecs.Select(static behaviorSpec => "new " + behaviorSpec.BehaviorTypeName + "()")) + " }";
                optionsSetters = behaviorSpecs
                    .Where(static behaviorSpec => behaviorSpec.OptionsTypeName is not null)
                    .Select(behaviorSpec => "__ctx.SetFeature(new " + behaviorSpec.OptionsTypeName + behaviorSpec.OptionsInitializer + ");")
                    .ToArray();
                return;
            }

            var cacheSuffix = (++cachedBehaviorPipelineCount).ToString("D3");
            var behaviorFieldNames = new string[behaviorSpecs.Count];
            var cachedOptionsSetters = new List<string>();

            for (var i = 0; i < behaviorSpecs.Count; i++)
            {
                var behaviorSpec = behaviorSpecs[i];
                var behaviorFieldName = "__cachedBehavior" + cacheSuffix + "_" + i;
                behaviorFieldNames[i] = behaviorFieldName;
                sb.AppendLine("    private static readonly " + behaviorSpec.BehaviorTypeName + " " + behaviorFieldName + " = new();");

                if (behaviorSpec.OptionsTypeName is null)
                    continue;

                var optionsFieldName = "__cachedBehaviorOptions" + cacheSuffix + "_" + i;
                sb.AppendLine("    private static readonly " + behaviorSpec.OptionsTypeName + " " + optionsFieldName + " = new " + behaviorSpec.OptionsTypeName + behaviorSpec.OptionsInitializer + ";");
                cachedOptionsSetters.Add("__ctx.SetFeature(" + optionsFieldName + ");");
            }

            var behaviorsFieldName = "__cachedBehaviors" + cacheSuffix;
            sb.AppendLine("    private static readonly " + FormatFrameworkType("System.Collections.Generic.IReadOnlyList`1") + "<" + FormatFrameworkType("SourceGenerator.Runtime.Pipeline.IInvocationAsyncBehavior") + "> " + behaviorsFieldName + " = " + FormatFrameworkType("System.Array") + ".AsReadOnly<" + FormatFrameworkType("SourceGenerator.Runtime.Pipeline.IInvocationAsyncBehavior") + ">(new " + FormatFrameworkType("SourceGenerator.Runtime.Pipeline.IInvocationAsyncBehavior") + "[] { " + string.Join(", ", behaviorFieldNames) + " });")
              .AppendLine()
              .AppendLine();

            behaviorExpression = behaviorsFieldName;
            optionsSetters = cachedOptionsSetters;

        }


        /// <summary>
        /// 从行为特性中提取行为类型和可选配置初始化代码
        /// </summary>
        /// <param name="attribute">待解析的代理行为特性</param>
        /// <param name="behaviorSpec">解析得到的代理行为生成信息</param>
        /// <returns>如果特性声明了有效代理行为则返回 true</returns>
        private bool TryGetBehaviorSpec(AttributeData attribute, out ProxyBehaviorSpec behaviorSpec)
        {

            behaviorSpec = default;

            if (!AutoProxyEligibility.TryGetProxyBehaviorTypes(attribute, out var behaviorTypeSymbol, out var optionsTypeSymbol) || behaviorTypeSymbol is null)
                return false;

            var behaviorTypeName = FormatType(behaviorTypeSymbol, string.Empty);
            string? optionsTypeName = null;
            string? optionsInitializer = null;

            if (optionsTypeSymbol is not null)
            {
                var assigns = new List<string>();
                foreach (var kv in attribute.NamedArguments)
                {
                    var propName = kv.Key;
                    var prop = AutoProxyEligibility.FindOptionsProperty(optionsTypeSymbol, propName);
                    if (prop is null) continue;
                    if (!AutoProxyEligibility.TryFormatAttributeArgument(kv.Value, out var lit)) continue;
                    assigns.Add(EscapeIdentifier(propName) + " = " + lit);
                }
                optionsTypeName = FormatType(optionsTypeSymbol, string.Empty);
                if (optionsTypeName.StartsWith("Options.", StringComparison.Ordinal))
                {
                    optionsTypeName = optionsTypeName.Substring("Options.".Length);
                }
                optionsInitializer = assigns.Count > 0 ? " { " + string.Join(", ", assigns) + " }" : "()";
            }

            behaviorSpec = new ProxyBehaviorSpec(behaviorTypeName, optionsTypeName, optionsInitializer, IsBuiltInBehavior(behaviorTypeSymbol, optionsTypeSymbol));

            return true;
        }


        /// <summary>
        /// 判断行为和配置类型是否属于生成器确认无状态且只读的内置组合
        /// </summary>
        /// <param name="behaviorType">待检查的行为类型</param>
        /// <param name="optionsType">待检查的行为配置类型</param>
        /// <returns>如果行为和配置可以安全跨调用复用则返回 true</returns>
        private bool IsBuiltInBehavior(ITypeSymbol behaviorType, INamedTypeSymbol? optionsType)
        {

            if (IsCompilationType(behaviorType, LoggingBehaviorMetadataName))
                return optionsType is null;

            if (IsCompilationType(behaviorType, RetryBehaviorMetadataName))
                return optionsType is not null && IsCompilationType(optionsType, RetryOptionsMetadataName);

            if (IsCompilationType(behaviorType, ConcurrencyLimitBehaviorMetadataName))
                return optionsType is not null && IsCompilationType(optionsType, ConcurrencyLimitOptionsMetadataName);

            if (IsCompilationType(behaviorType, CacheableBehaviorMetadataName))
                return optionsType is not null && IsCompilationType(optionsType, CacheableOptionsMetadataName);

            return false;

        }


        /// <summary>
        /// 使用当前编译中的类型符号精确判断指定元数据类型
        /// </summary>
        /// <param name="type">待检查的类型</param>
        /// <param name="metadataName">目标元数据类型名称</param>
        /// <returns>如果类型符号与当前编译中的目标类型一致则返回 true</returns>
        private bool IsCompilationType(ITypeSymbol type, string metadataName)
        {

            var expectedType = compilation.GetTypeByMetadataName(metadataName);
            return expectedType is not null && SymbolEqualityComparer.Default.Equals(type, expectedType);

        }


        /// <summary>
        /// 转发派生构造函数必须保留的编译器契约特性
        /// </summary>
        /// <param name="sb">目标源码构建器</param>
        /// <param name="constructor">被镜像的基类构造函数</param>
        /// <param name="includeActivatorUtilitiesConstructor">是否保留 ActivatorUtilities 构造函数选择特性</param>
        private static void AppendConstructorAttributes(StringBuilder sb, IMethodSymbol constructor, bool includeActivatorUtilitiesConstructor)
        {

            if (constructor.GetAttributes().Any(attribute => attribute.AttributeClass is INamedTypeSymbol attributeType
                && IsNamedType(attributeType, "System.Diagnostics.CodeAnalysis", "SetsRequiredMembersAttribute")))
            {
                sb.AppendLine("    [global::System.Diagnostics.CodeAnalysis.SetsRequiredMembers]");
            }

            if (includeActivatorUtilitiesConstructor
                && constructor.GetAttributes().Any(attribute => attribute.AttributeClass is INamedTypeSymbol attributeType
                    && IsNamedType(attributeType, "Microsoft.Extensions.DependencyInjection", "ActivatorUtilitiesConstructorAttribute")))
            {
                sb.AppendLine("    [global::Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]");
            }

        }


        /// <summary>
        /// 构建代理构造函数用于去重的参数签名
        /// </summary>
        /// <param name="parameters">基类构造函数参数</param>
        /// <param name="prependServiceProvider">是否在签名前添加服务容器参数</param>
        /// <returns>忽略参数名称和可空标注的构造函数签名</returns>
        private static string BuildConstructorSignatureKey(IEnumerable<IParameterSymbol> parameters, bool prependServiceProvider)
        {

            var signatureParts = new List<string>();

            if (prependServiceProvider)
                signatureParts.Add("0:N:System.IServiceProvider");

            foreach (var parameter in parameters)
            {
                var referenceKind = parameter.RefKind == RefKind.None ? "0" : "1";
                signatureParts.Add(referenceKind + ":" + BuildSignatureTypeKey(parameter.Type));
            }

            return string.Join("|", signatureParts);

        }


        /// <summary>
        /// 构建忽略可空标注 元组元素名称和 dynamic 别名的 C# 签名类型键
        /// </summary>
        /// <param name="type">待格式化的参数类型</param>
        /// <returns>符合 C# 重载等价规则的类型键</returns>
        private static string BuildSignatureTypeKey(ITypeSymbol type)
        {

            if (type.TypeKind == TypeKind.Dynamic || type.SpecialType == SpecialType.System_Object)
                return "N:System.Object";

            if (type is IArrayTypeSymbol arrayType)
                return "A" + arrayType.Rank.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + BuildSignatureTypeKey(arrayType.ElementType);

            if (type is IPointerTypeSymbol pointerType)
                return "P:" + BuildSignatureTypeKey(pointerType.PointedAtType);

            if (type is IFunctionPointerTypeSymbol functionPointerType)
                return "F:" + functionPointerType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            if (type is ITypeParameterSymbol typeParameter)
            {
                var owner = typeParameter.ContainingSymbol is INamedTypeSymbol containingType
                    ? BuildNamedTypeDefinitionKey(containingType)
                    : typeParameter.ContainingSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                return "T:" + owner + ":" + typeParameter.Ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            if (type is not INamedTypeSymbol namedType)
                return type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            if (namedType.IsTupleType)
                namedType = namedType.TupleUnderlyingType ?? namedType;

            var typeArguments = namedType.TypeArguments.Length == 0
                ? string.Empty
                : "[" + string.Join(",", namedType.TypeArguments.Select(BuildSignatureTypeKey)) + "]";

            var containingTypeKey = namedType.ContainingType is null
                ? string.Empty
                : "{" + BuildSignatureTypeKey(namedType.ContainingType) + "}";

            return "N:" + BuildNamedTypeDefinitionKey(namedType.OriginalDefinition) + containingTypeKey + typeArguments;

        }


        /// <summary>
        /// 构建命名类型定义的元数据标识
        /// </summary>
        /// <param name="type">待格式化的命名类型</param>
        /// <returns>包含命名空间和外层类型的定义标识</returns>
        private static string BuildNamedTypeDefinitionKey(INamedTypeSymbol type)
        {

            var containingTypes = new Stack<string>();

            for (var current = type; current is not null; current = current.ContainingType)
            {
                containingTypes.Push(current.MetadataName);
            }

            var namespacePrefix = type.ContainingNamespace.IsGlobalNamespace
                ? string.Empty
                : type.ContainingNamespace.ToDisplayString() + ".";

            return namespacePrefix + string.Join("+", containingTypes);

        }


        /// <summary>
        /// 将方法或构造函数参数格式化为调用参数文本
        /// </summary>
        /// <param name="parameter">待格式化的参数</param>
        /// <returns>包含引用修饰符和参数名称的调用文本</returns>
        private static string FormatArgument(IParameterSymbol parameter)
        {

            var modifier = parameter.RefKind switch
            {
                RefKind.Ref => "ref ",
                RefKind.Out => "out ",
                RefKind.In or RefKind.RefReadOnlyParameter => "in ",
                _ => string.Empty
            };

            return modifier + EscapeIdentifier(parameter.Name) + (parameter.RefKind == RefKind.None ? "!" : string.Empty);

        }


        /// <summary>
        /// 将 Roslyn 参数符号格式化为 C# 方法参数文本 可选择是否包含默认值
        /// </summary>
        /// <param name="p">待格式化的参数</param>
        /// <param name="includeDefault">是否保留默认值和调用方契约特性</param>
        /// <param name="currentNamespace">当前生成代码命名空间</param>
        /// <returns>可直接写入参数声明的源码</returns>
        private string FormatParameter(IParameterSymbol p, bool includeDefault, string? currentNamespace = null)
        {
            var type = FormatType(p.Type, currentNamespace);

            var mod = p.IsParams
                ? "params "
                : p.RefKind switch
            {
                RefKind.Ref => "ref ",
                RefKind.Out => "out ",
                RefKind.In => "in ",
                RefKind.RefReadOnlyParameter => "ref readonly ",
                _ => string.Empty
            };

            // 保留依赖注入 默认值和直接调用代理所需的参数契约特性
            var attrPrefix = BuildParameterAttributesPrefix(p, includeDefault);

            var hasMetadataDateTimeDefault = p.HasExplicitDefaultValue && p.ExplicitDefaultValue is DateTime;
            var @default = includeDefault && p.HasExplicitDefaultValue && !hasMetadataDateTimeDefault
                ? " = " + FormatDefaultValue(p)
                : string.Empty;

            return attrPrefix + mod + type + " " + EscapeIdentifier(p.Name) + @default;
        }


        /// <summary>
        /// 将参数默认值格式化为保留类型和转义信息的 C# 常量表达式
        /// </summary>
        /// <param name="parameter">包含默认值的参数</param>
        /// <returns>可直接写入参数声明的常量表达式</returns>
        private string FormatDefaultValue(IParameterSymbol parameter)
        {

            var value = parameter.ExplicitDefaultValue;

            if (value is null)
                return parameter.Type.IsReferenceType ? "null" : "default";

            var invariantCulture = System.Globalization.CultureInfo.InvariantCulture;
            string literal;

            switch (value)
            {
                case string stringValue:
                    literal = Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(stringValue, quote: true);
                    break;
                case char characterValue:
                    literal = Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(characterValue, quote: true);
                    break;
                case bool booleanValue:
                    literal = booleanValue ? "true" : "false";
                    break;
                case float floatValue when float.IsNaN(floatValue):
                    literal = "global::System.Single.NaN";
                    break;
                case float floatValue when float.IsPositiveInfinity(floatValue):
                    literal = "global::System.Single.PositiveInfinity";
                    break;
                case float floatValue when float.IsNegativeInfinity(floatValue):
                    literal = "global::System.Single.NegativeInfinity";
                    break;
                case float floatValue:
                    literal = floatValue.ToString("R", invariantCulture) + "F";
                    break;
                case double doubleValue when double.IsNaN(doubleValue):
                    literal = "global::System.Double.NaN";
                    break;
                case double doubleValue when double.IsPositiveInfinity(doubleValue):
                    literal = "global::System.Double.PositiveInfinity";
                    break;
                case double doubleValue when double.IsNegativeInfinity(doubleValue):
                    literal = "global::System.Double.NegativeInfinity";
                    break;
                case double doubleValue:
                    literal = doubleValue.ToString("R", invariantCulture) + "D";
                    break;
                case decimal decimalValue:
                    literal = decimalValue.ToString(invariantCulture) + "M";
                    break;
                case uint unsignedIntegerValue:
                    literal = unsignedIntegerValue.ToString(invariantCulture) + "U";
                    break;
                case long longValue:
                    literal = longValue.ToString(invariantCulture) + "L";
                    break;
                case ulong unsignedLongValue:
                    literal = unsignedLongValue.ToString(invariantCulture) + "UL";
                    break;
                case IFormattable formattableValue:
                    literal = formattableValue.ToString(null, invariantCulture);
                    break;
                default:
                    return "default";
            }

            // 可空枚举的非空默认值使用底层枚举类型生成常量表达式
            var defaultValueType = parameter.Type is INamedTypeSymbol namedType && namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
                ? namedType.TypeArguments[0]
                : parameter.Type;

            return defaultValueType.TypeKind == TypeKind.Enum
                ? "(" + FormatType(defaultValueType) + ")" + literal
                : literal;

        }


        /// <summary>
        /// 构建参数前缀中的依赖注入 默认值和调用方契约特性文本
        /// </summary>
        /// <param name="p">待检查的参数</param>
        /// <param name="includeCallerContractAttributes">是否保留仅对直接调用生效的调用方契约特性</param>
        /// <returns>参数声明前的特性源码</returns>
        private static string BuildParameterAttributesPrefix(IParameterSymbol p, bool includeCallerContractAttributes)
        {
            if (p is null) return string.Empty;

            var attrs = p.GetAttributes();

            if (attrs.Length == 0) return string.Empty;

            var sb = new StringBuilder();

            foreach (var attr in attrs)
            {
                if (attr.AttributeClass is not INamedTypeSymbol at) continue;

                var isDependencyInjectionContract = IsNamedType(at, "Microsoft.Extensions.DependencyInjection", "FromKeyedServicesAttribute")
                    || IsNamedType(at, "Microsoft.Extensions.DependencyInjection", "FromServicesAttribute")
                    || IsNamedType(at, "Microsoft.Extensions.DependencyInjection", "ServiceKeyAttribute");
                var isDefaultValueContract = IsNamedType(at, "System.Runtime.CompilerServices", "DateTimeConstantAttribute")
                    || IsNamedType(at, "System.Runtime.InteropServices", "OptionalAttribute");
                var isCallerContract = includeCallerContractAttributes
                    && (IsNamedType(at, "System.Runtime.CompilerServices", "CallerArgumentExpressionAttribute")
                        || IsNamedType(at, "System.Runtime.CompilerServices", "CallerFilePathAttribute")
                        || IsNamedType(at, "System.Runtime.CompilerServices", "CallerLineNumberAttribute")
                        || IsNamedType(at, "System.Runtime.CompilerServices", "CallerMemberNameAttribute")
                        || IsNamedType(at, "System.Runtime.CompilerServices", "InterpolatedStringHandlerArgumentAttribute"));

                if (isDependencyInjectionContract || isDefaultValueContract || isCallerContract)
                {
                    AppendAttributeSource(sb, at, attr);
                }
            }

            var result = sb.ToString();

            return result;
        }


        /// <summary>
        /// 将指定特性及其构造参数和命名参数完整写入生成源码
        /// </summary>
        /// <param name="sb">目标源码构建器</param>
        /// <param name="attributeType">特性类型</param>
        /// <param name="attribute">特性实例数据</param>
        private static void AppendAttributeSource(StringBuilder sb, INamedTypeSymbol attributeType, AttributeData attribute)
        {

            var arguments = new List<string>();

            foreach (var argument in attribute.ConstructorArguments)
            {
                if (!AutoProxyEligibility.TryFormatAttributeArgument(argument, out var argumentSource))
                    return;

                arguments.Add(argumentSource);
            }

            foreach (var namedArgument in attribute.NamedArguments)
            {
                if (!AutoProxyEligibility.TryFormatAttributeArgument(namedArgument.Value, out var argumentSource))
                    return;

                arguments.Add(EscapeIdentifier(namedArgument.Key) + " = " + argumentSource);
            }

            sb.Append('[')
              .Append(attributeType.ToDisplayString(SourceTypeDisplayFormat));

            if (arguments.Count > 0)
            {
                sb.Append('(')
                  .Append(string.Join(", ", arguments))
                  .Append(')');
            }

            sb.Append("] ");

        }


        /// <summary>
        /// 格式化模板依赖的框架类型 在名称冲突时保留完整限定
        /// </summary>
        private string FormatFrameworkType(string metadataName)
        {

            var type = compilation.GetTypeByMetadataName(metadataName);
            if (type is null)
                return "global::" + metadataName.Split('`')[0];

            var currentNamespace = generatedNamespace?.ToDisplayString() ?? "NetEngine.Generated";
            var scopeNamespaces = generatedNamespace is null
                ? fixedImportedNamespaces
                : fixedImportedNamespaces.Concat(new[] { generatedNamespace }).ToArray();
            return RequiresGlobalQualification(type, currentNamespace)
                || GeneratedTypeNameCollisionDetector.HasRootConflict(type, scopeNamespaces, Array.Empty<INamespaceSymbol>())
                ? "global::" + type.ContainingNamespace.ToDisplayString() + "." + type.Name
                : type.Name;

        }


        /// <summary>
        /// 补充重写和显式接口方法中解释可空泛型签名所需的约束
        /// </summary>
        private static void AppendMethodNullableConstraints(StringBuilder builder, IMethodSymbol method)
        {

            foreach (var parameter in method.TypeParameters)
            {
                if (parameter.HasValueTypeConstraint
                    || (!ContainsNullableTypeParameter(method.ReturnType, parameter)
                        && !method.Parameters.Any(item => ContainsNullableTypeParameter(item.Type, parameter))))
                    continue;

                builder.Append("        where ").Append(EscapeIdentifier(parameter.Name)).Append(" : ")
                    .AppendLine(parameter.IsReferenceType ? "class" : "default");
            }

        }


        /// <summary>
        /// 判断签名类型及其组成类型是否包含指定泛型参数的可空引用标注
        /// </summary>
        private static bool ContainsNullableTypeParameter(ITypeSymbol type, ITypeParameterSymbol parameter)
        {

            if (SymbolEqualityComparer.Default.Equals(type, parameter))
                return type.NullableAnnotation == NullableAnnotation.Annotated;

            if (type is IArrayTypeSymbol array)
                return ContainsNullableTypeParameter(array.ElementType, parameter);

            return type is INamedTypeSymbol namedType
                && (namedType.TypeArguments.Any(argument => ContainsNullableTypeParameter(argument, parameter))
                    || (namedType.ContainingType is not null && ContainsNullableTypeParameter(namedType.ContainingType, parameter)));

        }


        /// <summary>
        /// 将类型符号格式化为可安全输出到 C# 源码中的类型文本
        /// </summary>
        private string FormatType(ITypeSymbol type, string? currentNamespace = null)
        {

            var typeName = type.ToDisplayString(SourceTypeDisplayFormat);

            if (!string.IsNullOrEmpty(currentNamespace))
            {
                if (RequiresGlobalQualification(type, currentNamespace!))
                    return typeName;

                typeName = TrimCurrentNamespace(typeName, currentNamespace!);
            }

            return typeName;

        }


        /// <summary>
        /// 格式化代理内部参数快照字典类型并避免被业务同名类型遮蔽
        /// </summary>
        /// <param name="currentNamespace">当前生成代码命名空间</param>
        /// <returns>可安全使用的字典类型文本</returns>
        private string FormatArgumentsDictionaryType(string currentNamespace)
        {

            var dictionaryType = compilation.GetTypeByMetadataName("System.Collections.Generic.Dictionary`2");
            return dictionaryType is not null && RequiresGlobalQualification(dictionaryType, currentNamespace)
                ? "global::System.Collections.Generic.Dictionary<string, object?>"
                : "Dictionary<string, object?>";

        }


        /// <summary>
        /// 判断类型引用在当前代理作用域中是否必须保留 global 限定
        /// </summary>
        /// <param name="type">待格式化的类型</param>
        /// <param name="currentNamespace">当前生成代码命名空间</param>
        /// <returns>移除 global 后可能改变绑定目标时返回 true</returns>
        private bool RequiresGlobalQualification(ITypeSymbol type, string currentNamespace)
        {

            if (type is IArrayTypeSymbol arrayType)
                return RequiresGlobalQualification(arrayType.ElementType, currentNamespace);

            if (type is IPointerTypeSymbol pointerType)
                return RequiresGlobalQualification(pointerType.PointedAtType, currentNamespace);

            if (type is IFunctionPointerTypeSymbol functionPointerType)
            {
                return RequiresGlobalQualification(functionPointerType.Signature.ReturnType, currentNamespace)
                       || functionPointerType.Signature.Parameters.Any(parameter => RequiresGlobalQualification(parameter.Type, currentNamespace));
            }

            if (type is not INamedTypeSymbol namedType)
                return false;

            for (var current = namedType; current is not null; current = current.ContainingType)
            {
                foreach (var typeArgument in current.TypeArguments)
                {
                    if (RequiresGlobalQualification(typeArgument, currentNamespace))
                        return true;
                }
            }

            var rootType = GeneratedTypeNameCollisionDetector.GetRootType(namedType);
            var namespaceName = rootType.ContainingNamespace.IsGlobalNamespace
                ? string.Empty
                : rootType.ContainingNamespace.ToDisplayString();

            if (string.Equals(namespaceName, currentNamespace, StringComparison.Ordinal))
            {
                return scopedTypeNames.Contains(rootType.Name);
            }

            if (namespaceName.StartsWith(currentNamespace + ".", StringComparison.Ordinal))
            {
                var relativeNamespace = namespaceName.Substring(currentNamespace.Length + 1);
                var firstSegment = relativeNamespace.Split('.')[0];
                return scopedTypeNames.Contains(firstSegment);
            }

            if (scopedTypeNames.Contains(rootType.Name))
                return true;

            var scopeNamespaces = generatedNamespace is null
                ? fixedImportedNamespaces
                : fixedImportedNamespaces.Concat(new[] { generatedNamespace }).ToArray();

            if (string.Equals(namespaceName, "System", StringComparison.Ordinal))
            {
                return GeneratedTypeNameCollisionDetector.HasRootConflict(rootType, scopeNamespaces, Array.Empty<INamespaceSymbol>());
            }

            var shortenedPrefix = ShortenedNamespacePrefixes
                .Where(prefix => string.Equals(namespaceName, prefix, StringComparison.Ordinal)
                                 || namespaceName.StartsWith(prefix + ".", StringComparison.Ordinal))
                .OrderByDescending(static prefix => prefix.Length)
                .FirstOrDefault();

            if (shortenedPrefix is not null)
            {
                if (string.Equals(namespaceName, shortenedPrefix, StringComparison.Ordinal))
                {
                    return GeneratedTypeNameCollisionDetector.HasRootConflict(rootType, scopeNamespaces, Array.Empty<INamespaceSymbol>());
                }

                var relativeNamespace = namespaceName.Substring(shortenedPrefix.Length + 1);
                var firstSegment = relativeNamespace.Split('.')[0];
                return scopedTypeNames.Contains(firstSegment) || CountMatchingNamespaces(firstSegment, 0, scopeNamespaces) > 1;
            }

            if (string.IsNullOrEmpty(namespaceName))
            {
                return CountMatchingNamespaces(rootType.Name, rootType.Arity, scopeNamespaces) > 0;
            }

            var namespaceRoot = namespaceName.Split('.')[0];
            return scopedTypeNames.Contains(namespaceRoot) || CountMatchingNamespaces(namespaceRoot, 0, scopeNamespaces) > 0;

        }


        /// <summary>
        /// 收集代理作用域内的类型参数 方法参数和继承成员名称
        /// </summary>
        /// <param name="type">当前代理目标类型</param>
        /// <param name="analysis">目标类型的代理生成分析结果</param>
        private void CollectScopedTypeNames(INamedTypeSymbol type, AutoProxyAnalysisResult analysis)
        {

            scopedTypeNames.Clear();

            foreach (var typeParameter in GetAllTypeParameters(type))
            {
                scopedTypeNames.Add(typeParameter.Name);
            }

            var methods = type.Constructors.Concat(analysis.EffectiveProxyMethods).Concat(analysis.ExplicitInterfaceMethods.Select(static item => item.Method));
            foreach (var method in methods)
            {
                foreach (var typeParameter in method.TypeParameters)
                {
                    scopedTypeNames.Add(typeParameter.Name);
                }

                foreach (var parameter in method.Parameters)
                {
                    scopedTypeNames.Add(parameter.Name);
                }
            }

            for (var current = type; current is not null; current = current.BaseType)
            {
                foreach (var member in current.GetMembers())
                {
                    scopedTypeNames.Add(member.Name);
                }
            }

        }


        /// <summary>
        /// 统计作用域命名空间中包含指定根成员的不同命名空间数量
        /// </summary>
        /// <param name="name">根成员名称</param>
        /// <param name="arity">类型泛型参数数量</param>
        /// <param name="namespaces">待检查的命名空间</param>
        /// <returns>包含匹配根成员的命名空间数量</returns>
        private static int CountMatchingNamespaces(string name, int arity, IEnumerable<INamespaceSymbol> namespaces)
        {

            return namespaces
                .GroupBy(static namespaceSymbol => namespaceSymbol.ToDisplayString(), StringComparer.Ordinal)
                .Count(group => group.Any(namespaceSymbol => GeneratedTypeNameCollisionDetector.NamespaceContainsRootMember(namespaceSymbol, name, arity)));

        }


        /// <summary>
        /// 如果类型在当前命名空间下，裁剪掉重复的命名空间前缀提升可读性
        /// </summary>
        private static string TrimCurrentNamespace(string typeName, string currentNamespace)
        {
            if (string.IsNullOrEmpty(typeName)) return typeName;

            // 只裁剪由 Roslyn 输出的完整当前命名空间前缀 避免误删外部命名空间中的同名片段
            if (!string.IsNullOrEmpty(currentNamespace))
            {
                typeName = typeName.Replace("global::" + currentNamespace + ".", string.Empty);
            }

            // 常用 BCL 命名空间前缀去除，便于输出简洁类型名
            typeName = typeName.Replace("global::System.Collections.Generic.", string.Empty)
                               .Replace("global::System.Threading.Tasks.", string.Empty)
                               ;

            // 常用类型简化需要在移除剩余 global 前缀前处理
            typeName = typeName.Replace("global::System.Net.Http.HttpClient", "HttpClient")
                               .Replace("global::System.Net.Http.HttpRequestMessage", "HttpRequestMessage")
                               .Replace("global::System.Net.Http.HttpResponseMessage", "HttpResponseMessage")
                               .Replace("global::System.Threading.CancellationToken", "CancellationToken")
                               .Replace("global::System.Threading.CancellationTokenSource", "CancellationTokenSource")
                               .Replace("global::SourceGenerator.Runtime.Pipeline.Behaviors.", string.Empty)
                               .Replace("global::SourceGenerator.Runtime.Pipeline.", string.Empty)
                               .Replace("global::SourceGenerator.Runtime.Options.", string.Empty)
                               .Replace("global::SourceGenerator.Runtime.Serialization.", string.Empty);

            // 其余类型保留完整命名空间但去掉 global 前缀以维持生成代码可读性
            typeName = typeName.Replace("global::", string.Empty);

            // 兼容生成器内部手工构造的少量类型文本 只处理完整前缀避免误伤外部命名空间片段
            if (typeName.StartsWith("System.Collections.Generic.", StringComparison.Ordinal))
            {
                typeName = typeName.Substring("System.Collections.Generic.".Length);
            }
            else if (typeName.StartsWith("System.Threading.Tasks.", StringComparison.Ordinal))
            {
                typeName = typeName.Substring("System.Threading.Tasks.".Length);
            }

            // 对于形如 System.Int32 / System.String / System.Guid 这种明显只有一段的 System.* 类型名，
            // 可以安全移除 System. 前缀（避免误伤 System.Threading.* / System.Net.* 等多段命名空间）
            if (typeName.StartsWith("System.", StringComparison.Ordinal))
            {
                var afterSystem = typeName.Substring("System.".Length);
                var segmentEnd = afterSystem.IndexOfAny(['<', '[', '?', ',', ' ', ')', ':']);
                if (segmentEnd < 0) segmentEnd = afterSystem.Length;
                var firstSegment = afterSystem.Substring(0, segmentEnd);

                if (firstSegment.IndexOf('.') < 0)
                {
                    typeName = afterSystem;
                }
            }
            // 兼容不包含 global 前缀的手工类型文本 仅处理整个类型名的已知前缀
            var knownPrefixes = new[]
            {
                "System.Net.Http.",
                "System.Threading.",
                "SourceGenerator.Runtime.Pipeline.Behaviors.",
                "SourceGenerator.Runtime.Pipeline.",
                "SourceGenerator.Runtime.Options.",
                "SourceGenerator.Runtime.Serialization."
            };

            foreach (var knownPrefix in knownPrefixes)
            {
                if (typeName.StartsWith(knownPrefix, StringComparison.Ordinal))
                {
                    typeName = typeName.Substring(knownPrefix.Length);
                    break;
                }
            }

            return typeName;
        }


        /// <summary>
        /// 将符号名称转换为可安全输出到 C# 源码中的标识符
        /// </summary>
        private static string EscapeIdentifier(string name)
        {

            if (string.IsNullOrWhiteSpace(name))
                return "_";

            if (name.StartsWith("@", StringComparison.Ordinal))
                return name;

            if (!SyntaxFacts.IsValidIdentifier(name)
                || SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None
                || SyntaxFacts.GetContextualKeywordKind(name) != SyntaxKind.None)
            {
                return "@" + name;
            }

            return name;

        }


        /// <summary>
        /// 判断给定类型在完全限定名层面是否等于指定元数据名称
        /// </summary>
        private static bool IsType(ITypeSymbol t, string metadataName)
        {
            // 通过完全限定名进行精确比较 对泛型类型使用未构造的泛型定义进行比较
            if (t is INamedTypeSymbol nt)
            {
                var open = nt.IsGenericType && nt.ConstructedFrom is INamedTypeSymbol cf ? cf : nt;

                var fq = open.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                var expected = metadataName.StartsWith("global::", StringComparison.Ordinal)
                    ? metadataName
                    : "global::" + metadataName;

                return string.Equals(fq, expected, StringComparison.Ordinal);
            }
            else
            {
                var fq = t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                var expected = metadataName.StartsWith("global::", StringComparison.Ordinal)
                    ? metadataName
                    : "global::" + metadataName;

                return string.Equals(fq, expected, StringComparison.Ordinal);
            }
        }


        /// <summary>
        /// 判断命名类型是否匹配指定的命名空间 名称以及可选泛型参数个数
        /// </summary>
        private static bool IsNamedType(INamedTypeSymbol symbol, string @namespace, string name, int? arity = null)
        {
            var ns = GetFullNamespace(symbol.ContainingNamespace);

            if (!string.Equals(ns, @namespace, StringComparison.Ordinal)) return false;

            if (!string.Equals(symbol.Name, name, StringComparison.Ordinal)) return false;

            if (arity.HasValue && symbol.Arity != arity.Value) return false;

            return true;
        }


        /// <summary>
        /// 在编译期判断参数类型是否需要跳过 JSON 序列化并用占位字符串替代
        /// </summary>
        private static bool TryGetSkipPlaceholder(ITypeSymbol type, out string placeholder)
        {
            placeholder = "<skipped>";

            // 取消相关类型
            if (IsType(type, "System.Threading.CancellationToken")) { placeholder = "<cancellation-token>"; return true; }
            if (IsType(type, "System.Threading.CancellationTokenSource")) { placeholder = "<cancellation-token-source>"; return true; }

            // 委托类型
            if (type.TypeKind == TypeKind.Delegate) { placeholder = "<delegate>"; return true; }

            // 流与文本读写相关类型
            if (IsOrDerivedFrom(type, "System.IO.Stream")) { placeholder = "<stream>"; return true; }
            if (IsOrDerivedFrom(type, "System.IO.TextReader")) { placeholder = "<text-reader>"; return true; }
            if (IsOrDerivedFrom(type, "System.IO.TextWriter")) { placeholder = "<text-writer>"; return true; }

            // 管道相关类型 System.IO.Pipelines
            if (IsType(type, "System.IO.Pipelines.PipeReader")) { placeholder = "<pipe-reader>"; return true; }
            if (IsType(type, "System.IO.Pipelines.PipeWriter")) { placeholder = "<pipe-writer>"; return true; }

            // 通道相关泛型类型 System.Threading.Channels
            if (IsOrDerivedFromGeneric(type, "System.Threading.Channels", "ChannelReader", 1)) { placeholder = "<channel-reader>"; return true; }
            if (IsOrDerivedFromGeneric(type, "System.Threading.Channels", "ChannelWriter", 1)) { placeholder = "<channel-writer>"; return true; }

            // ASP.NET Core Http 相关类型
            var ns = GetFullNamespace(type.ContainingNamespace);
            if (ns.StartsWith("Microsoft.AspNetCore.Http", StringComparison.Ordinal)) { placeholder = "<http-context>"; return true; }

            // 安全主体相关类型
            if (IsType(type, "System.Security.Claims.ClaimsPrincipal") || ImplementsInterface(type, "System.Security.Principal.IPrincipal"))
            { placeholder = "<principal>"; return true; }

            // 依赖注入和日志相关类型
            if (ImplementsInterface(type, "System.IServiceProvider")) { placeholder = "<service-provider>"; return true; }
            if (ImplementsInterfaceNamed(type, "Microsoft.Extensions.Logging", "ILogger")) { placeholder = "<logger>"; return true; }

            // 数据库访问相关类型
            if (IsOrDerivedFrom(type, "System.Data.Common.DbConnection") || ImplementsInterface(type, "System.Data.IDbConnection"))
            { placeholder = "<db-connection>"; return true; }
            if (IsOrDerivedFrom(type, "System.Data.Common.DbTransaction")) { placeholder = "<db-transaction>"; return true; }
            if (IsOrDerivedFrom(type, "System.Data.Common.DbCommand")) { placeholder = "<db-command>"; return true; }

            // HTTP 通信相关类型
            if (IsType(type, "System.Net.Http.HttpClient")) { placeholder = "<http-client>"; return true; }
            if (IsType(type, "System.Net.Http.HttpRequestMessage")) { placeholder = "<http-request>"; return true; }
            if (IsType(type, "System.Net.Http.HttpResponseMessage")) { placeholder = "<http-response>"; return true; }

            // 表达式树相关类型
            if (IsOrDerivedFrom(type, "System.Linq.Expressions.Expression")) { placeholder = "<expression>"; return true; }

            return false;
        }


        /// <summary>
        /// 判断类型本身或其继承链上是否存在指定元数据名称的类型
        /// </summary>
        private static bool IsOrDerivedFrom(ITypeSymbol type, string metadataName)
        {
            for (var t = type; t is not null; t = t.BaseType)
            {
                if (IsType(t, metadataName)) return true;
            }

            return false;
        }


        /// <summary>
        /// 判断类型本身或其继承链上是否存在指定命名空间 名称和泛型参数个数的泛型类型
        /// </summary>
        private static bool IsOrDerivedFromGeneric(ITypeSymbol type, string @namespace, string name, int arity)
        {
            for (var t = type; t is not null; t = t.BaseType)
            {
                if (t is INamedTypeSymbol nt && nt.IsGenericType && IsNamedType(nt.ConstructedFrom, @namespace, name, arity))
                    return true;
            }

            return false;
        }


        /// <summary>
        /// 判断类型是否实现给定元数据名称的接口
        /// </summary>
        private static bool ImplementsInterface(ITypeSymbol type, string metadataName)
        {
            foreach (var i in type.AllInterfaces)
            {
                if (IsType(i, metadataName)) return true;
            }

            return false;
        }


        /// <summary>
        /// 判断类型是否实现指定命名空间和名称以及可选泛型参数个数的接口
        /// </summary>
        private static bool ImplementsInterfaceNamed(ITypeSymbol type, string @namespace, string name, int? arity = null)
        {
            foreach (var i in type.AllInterfaces)
            {
                if (i is INamedTypeSymbol nt && IsNamedType(nt, @namespace, name, arity)) return true;
            }

            return false;
        }


        /// <summary>
        /// 判断方法返回值类型在日志中是否允许进行序列化输出
        /// </summary>
        /// <param name="method">待检查的代理方法</param>
        /// <param name="returnTypeInfo">统一的返回类型分析结果</param>
        /// <returns>如果返回值允许序列化则返回 true</returns>
        private static bool IsAllowReturnSerialization(IMethodSymbol method, ProxyReturnTypeInfo returnTypeInfo)
        {

            // 异步流类型使用占位符记录日志 视为可记录类型
            if (returnTypeInfo.IsAsyncStream)
                return true;

            // 无返回值的 Task 或 ValueTask
            if (returnTypeInfo.Kind is ProxyReturnTypeKind.Void or ProxyReturnTypeKind.Task or ProxyReturnTypeKind.ValueTask)
                return false;

            // 对 Task<T> 和 ValueTask<T> 进行解包 使用其泛型参数做判断
            if (returnTypeInfo.Kind is ProxyReturnTypeKind.TaskOfT or ProxyReturnTypeKind.ValueTaskOfT)
                return returnTypeInfo.ResultType is not null && IsReturnTypeLoggableCore(returnTypeInfo.ResultType);

            return IsReturnTypeLoggableCore(method.ReturnType);
        }


        /// <summary>
        /// 判断具体类型在日志中是否适合作为返回值进行序列化记录
        /// </summary>
        private static bool IsReturnTypeLoggableCore(ITypeSymbol type)
        {
            if (IsOrDerivedFrom(type, "System.IO.Stream")) return false;

            if (IsOrDerivedFrom(type, "System.IO.TextReader")) return false;

            if (IsOrDerivedFrom(type, "System.IO.TextWriter")) return false;

            if (IsType(type, "System.IO.Pipelines.PipeReader")) return false;

            if (IsType(type, "System.IO.Pipelines.PipeWriter")) return false;

            if (IsOrDerivedFromGeneric(type, "System.Threading.Channels", "ChannelReader", 1)) return false;

            if (IsOrDerivedFromGeneric(type, "System.Threading.Channels", "ChannelWriter", 1)) return false;

            if (IsOrDerivedFrom(type, "System.Data.Common.DbConnection") || ImplementsInterface(type, "System.Data.IDbConnection")) return false;

            if (IsOrDerivedFrom(type, "System.Data.Common.DbTransaction")) return false;

            if (IsOrDerivedFrom(type, "System.Data.Common.DbCommand")) return false;

            if (IsType(type, "System.Net.Http.HttpRequestMessage")) return false;

            if (IsType(type, "System.Net.Http.HttpResponseMessage")) return false;

            if (IsType(type, "System.Net.Http.HttpClient")) return false;

            if (type.TypeKind == TypeKind.Delegate) return false;

            if (IsOrDerivedFrom(type, "System.Linq.Expressions.Expression")) return false;

            if (IsType(type, "System.Security.Claims.ClaimsPrincipal") || ImplementsInterface(type, "System.Security.Principal.IPrincipal")) return false;

            return true;
        }


        /// <summary>
        /// 获取命名空间的完整限定名 对全局命名空间返回空字符串
        /// </summary>
        private static string GetFullNamespace(INamespaceSymbol ns)
        {
            if (ns == null || ns.IsGlobalNamespace) return string.Empty;

            var stack = new Stack<string>();

            for (var n = ns; n is not null && !n.IsGlobalNamespace; n = n.ContainingNamespace)
            {
                stack.Push(n.Name);
            }

            return string.Join(".", stack);
        }


        /// <summary>
        /// 从编译的合并全局命名空间中查找指定命名空间
        /// </summary>
        /// <param name="globalNamespace">编译的全局命名空间</param>
        /// <param name="namespaceName">以点分隔的完整命名空间名称</param>
        /// <returns>找到的命名空间 不存在时返回 null</returns>
        private static INamespaceSymbol? FindNamespace(INamespaceSymbol globalNamespace, string namespaceName)
        {

            var current = globalNamespace;
            foreach (var segment in namespaceName.Split('.'))
            {
                current = current.GetNamespaceMembers().FirstOrDefault(namespaceSymbol => string.Equals(namespaceSymbol.Name, segment, StringComparison.Ordinal));
                if (current is null)
                    return null;
            }

            return current;

        }


        /// <summary>
        /// 根据代理行为需要生成供日志或自定义行为读取的参数快照代码
        /// </summary>
        /// <param name="sb">目标源码构建器</param>
        /// <param name="method">当前代理方法</param>
        /// <param name="currentNamespace">当前生成源码所在命名空间</param>
        /// <param name="requiresArgumentsSnapshot">是否需要生成参数快照</param>
        private void AppendArgumentsSnapshot(StringBuilder sb, IMethodSymbol method, string currentNamespace, bool requiresArgumentsSnapshot)
        {

            if (!requiresArgumentsSnapshot || method.Parameters.Length == 0)
            {
                sb.AppendLine("        object? __argsObj = null;");
                return;
            }

            var dictType = FormatArgumentsDictionaryType(currentNamespace);
            sb.AppendLine("        var __argsDict = new " + dictType + "(" + method.Parameters.Length + ");");

            foreach (var parameter in method.Parameters)
            {
                var isOut = parameter.RefKind == RefKind.Out;
                var isRefLike = parameter.Type.IsRefLikeType;

                if (isOut || isRefLike)
                {
                    sb.Append("        __argsDict[\"").Append(parameter.Name).Append("\"] = null;").AppendLine();
                }
                else if (TryGetSkipPlaceholder(parameter.Type, out var placeholder))
                {
                    sb.Append("        __argsDict[\"").Append(parameter.Name).Append("\"] = \"")
                      .Append(placeholder.Replace("\\", "\\\\").Replace("\"", "\\\""))
                      .Append("\";").AppendLine();
                }
                else
                {
                    sb.Append("        __argsDict[\"").Append(parameter.Name).Append("\"] = ").Append(FormatFrameworkType("SourceGenerator.Runtime.Serialization.JsonUtil")).Append(".CreateSnapshotValue(")
                      .Append(EscapeIdentifier(parameter.Name)).Append(");").AppendLine();
                }
            }

            sb.AppendLine("        object? __argsObj = __argsDict;");

        }


        /// <summary>
        /// 为带 ref out 或 in 参数的方法构建调用后刷新参数快照的代码片段
        /// </summary>
        /// <param name="method">当前代理方法</param>
        /// <param name="requiresArgumentsSnapshot">是否需要刷新参数快照</param>
        /// <param name="includeOutParameters">是否刷新只有成功调用后才能安全读取的 out 参数</param>
        /// <returns>参数快照刷新代码 不需要刷新时返回空字符串</returns>
        private string BuildArgsUpdateSnippet(IMethodSymbol method, bool requiresArgumentsSnapshot, bool includeOutParameters)
        {

            if (!requiresArgumentsSnapshot)
                return string.Empty;

            var updates = new List<string>();

            foreach (var p in method.Parameters)
            {
                if (p.RefKind != RefKind.None && (includeOutParameters || p.RefKind != RefKind.Out))
                {
                    // 刷新当前分支中可以安全读取的引用参数值
                    if (p.Type.IsRefLikeType)
                    {
                        updates.Add($"__argsDict[\"{p.Name}\"] = null;");
                    }
                    else if (TryGetSkipPlaceholder(p.Type, out var ph))
                    {
                        var escaped = ph.Replace("\\", "\\\\").Replace("\"", "\\\"");
                        updates.Add($"__argsDict[\"{p.Name}\"] = \"{escaped}\";");
                    }
                    else
                    {
                        var parameterName = EscapeIdentifier(p.Name);
                        updates.Add($"__argsDict[\"{p.Name}\"] = " + FormatFrameworkType("SourceGenerator.Runtime.Serialization.JsonUtil") + $".CreateSnapshotValue({parameterName});");
                    }
                }
            }

            return updates.Count == 0 ? string.Empty : string.Join(" ", updates);
        }


        /// <summary>
        /// 生成当前调用用于构建缓存键和锁键的规范化参数快照代码
        /// </summary>
        /// <param name="sb">目标源码构建器</param>
        /// <param name="method">当前代理方法</param>
        /// <param name="requiresArgumentsKey">是否需要生成规范化参数内容</param>
        private void AppendArgumentsKeySnapshot(StringBuilder sb, IMethodSymbol method, bool requiresArgumentsKey)
        {

            if (!requiresArgumentsKey)
            {
                sb.AppendLine("        var __isArgumentsKeyComplete = true;");
                sb.AppendLine("        string? __argumentsKey = null;");
                return;
            }

            if (method.Parameters.Length == 0)
            {
                sb.AppendLine("        var __isArgumentsKeyComplete = true;");
                sb.AppendLine("        string? __argumentsKey = \"[]\";");
                return;
            }

            sb.AppendLine("        var __argumentsKeyParts = new string?[" + method.Parameters.Length + "];");
            sb.AppendLine("        var __isArgumentsKeyComplete = true;");

            for (var index = 0; index < method.Parameters.Length; index++)
            {
                var parameter = method.Parameters[index];

                if (IsType(parameter.Type, "System.Threading.CancellationToken"))
                {
                    sb.AppendLine("        __argumentsKeyParts[" + index + "] = \"\\\"<cancellation-token>\\\"\";");
                    continue;
                }

                if (parameter.RefKind == RefKind.Out
                    || parameter.Type.IsRefLikeType
                    || parameter.Type.TypeKind == TypeKind.Pointer
                    || parameter.Type is IFunctionPointerTypeSymbol
                    || TryGetSkipPlaceholder(parameter.Type, out _))
                {
                    sb.AppendLine("        __isArgumentsKeyComplete = false;");
                    continue;
                }

                var parameterName = EscapeIdentifier(parameter.Name);
                var keyVariableName = "__argumentKey" + index;
                sb.Append("        if (" + FormatFrameworkType("SourceGenerator.Runtime.Serialization.JsonUtil") + ".TryToCanonicalJson(").Append(parameterName).Append(", out var ").Append(keyVariableName).AppendLine("))");
                sb.AppendLine("        {");
                sb.Append("            __argumentsKeyParts[").Append(index).Append("] = ").Append(keyVariableName).AppendLine(";");
                sb.AppendLine("        }");
                sb.AppendLine("        else");
                sb.AppendLine("        {");
                sb.AppendLine("            __isArgumentsKeyComplete = false;");
                sb.AppendLine("        }");
            }

            sb.AppendLine("        string? __argumentsKey = __isArgumentsKeyComplete ? \"[\" + string.Join(\",\", __argumentsKeyParts) + \"]\" : null;");

        }


        /// <summary>
        /// 生成包含完整方法签名和运行时泛型类型的方法标识代码
        /// </summary>
        /// <param name="sb">目标源码构建器</param>
        /// <param name="targetType">当前代理目标类型</param>
        /// <param name="method">当前代理方法</param>
        /// <param name="typeFullName">代理目标类型完整名称</param>
        private void AppendMethodKey(StringBuilder sb, INamedTypeSymbol targetType, IMethodSymbol method, string typeFullName)
        {

            var parameterTypes = method.Parameters.Select(parameter =>
            {
                var modifier = parameter.RefKind switch
                {
                    RefKind.Ref => "ref ",
                    RefKind.Out => "out ",
                    RefKind.In => "in ",
                    RefKind.RefReadOnlyParameter => "ref readonly ",
                    _ => string.Empty
                };

                return modifier + parameter.Type.ToDisplayString(MethodKeyTypeDisplayFormat);
            });
            var assemblyName = targetType.ContainingAssembly?.Name ?? method.ContainingAssembly?.Name ?? string.Empty;
            var signature = assemblyName
                + "|"
                + typeFullName
                + "."
                + method.Name
                + "``"
                + method.Arity
                + "("
                + string.Join(",", parameterTypes)
                + ")";
            var escapedSignature = EscapeStringLiteral(signature);
            var runtimeTypeExpressions = new List<string>();

            if (GetAllTypeParameters(targetType).Count > 0)
            {
                runtimeTypeExpressions.Add("(GetType().BaseType?.AssemblyQualifiedName ?? GetType().BaseType?.FullName ?? \"" + EscapeStringLiteral(typeFullName) + "\")");
            }

            foreach (var typeParameter in method.TypeParameters)
            {
                var typeParameterName = EscapeIdentifier(typeParameter.Name);
                runtimeTypeExpressions.Add("(typeof(" + typeParameterName + ").AssemblyQualifiedName ?? typeof(" + typeParameterName + ").FullName ?? typeof(" + typeParameterName + ").Name)");
            }

            if (runtimeTypeExpressions.Count == 0)
            {
                sb.AppendLine("        var __methodKey = \"" + escapedSignature + "\";");
                return;
            }

            sb.AppendLine("        var __methodKey = \"" + escapedSignature + "|runtime=\" + string.Join(\"|\", new string[] { " + string.Join(", ", runtimeTypeExpressions) + " });");

        }


        /// <summary>
        /// 将文本转义为可安全写入 C# 字符串字面量的内容
        /// </summary>
        /// <param name="value">待转义文本</param>
        /// <returns>转义后的字符串字面量内容</returns>
        private static string EscapeStringLiteral(string value)
            => value.Replace("\\", "\\\\").Replace("\"", "\\\"");


        /// <summary>
        /// 获取当前方法可用于代理行为的取消令牌表达式
        /// </summary>
        /// <param name="method">当前代理方法</param>
        /// <returns>取消令牌参数表达式或默认值</returns>
        private static string GetCancellationTokenExpression(IMethodSymbol method)
        {

            var cancellationToken = method.Parameters.FirstOrDefault(parameter =>
                parameter.RefKind == RefKind.None
                && IsType(parameter.Type, "System.Threading.CancellationToken"));

            return cancellationToken is null
                ? "default"
                : EscapeIdentifier(cancellationToken.Name);

        }


        /// <summary>
        /// 构建代理类的类型参数声明部分 并统一包含外层和内层所有类型参数
        /// </summary>
        private static string BuildTypeParametersDecl(INamedTypeSymbol cls)
        {
            // 将所有外层和内层类型参数统一提升到代理类声明上
            var allTps = GetAllTypeParameters(cls);

            if (allTps.Count == 0) return string.Empty;
            return "<" + string.Join(", ", allTps.Select(tp => EscapeIdentifier(tp.Name))) + ">";
        }


        /// <summary>
        /// 构建代理类的类型参数约束部分 为所有提升的类型参数添加约束
        /// </summary>
        private string BuildTypeParameterConstraints(INamedTypeSymbol cls, string currentNamespace)
        {
            // 为所有提升后的类型参数应用约束 包含外层类型的参数
            var allTps = GetAllTypeParameters(cls);

            if (allTps.Count == 0) return string.Empty;
            var sb = new StringBuilder();
            foreach (var tp in allTps)
            {
                var parts = new List<string>();

                // 主约束在 C# 中互斥 unmanaged 已经包含 struct 语义
                if (tp.HasUnmanagedTypeConstraint)
                {
                    parts.Add("unmanaged");
                }
                else if (tp.HasValueTypeConstraint)
                {
                    parts.Add("struct");
                }
                else if (tp.HasReferenceTypeConstraint)
                {
                    parts.Add(tp.ReferenceTypeConstraintNullableAnnotation == NullableAnnotation.Annotated ? "class?" : "class");
                }
                else if (tp.HasNotNullConstraint)
                {
                    parts.Add("notnull");
                }

                // 然后输出具体的类型或接口约束
                foreach (var ct in tp.ConstraintTypes)
                {
                    parts.Add(FormatType(ct, currentNamespace));
                }

                // 最后输出 new() 约束
                if (tp.HasConstructorConstraint) parts.Add("new()");
                if (tp.AllowsRefLikeType) parts.Add("allows ref struct");
                if (parts.Count > 0)
                {
                    sb.Append("    where ").Append(EscapeIdentifier(tp.Name)).Append(" : ").Append(string.Join(", ", parts)).AppendLine();
                }
            }
            return sb.ToString();
        }


        /// <summary>
        /// 为给定类型生成适合作为 AddSource 提示名的安全字符串
        /// </summary>
        private static string GetSafeHintName(INamedTypeSymbol type)
        {
            var ns = type.ContainingNamespace.IsGlobalNamespace ? "global" : type.ContainingNamespace.ToDisplayString().Replace('.', '_');

            // 提示名中包含包含类型链及泛型个数和参数名 并避免使用特殊字符
            var parts = new List<string>();
            for (var t = type; t is not null; t = t.ContainingType)
            {
                var arity = t.TypeParameters.Length;
                var tpNames = arity > 0 ? "_" + string.Join("_", t.TypeParameters.Select(tp => tp.Name)) : string.Empty;
                var safeName = new string(t.Name.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray());
                var name = safeName + (arity > 0 ? "_g" + arity + tpNames : string.Empty);
                parts.Add(name);
            }
            parts.Reverse();
            return ns + "__" + string.Join("_", parts) + "__Proxy";
        }


        /// <summary>
        /// 收集包含类型链上的全部类型参数 按从外到内的顺序返回
        /// </summary>
        private static List<ITypeParameterSymbol> GetAllTypeParameters(INamedTypeSymbol type)
        {
            // 从最外层到最内层依次收集所有包含类型的类型参数
            var stack = new Stack<INamedTypeSymbol>();
            for (var t = type; t is not null; t = t.ContainingType)
                stack.Push(t);
            var list = new List<ITypeParameterSymbol>();
            foreach (var t in stack)
                list.AddRange(t.TypeParameters);
            return list;
        }

    }

}
