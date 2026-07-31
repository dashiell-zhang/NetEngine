using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SourceGenerator.Core;

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

            var attrData = ctx.Attributes.FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == AutoProxyAttributeMetadataName);

            if (typeSymbol.TypeKind == TypeKind.Class)
            {
                var classHandler = new ClassProxyHandler();
                var validation = AutoProxyEligibility.Validate(typeSymbol);

                if (!validation.CanGenerate)
                {
                    spc.ReportDiagnostic(Diagnostic.Create(
                        InvalidAutoProxyTargetDescriptor,
                        typeSymbol.Locations.FirstOrDefault(),
                        typeSymbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                        validation.Reason ?? "目标类型不满足代理生成条件"));

                    return;
                }

                var hasInvalidMethod = false;
                foreach (var method in AutoProxyEligibility.GetUnsupportedAsyncByRefMethods(typeSymbol))
                {
                    hasInvalidMethod = true;
                    spc.ReportDiagnostic(Diagnostic.Create(
                        UnsupportedAsyncByRefMethodDescriptor,
                        method.Locations.FirstOrDefault() ?? typeSymbol.Locations.FirstOrDefault(),
                        method.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)));
                }

                foreach (var method in AutoProxyEligibility.GetUnsupportedPointerMethods(typeSymbol))
                {
                    hasInvalidMethod = true;
                    spc.ReportDiagnostic(Diagnostic.Create(
                        UnsupportedPointerMethodDescriptor,
                        method.Locations.FirstOrDefault() ?? typeSymbol.Locations.FirstOrDefault(),
                        method.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)));
                }

                foreach (var property in AutoProxyEligibility.GetUnsupportedPointerProperties(typeSymbol))
                {
                    hasInvalidMethod = true;
                    spc.ReportDiagnostic(Diagnostic.Create(
                        UnsupportedPointerMethodDescriptor,
                        property.Locations.FirstOrDefault() ?? typeSymbol.Locations.FirstOrDefault(),
                        property.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)));
                }

                foreach (var method in AutoProxyEligibility.GetUnsupportedRefLikeReturnMethods(typeSymbol))
                {
                    hasInvalidMethod = true;
                    spc.ReportDiagnostic(Diagnostic.Create(
                        UnsupportedRefLikeReturnMethodDescriptor,
                        method.Locations.FirstOrDefault() ?? typeSymbol.Locations.FirstOrDefault(),
                        method.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)));
                }

                foreach (var method in AutoProxyEligibility.GetUnsupportedDefaultInterfaceMethods(typeSymbol))
                {
                    hasInvalidMethod = true;
                    spc.ReportDiagnostic(Diagnostic.Create(
                        UnsupportedDefaultInterfaceMethodDescriptor,
                        method.Locations.FirstOrDefault() ?? typeSymbol.Locations.FirstOrDefault(),
                        typeSymbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                        method.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)));
                }

                foreach (var result in AutoProxyEligibility.GetUnsupportedProxyBehaviors(typeSymbol, compilation))
                {
                    hasInvalidMethod = true;
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

                if (hasInvalidMethod)
                {
                    return;
                }

                if (classHandler.CanHandle(typeSymbol, attrData))
                {
                    classHandler.Execute(new HandlerContext(spc, typeSymbol, attrData));
                }
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
    /// 处理器执行上下文 保存生成时的编译上下文 目标类型及相关特性信息
    /// </summary>
    private readonly struct HandlerContext
    {
        public SourceProductionContext Context { get; }

        public INamedTypeSymbol Type { get; }

        public AttributeData? Attribute { get; }


        public HandlerContext(SourceProductionContext context, INamedTypeSymbol type, AttributeData? attribute)
        {
            Context = context;
            Type = type;
            Attribute = attribute;
        }
    }


    /// <summary>
    /// 针对类类型的代理生成处理器 负责生成派生代理类源码
    /// </summary>
    private sealed class ClassProxyHandler
    {

        private const string ProxyBehaviorAttributeMetadataName = "SourceGenerator.Runtime.Attributes.ProxyBehaviorAttribute";


        private static readonly SymbolDisplayFormat SourceTypeDisplayFormat = SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
            | SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);

        private static readonly SymbolDisplayFormat MethodKeyTypeDisplayFormat = new(globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included, typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces, genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters, miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);


        /// <summary>
        /// 判断当前处理器是否可以处理给定类型
        /// </summary>
        public bool CanHandle(INamedTypeSymbol type, AttributeData? attribute)
            => AutoProxyEligibility.CanGenerateProxy(type);


        /// <summary>
        /// 执行代理生成逻辑 并将生成结果输出到编译上下文
        /// </summary>
        public void Execute(in HandlerContext ctx)
        {
            var src = GenerateDerivedProxy(ctx.Type);
            var hint = GetSafeHintName(ctx.Type) + ".g.cs";
            ctx.Context.AddSource(hint, src);
        }


        /// <summary>
        /// 为指定类生成派生代理类的完整源码
        /// </summary>
        private static string GenerateDerivedProxy(INamedTypeSymbol cls)
        {
            var ns = cls.ContainingNamespace.IsGlobalNamespace
                ? "NetEngine.Generated"
                : cls.ContainingNamespace.ToDisplayString();

            // 使用包含和不包含 global:: 前缀的完全限定类型名
            var classFull = FormatType(cls).Replace("global::", string.Empty);
            var classLocal = TrimCurrentNamespace(classFull, ns);
            var proxyName = AutoProxyEligibility.GetProxyTypeName(cls);
            var typeParamsDecl = BuildTypeParametersDecl(cls);
            var typeParamConstraints = BuildTypeParameterConstraints(cls);

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
            sb.AppendLine("using SourceGenerator.Runtime;");
            sb.AppendLine("using SourceGenerator.Runtime.Pipeline;");
            sb.AppendLine("using SourceGenerator.Runtime.Pipeline.Behaviors;");
            sb.AppendLine("using SourceGenerator.Runtime.Options;");
            sb.AppendLine();
            sb.Append("namespace ").Append(ns).AppendLine(";");
            sb.AppendLine();


            // 代理类继承原始实现类型 只列出需要在代理中显式实现的接口
            var minimalInterfaces = GetInterfacesNeedingExplicitImplementations(cls);
            var ifaceList = minimalInterfaces.Length == 0 ? string.Empty : ", " + string.Join(", ", minimalInterfaces.Select(i => TrimCurrentNamespace(i, ns)));
            var proxyAccessibility = AutoProxyEligibility.GetProxyAccessibilityText(cls);
            
            // 使用完全限定的基类类型 避免全局命名空间或嵌套类型的解析问题
            sb.Append(proxyAccessibility).Append(" sealed class ").Append(proxyName).Append(typeParamsDecl).Append(" : ").Append(classLocal).Append(ifaceList).AppendLine();
            
            if (!string.IsNullOrWhiteSpace(typeParamConstraints)) sb.Append(typeParamConstraints);
            
            sb.AppendLine("{")
              .AppendLine("    private readonly IServiceProvider? __sp;")
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
                    ? "IServiceProvider sp"
                    : "IServiceProvider sp, " + paramList;

                AppendConstructorAttributes(sb, ctor, includeActivatorUtilitiesConstructor: true);
                sb.Append("    public ").Append(proxyName).Append('(').Append(withSpParams).Append(')').AppendLine()
                  .AppendLine("        : base(" + argList + ")")
                  .AppendLine("    {")
                  .AppendLine("        __sp = sp;")
                  .AppendLine("    }")
                  .AppendLine()
                  .AppendLine();
            }

            // 为当前类型直接声明的方法和有效继承代理方法生成重写实现
            foreach (var method in AutoProxyEligibility.GetEffectiveProxyMethods(cls))
            {
                AppendDerivedOverride(sb, cls, method, classFull, callTarget: "base", ns);
            }

            // 为接口成员生成显式实现 使通过接口调用时也能被拦截
            foreach (var iface in cls.AllInterfaces)
            {
                foreach (var member in iface.GetMembers())
                {
                    switch (member)
                    {
                        case IMethodSymbol m:
                            if (!AutoProxyEligibility.ShouldGenerateExplicitInterfaceMethod(cls, m, out var impl))
                                break;
                            AppendExplicitInterfaceMethod(sb, cls, iface, m, impl, classFull, ns);
                            break;
                        
                        case IPropertySymbol p:
                            {
                                if (!AutoProxyEligibility.ShouldGenerateExplicitInterfaceProperty(cls, p, out _))
                                    break;

                                AppendExplicitInterfaceProperty(sb, iface, p, cls, ns);
                            }
                            break;
                        
                        case IEventSymbol e:
                            {
                                if (!AutoProxyEligibility.ShouldGenerateExplicitInterfaceEvent(cls, e, out _))
                                    break;

                                AppendExplicitInterfaceEvent(sb, iface, e, cls, ns);
                            }
                            break;
                    }
                }
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
        /// <param name="invokeBefore">是否在包装器开始枚举时执行 OnBefore</param>
        private static void AppendAsyncStreamWrapper(StringBuilder sb, string itemType, string sourceExpression, bool sourceIsParameter, bool invokeBefore)
        {

            var sourceParameter = sourceIsParameter ? "IAsyncEnumerable<" + itemType + "> __s, " : string.Empty;

            sb.AppendLine("        async IAsyncEnumerable<" + itemType + "> __streamWrapper(" + sourceParameter + "[global::System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken __enumerationCancellationToken = default){");
            sb.AppendLine("            var __items = new List<object?>(16);");

            if (invokeBefore)
            {
                sb.AppendLine("            try { foreach (var __f in __filters) __f.OnBefore(__ctx); } catch (Exception __ex) { foreach (var __f in __filters) __f.OnException(__ctx, __ex); throw; }");
            }

            sb.AppendLine("            IAsyncEnumerator<" + itemType + "> __e;");
            sb.AppendLine("            try { __e = " + sourceExpression + ".GetAsyncEnumerator(__enumerationCancellationToken); } catch (Exception __ex) { foreach (var __f in __filters) __f.OnException(__ctx, __ex); throw; }");
            sb.AppendLine("            var __faulted = false;");
            sb.AppendLine("            try");
            sb.AppendLine("            {");
            sb.AppendLine("                while (true)");
            sb.AppendLine("                {");
            sb.AppendLine("                    bool __moved;");
            sb.AppendLine("                    try { __moved = await __e.MoveNextAsync(); } catch (Exception __ex) { __faulted = true; foreach (var __f in __filters) __f.OnException(__ctx, __ex); throw; }");
            sb.AppendLine("                    if (!__moved) break;");
            sb.AppendLine("                    " + itemType + " __item;");
            sb.AppendLine("                    try { __item = __e.Current; } catch (Exception __ex) { __faulted = true; foreach (var __f in __filters) __f.OnException(__ctx, __ex); throw; }");
            sb.AppendLine("                    try { __items.Add(JsonUtil.ToObject(JsonUtil.ToJson(__item))); } catch { __items.Add(Convert.ToString(__item)); }");
            sb.AppendLine("                    yield return __item;");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            sb.AppendLine("            finally");
            sb.AppendLine("            {");
            sb.AppendLine("                try { await __e.DisposeAsync(); } catch (Exception __ex) { if (!__faulted) { __faulted = true; foreach (var __f in __filters) __f.OnException(__ctx, __ex); } throw; }");
            sb.AppendLine("                if (!__faulted) { try { foreach (var __f in __filters) __f.OnAfter(__ctx, __items); } catch (Exception __ex) { foreach (var __f in __filters) __f.OnException(__ctx, __ex); throw; } }");
            sb.AppendLine("            }");
            sb.AppendLine("        }");

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
        private static void AppendDerivedOverride(StringBuilder sb, INamedTypeSymbol targetType, IMethodSymbol method, string typeFullName, string callTarget, string currentNamespace)
        {
            
            var isGenericTask = method.ReturnType is INamedTypeSymbol nts && nts.IsGenericType && IsType(nts.ConstructedFrom, "System.Threading.Tasks.Task");
            
            var isTask = method.ReturnType is INamedTypeSymbol nts0 && !nts0.IsGenericType && IsType(nts0, "System.Threading.Tasks.Task");
            
            var isGenericValueTask = method.ReturnType is INamedTypeSymbol nts2 && nts2.IsGenericType && IsType(nts2.ConstructedFrom, "System.Threading.Tasks.ValueTask");
            
            var isValueTask = method.ReturnType is INamedTypeSymbol nts3 && !nts3.IsGenericType && IsType(nts3, "System.Threading.Tasks.ValueTask");
            
            var isAsyncEnumerable = (method.ReturnType is INamedTypeSymbol nts4 && nts4.IsGenericType && IsType(nts4.ConstructedFrom, "System.Collections.Generic.IAsyncEnumerable"))
                || method.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).StartsWith("global::System.Collections.Generic.IAsyncEnumerable<", System.StringComparison.Ordinal);
            
            var isTaskOfAsyncEnumerable = isGenericTask && ((INamedTypeSymbol)method.ReturnType).TypeArguments[0] is INamedTypeSymbol t1 && (
                (t1.IsGenericType && IsType(t1.ConstructedFrom, "System.Collections.Generic.IAsyncEnumerable")) ||
                t1.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).StartsWith("global::System.Collections.Generic.IAsyncEnumerable<", System.StringComparison.Ordinal));
            
            var isValueTaskOfAsyncEnumerable = isGenericValueTask && ((INamedTypeSymbol)method.ReturnType).TypeArguments[0] is INamedTypeSymbol t2 && (
                (t2.IsGenericType && IsType(t2.ConstructedFrom, "System.Collections.Generic.IAsyncEnumerable")) ||
                t2.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).StartsWith("global::System.Collections.Generic.IAsyncEnumerable<", System.StringComparison.Ordinal));
            
            if (!isTaskOfAsyncEnumerable)
            {
                var rtText2 = method.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                if (rtText2.StartsWith("global::System.Threading.Tasks.Task<global::System.Collections.Generic.IAsyncEnumerable<", StringComparison.Ordinal))
                    isTaskOfAsyncEnumerable = true;
            }
            
            if (!isValueTaskOfAsyncEnumerable)
            {
                var rtText2 = method.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                if (rtText2.StartsWith("global::System.Threading.Tasks.ValueTask<global::System.Collections.Generic.IAsyncEnumerable<", StringComparison.Ordinal))
                    isValueTaskOfAsyncEnumerable = true;
            }

            var returnTypeFullText = FormatType(method.ReturnType);
            var returnType = TrimCurrentNamespace(returnTypeFullText, currentNamespace);
            returnType = TrimCurrentNamespace(returnType, currentNamespace);
            
            var methodName = EscapeIdentifier(method.Name);
            var rawMethodName = method.Name;
            
            var typeParams = method.TypeParameters.Length > 0 ? "<" + string.Join(", ", method.TypeParameters.Select(tp => EscapeIdentifier(tp.Name))) + ">" : string.Empty;
            
            var paramList = string.Join(", ", method.Parameters.Select(p => FormatParameter(p, includeDefault: true, currentNamespace)));
            
            var argList = string.Join(", ", method.Parameters.Select(FormatArgument));

            var isByRefReturn = method.ReturnsByRef || method.ReturnsByRefReadonly;
            
            var hasByRefAny = isByRefReturn || method.Parameters.Any(p => p.RefKind != RefKind.None || p.Type.IsRefLikeType);
            
            var needsAsync = hasByRefAny && (isTask || isGenericTask || isValueTask || isGenericValueTask);
            
            var sigReturnType = method.ReturnsVoid
                ? "void"
                : isByRefReturn
                    ? (method.ReturnsByRefReadonly ? "ref readonly " : "ref ") + returnType
                    : returnType;

            var accessibilityText = GetOverrideAccessibilityText(targetType, method);
            sb.Append("    ").Append(accessibilityText).Append(" override ").Append(needsAsync ? "async " : string.Empty).Append(sigReturnType).Append(' ').Append(methodName).Append(typeParams)
              .Append('(').Append(paramList).Append(')').AppendLine()
              .AppendLine("    {");

            if (method.Parameters.Length > 0)
            {
                var dictType = TrimCurrentNamespace("System.Collections.Generic.Dictionary<string, string?>", currentNamespace);
                sb.AppendLine("        var __argsDict = new " + dictType + "(" + method.Parameters.Length + ");");
                
                foreach (var p in method.Parameters)
                {
                    var isOut = p.RefKind == RefKind.Out;
                    
                    var isRefLike = p.Type.IsRefLikeType;
                    if (isOut || isRefLike)
                    {
                        sb.Append("        __argsDict[\"").Append(p.Name).Append("\"] = null;").AppendLine();
                    }
                    else
                    {
                        if (TryGetSkipPlaceholder(p.Type, out var __ph))
                        {
                            sb.Append("        __argsDict[\"").Append(p.Name).Append("\"] = \"")
                              .Append(__ph.Replace("\\", "\\\\").Replace("\"", "\\\""))
                              .Append("\";").AppendLine();
                        }
                        else
                        {
                            sb.Append("        try { __argsDict[\"").Append(p.Name).Append("\"] = JsonUtil.ToJson(")
                              .Append(EscapeIdentifier(p.Name)).Append("); } catch { __argsDict[\"").Append(p.Name).Append("\"] = Convert.ToString(")
                              .Append(EscapeIdentifier(p.Name)).Append("); }").AppendLine();
                        }
                    }
                }
                sb.AppendLine("        object? __argsObj = __argsDict;");
            }
            else
            {
                sb.AppendLine("        object? __argsObj = null;");
            }

            var requiresArgumentsKey = method.GetAttributes().Any(AutoProxyEligibility.RequiresArgumentsKey);
            AppendArgumentsKeySnapshot(sb, method, requiresArgumentsKey);

            sb.AppendLine("        var __logMethod = \"" + typeFullName + "\" + \"." + rawMethodName + "\";");
            AppendMethodKey(sb, targetType, method, typeFullName);
            sb.AppendLine("        var __logger = __sp?.GetService<ILoggerFactory>()?.CreateLogger(\"ProxyRuntime\");");

            var hasByRef = hasByRefAny;
            
            var behaviorSnippets = new List<string>();
            
            var optionsSetters = new List<string>();
            
            foreach (var a in method.GetAttributes())
            {
                if (TryGetBehaviorSpec(a, out var behaviorFull, out var optInit))
                {
                    behaviorSnippets.Add($"new {behaviorFull}()");
                    if (!string.IsNullOrEmpty(optInit)) optionsSetters.Add(optInit!);
                }
            }
            
            sb.AppendLine("        var __behaviors = new IInvocationAsyncBehavior[] { " + string.Join(", ", behaviorSnippets) + " };");
            
            var __hasReturn = isGenericTask || isGenericValueTask || (!isTask && !isValueTask && !method.ReturnsVoid) || isAsyncEnumerable;
            var __allowRet = __hasReturn && IsAllowReturnSerialization(method);
            var cancellationTokenExpression = GetCancellationTokenExpression(method);

            sb.AppendLine("        var __ctx = new InvocationContext { Method = __logMethod, MethodKey = __methodKey, Args = __argsObj, ArgumentsKey = __argumentsKey, IsArgumentsKeyComplete = __isArgumentsKeyComplete, CancellationToken = " + cancellationTokenExpression + ", TraceId = Guid.CreateVersion7(), Log = true, HasReturnValue = " + (__hasReturn ? "true" : "false") + ", AllowReturnSerialization = " + (__allowRet ? "true" : "false") + ", ServiceProvider = __sp, Logger = __logger, Behaviors = __behaviors };");
            
            if (optionsSetters.Count > 0) sb.AppendLine("        " + string.Join("\n        ", optionsSetters));

            if (hasByRef || isAsyncEnumerable || isTaskOfAsyncEnumerable || isValueTaskOfAsyncEnumerable)
            {
                sb.AppendLine("        var __filters = new List<IInvocationBehavior>();");
                sb.AppendLine("        foreach (var __b in __behaviors) { if (__b is IInvocationBehavior __f) __filters.Add(__f); }");
                
                if (isAsyncEnumerable)
                {
                    // 对异步枚举结果进行包装 在迭代过程中收集每个元素的 JSON 并在完成后统一记录日志
                    var tArg = FormatType(((INamedTypeSymbol)method.ReturnType).TypeArguments[0], currentNamespace);
                    var callExpr = callTarget + "." + methodName + typeParams + "(" + argList + ")";
                    
                    AppendAsyncStreamWrapper(sb, tArg, callExpr, sourceIsParameter: false, invokeBefore: true);
                    sb.AppendLine($"        return __streamWrapper();");
                    sb.AppendLine("    }").AppendLine().AppendLine();
                    return; // 方法体已经在前面生成 此处直接返回结束代码生成
                }
                if (isTask)
                {
                    var updateSnippet = BuildArgsUpdateSnippet(method);
                    
                    sb.AppendLine("        try");
                    sb.AppendLine("        {");
                    sb.AppendLine("            foreach (var __f in __filters) __f.OnBefore(__ctx);");
                    sb.AppendLine($"            await {callTarget}.{methodName}{typeParams}({argList});");
                    
                    if (!string.IsNullOrEmpty(updateSnippet)) sb.AppendLine("            " + updateSnippet);
                    
                    sb.AppendLine("            foreach (var __f in __filters) __f.OnAfter(__ctx, null);");
                    sb.AppendLine("        }");
                    sb.AppendLine("        catch (Exception __ex)");
                    sb.AppendLine("        {");
                    
                    if (!string.IsNullOrEmpty(updateSnippet)) sb.AppendLine("            " + updateSnippet);
                    
                    sb.AppendLine("            foreach (var __f in __filters) __f.OnException(__ctx, __ex);");
                    sb.AppendLine("            throw;");
                    sb.AppendLine("        }");
                }
                else if (isTaskOfAsyncEnumerable)
                {
                    var tItem = FormatType(((INamedTypeSymbol)((INamedTypeSymbol)method.ReturnType).TypeArguments[0]).TypeArguments[0], currentNamespace);
                    var callExpr = callTarget + "." + methodName + typeParams + "(" + argList + ")";
                    
                    AppendAsyncStreamWrapper(sb, tItem, "__s", sourceIsParameter: true, invokeBefore: true);
                    sb.AppendLine($"        async Task<IAsyncEnumerable<{tItem}>> __taskWrapper(){{");
                    sb.AppendLine("            try");
                    sb.AppendLine("            {");
                    sb.AppendLine($"                var __s = await {callExpr};");
                    sb.AppendLine("                return __streamWrapper(__s);");
                    sb.AppendLine("            }");
                    sb.AppendLine("            catch (Exception __ex)");
                    sb.AppendLine("            {");
                    sb.AppendLine("                try { foreach (var __f in __filters) __f.OnBefore(__ctx); } catch (Exception __beforeEx) { foreach (var __f in __filters) __f.OnException(__ctx, __beforeEx); throw; }");
                    sb.AppendLine("                foreach (var __f in __filters) __f.OnException(__ctx, __ex);");
                    sb.AppendLine("                throw;");
                    sb.AppendLine("            }");
                    sb.AppendLine("        }");
                    sb.AppendLine("        return __taskWrapper();");
                }
                else if (isGenericTask)
                {
                    var updateSnippet = BuildArgsUpdateSnippet(method);
                    
                    sb.AppendLine("        try");
                    sb.AppendLine("        {");
                    sb.AppendLine("            foreach (var __f in __filters) __f.OnBefore(__ctx);");
                    sb.AppendLine($"            var __res = await {callTarget}.{methodName}{typeParams}({argList});");
                    
                    if (!string.IsNullOrEmpty(updateSnippet)) sb.AppendLine("            " + updateSnippet);
                    
                    sb.AppendLine("            foreach (var __f in __filters) __f.OnAfter(__ctx, __res);");
                    sb.AppendLine("            return __res;");
                    sb.AppendLine("        }");
                    sb.AppendLine("        catch (Exception __ex)");
                    sb.AppendLine("        {");
                    
                    if (!string.IsNullOrEmpty(updateSnippet)) sb.AppendLine("            " + updateSnippet);
                    
                    sb.AppendLine("            foreach (var __f in __filters) __f.OnException(__ctx, __ex);");
                    sb.AppendLine("            throw;");
                    sb.AppendLine("        }");
                }
                else if (isValueTaskOfAsyncEnumerable)
                {
                    var tItem = FormatType(((INamedTypeSymbol)((INamedTypeSymbol)method.ReturnType).TypeArguments[0]).TypeArguments[0], currentNamespace);
                    var callExpr = callTarget + "." + methodName + typeParams + "(" + argList + ")";
                    AppendAsyncStreamWrapper(sb, tItem, "__s", sourceIsParameter: true, invokeBefore: true);
                    sb.AppendLine($"        async ValueTask<IAsyncEnumerable<{tItem}>> __valueTaskWrapper(){{");
                    sb.AppendLine("            try");
                    sb.AppendLine("            {");
                    sb.AppendLine($"                var __s = await {callExpr};");
                    sb.AppendLine("                return __streamWrapper(__s);");
                    sb.AppendLine("            }");
                    sb.AppendLine("            catch (Exception __ex)");
                    sb.AppendLine("            {");
                    sb.AppendLine("                try { foreach (var __f in __filters) __f.OnBefore(__ctx); } catch (Exception __beforeEx) { foreach (var __f in __filters) __f.OnException(__ctx, __beforeEx); throw; }");
                    sb.AppendLine("                foreach (var __f in __filters) __f.OnException(__ctx, __ex);");
                    sb.AppendLine("                throw;");
                    sb.AppendLine("            }");
                    sb.AppendLine("        }");
                    sb.AppendLine("        return __valueTaskWrapper();");
                }
                else if (isValueTask)
                {
                    var updateSnippet = BuildArgsUpdateSnippet(method);
                    
                    sb.AppendLine("        try");
                    sb.AppendLine("        {");
                    sb.AppendLine("            foreach (var __f in __filters) __f.OnBefore(__ctx);");
                    sb.AppendLine($"            await {callTarget}.{methodName}{typeParams}({argList});");
                    
                    if (!string.IsNullOrEmpty(updateSnippet)) sb.AppendLine("            " + updateSnippet);
                    
                    sb.AppendLine("            foreach (var __f in __filters) __f.OnAfter(__ctx, null);");
                    sb.AppendLine("            return;");
                    sb.AppendLine("        }");
                    sb.AppendLine("        catch (Exception __ex)");
                    
                    sb.AppendLine("        {");
                    
                    if (!string.IsNullOrEmpty(updateSnippet)) sb.AppendLine("            " + updateSnippet);
                    
                    sb.AppendLine("            foreach (var __f in __filters) __f.OnException(__ctx, __ex);");
                    sb.AppendLine("            throw;");
                    sb.AppendLine("        }");
                }
                else if (isGenericValueTask)
                {
                    var updateSnippet = BuildArgsUpdateSnippet(method);
                    sb.AppendLine("        try");
                    sb.AppendLine("        {");
                    sb.AppendLine("            foreach (var __f in __filters) __f.OnBefore(__ctx);");
                    sb.AppendLine($"            var __res = await {callTarget}.{methodName}{typeParams}({argList});");
                    
                    if (!string.IsNullOrEmpty(updateSnippet)) sb.AppendLine("            " + updateSnippet);
                    
                    sb.AppendLine("            foreach (var __f in __filters) __f.OnAfter(__ctx, __res);");
                    sb.AppendLine("            return __res;");
                    sb.AppendLine("        }");
                    sb.AppendLine("        catch (Exception __ex)");
                    sb.AppendLine("        {");
                    
                    if (!string.IsNullOrEmpty(updateSnippet)) sb.AppendLine("            " + updateSnippet);
                    
                    sb.AppendLine("            foreach (var __f in __filters) __f.OnException(__ctx, __ex);");
                    sb.AppendLine("            throw;");
                    sb.AppendLine("        }");
                }
                else if (isByRefReturn)
                {
                    var refLocalModifier = method.ReturnsByRefReadonly ? "ref readonly var" : "ref var";
                    sb.AppendLine("        try { foreach (var __f in __filters) __f.OnBefore(__ctx); " + refLocalModifier + " __ret = ref " + callTarget + "." + methodName + typeParams + "(" + argList + "); var __snap = __ret; foreach (var __f in __filters) __f.OnAfter(__ctx, __snap); return ref __ret; } catch (Exception __ex) { foreach (var __f in __filters) __f.OnException(__ctx, __ex); throw; }");
                }
                else if (method.ReturnsVoid)
                {
                    var updateSnippet = BuildArgsUpdateSnippet(method);
                    
                    sb.AppendLine("        try { foreach (var __f in __filters) __f.OnBefore(__ctx); " + callTarget + "." + methodName + typeParams + "(" + argList + "); " + updateSnippet + " foreach (var __f in __filters) __f.OnAfter(__ctx, null); } catch (Exception __ex) { foreach (var __f in __filters) __f.OnException(__ctx, __ex); throw; }");
                    sb.AppendLine("        return;");
                }
                else
                {
                    var updateSnippet = BuildArgsUpdateSnippet(method);
                    
                    sb.AppendLine("        try { foreach (var __f in __filters) __f.OnBefore(__ctx); var __ret = " + callTarget + "." + methodName + typeParams + "(" + argList + "); " + updateSnippet + " foreach (var __f in __filters) __f.OnAfter(__ctx, __ret); return __ret; } catch (Exception __ex) { foreach (var __f in __filters) __f.OnException(__ctx, __ex); throw; }");
                }
            }
            else
            {
                if (isAsyncEnumerable)
                {
                    var tArg = FormatType(((INamedTypeSymbol)method.ReturnType).TypeArguments[0], currentNamespace);
                    
                    var callExpr = callTarget + "." + methodName + typeParams + "(" + argList + ")";
                    sb.AppendLine("        var __behaviors = new IInvocationAsyncBehavior[] { " + string.Join(", ", behaviorSnippets) + " };")
                      .AppendLine("        var __ctx = new InvocationContext { Method = __logMethod, MethodKey = __methodKey, Args = __argsObj, ArgumentsKey = __argumentsKey, IsArgumentsKeyComplete = __isArgumentsKeyComplete, CancellationToken = " + cancellationTokenExpression + ", TraceId = System.Guid.CreateVersion7(), Log = true, HasReturnValue = true, AllowReturnSerialization = true, ServiceProvider = __sp, Logger = __logger, Behaviors = __behaviors };");
                    sb.AppendLine("        var __filters = new List<IInvocationBehavior>();");
                    sb.AppendLine("        foreach (var __b in __behaviors) { if (__b is IInvocationBehavior __f) __filters.Add(__f); }");
                    AppendAsyncStreamWrapper(sb, tArg, callExpr, sourceIsParameter: false, invokeBefore: true);
                    sb.AppendLine($"        return __streamWrapper();");
                    sb.AppendLine("    }").AppendLine().AppendLine();
                    return;
                }

            var runtime = "ProxyRuntime";
                
                if (isTask)
                {
                    sb.AppendLine($"        return {runtime}.ExecuteTask(__ctx, () => {callTarget}.{methodName}{typeParams}({argList}));");
                }
                else if (isGenericTask)
                {
                    var tArg = FormatType(((INamedTypeSymbol)method.ReturnType).TypeArguments[0], currentNamespace);
                    sb.AppendLine($"        return {runtime}.ExecuteAsync<{tArg}>(__ctx, () => {callTarget}.{methodName}{typeParams}({argList}));");
                }
                else if (isValueTask)
                {
                    sb.AppendLine($"        return {runtime}.ExecuteTask(__ctx, () => {callTarget}.{methodName}{typeParams}({argList}));");
                }
                else if (isGenericValueTask)
                {
                    var tArg = FormatType(((INamedTypeSymbol)method.ReturnType).TypeArguments[0], currentNamespace);
                    sb.AppendLine($"        return {runtime}.ExecuteAsync<{tArg}>(__ctx, () => {callTarget}.{methodName}{typeParams}({argList}) );");
                }
                else if (method.ReturnsVoid)
                {
                    sb.AppendLine($"        {runtime}.Execute<object?>(__ctx, () => {{ {callTarget}.{methodName}{typeParams}({argList}); return ValueTask.FromResult<object?>(null); }});");
                    
                    sb.AppendLine("        return;");
                }
                else
                {
                    // 兜底保护 如果编译期检测未识别为 Task 或 Task<T> 则根据返回类型字符串进行判断
                    if (returnTypeFullText.StartsWith("global::System.Threading.Tasks.Task<", StringComparison.Ordinal))
                    {
                        var tArgText = returnTypeFullText.Substring("global::System.Threading.Tasks.Task<".Length);
                        
                        tArgText = tArgText.EndsWith(">", StringComparison.Ordinal) ? tArgText.Substring(0, tArgText.Length - 1) : tArgText;
                        tArgText = TrimCurrentNamespace(tArgText, currentNamespace);
                        
                        sb.AppendLine($"        return {runtime}.ExecuteAsync<{tArgText}>(__ctx, () => {callTarget}.{methodName}{typeParams}({argList}));");
                    }
                    else if (string.Equals(returnTypeFullText, "global::System.Threading.Tasks.Task", StringComparison.Ordinal))
                    {
                        sb.AppendLine($"        return {runtime}.ExecuteTask(__ctx, () => {callTarget}.{methodName}{typeParams}({argList}));");
                    }
                    // 兜底保护 如果编译期检测未识别为 ValueTask 或 ValueTask<T> 则根据返回类型字符串进行判断
                    else if (returnTypeFullText.StartsWith("global::System.Threading.Tasks.ValueTask<", StringComparison.Ordinal))
                    {
                        var tArgText = returnTypeFullText.Substring("global::System.Threading.Tasks.ValueTask<".Length);
                        
                        tArgText = tArgText.EndsWith(">", StringComparison.Ordinal) ? tArgText.Substring(0, tArgText.Length - 1) : tArgText;
                        tArgText = TrimCurrentNamespace(tArgText, currentNamespace);
                        
                        sb.AppendLine($"        return {runtime}.ExecuteAsync<{tArgText}>(__ctx, () => {callTarget}.{methodName}{typeParams}({argList}) );");
                    }
                    else if (string.Equals(returnTypeFullText, "global::System.Threading.Tasks.ValueTask", StringComparison.Ordinal))
                    {
                        sb.AppendLine($"        return {runtime}.ExecuteTask(__ctx, () => {callTarget}.{methodName}{typeParams}({argList}));");
                    }
                    else
                    {
                        sb.AppendLine($"        return {runtime}.Execute<{returnType}>(__ctx, () => ValueTask.FromResult({callTarget}.{methodName}{typeParams}({argList})));");
                    }
                }
            }

            sb.AppendLine("    }").AppendLine().AppendLine();
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
        private static void AppendExplicitInterfaceMethod(StringBuilder sb, INamedTypeSymbol cls, INamedTypeSymbol iface, IMethodSymbol method, IMethodSymbol? impl, string typeFullName, string currentNamespace)
        {
            // 在编译期根据接口方法和实现方法上的特性构建行为管道配置
            var isGenericTask = method.ReturnType is INamedTypeSymbol nts && nts.IsGenericType && IsType(nts.ConstructedFrom, "System.Threading.Tasks.Task");

            var isTask = method.ReturnType is INamedTypeSymbol nts0 && !nts0.IsGenericType && IsType(nts0, "System.Threading.Tasks.Task");

            var isGenericValueTask = method.ReturnType is INamedTypeSymbol nts2 && nts2.IsGenericType && IsType(nts2.ConstructedFrom, "System.Threading.Tasks.ValueTask");

            var isValueTask = method.ReturnType is INamedTypeSymbol nts3 && !nts3.IsGenericType && IsType(nts3, "System.Threading.Tasks.ValueTask");

            var isAsyncEnumerable = (method.ReturnType is INamedTypeSymbol nts4 && nts4.IsGenericType && IsType(nts4.ConstructedFrom, "System.Collections.Generic.IAsyncEnumerable"))
                || method.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).StartsWith("global::System.Collections.Generic.IAsyncEnumerable<", System.StringComparison.Ordinal);

            var isTaskOfAsyncEnumerable = isGenericTask && ((INamedTypeSymbol)method.ReturnType).TypeArguments[0] is INamedTypeSymbol t1 && (
                (t1.IsGenericType && IsType(t1.ConstructedFrom, "System.Collections.Generic.IAsyncEnumerable")) ||
                t1.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).StartsWith("global::System.Collections.Generic.IAsyncEnumerable<", System.StringComparison.Ordinal));

            var isValueTaskOfAsyncEnumerable = isGenericValueTask && ((INamedTypeSymbol)method.ReturnType).TypeArguments[0] is INamedTypeSymbol t2 && (
                (t2.IsGenericType && IsType(t2.ConstructedFrom, "System.Collections.Generic.IAsyncEnumerable")) ||
                t2.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).StartsWith("global::System.Collections.Generic.IAsyncEnumerable<", System.StringComparison.Ordinal));

            var returnTypeFullText = FormatType(method.ReturnType);
            var returnType = TrimCurrentNamespace(returnTypeFullText, currentNamespace);

            var ifaceDisplay = FormatType(iface, currentNamespace);

            var methodName = EscapeIdentifier(method.Name);
            var rawMethodName = method.Name;

            var typeParams = method.TypeParameters.Length > 0 ? "<" + string.Join(", ", method.TypeParameters.Select(tp => EscapeIdentifier(tp.Name))) + ">" : string.Empty;

            var paramList = string.Join(", ", method.Parameters.Select(p => FormatParameter(p, includeDefault: false, currentNamespace)));

            var argList = string.Join(", ", method.Parameters.Select(FormatArgument));


            var isByRefReturn = method.ReturnsByRef || method.ReturnsByRefReadonly;

            var hasByRef2_head = isByRefReturn || method.Parameters.Any(p => p.RefKind != RefKind.None || p.Type.IsRefLikeType);

            var needsAsync2 = hasByRef2_head && (isTask || isGenericTask || isValueTask || isGenericValueTask);

            var sigReturnType = method.ReturnsVoid
                ? "void"
                : isByRefReturn
                    ? (method.ReturnsByRefReadonly ? "ref readonly " : "ref ") + returnType
                    : returnType;

            sb.Append("    ").Append(needsAsync2 ? "async " : string.Empty).Append(sigReturnType).Append(' ').Append(ifaceDisplay).Append('.').Append(methodName).Append(typeParams)
              .Append('(').Append(paramList).Append(')').AppendLine()
              .AppendLine("    {");

            if (method.Parameters.Length > 0)
            {
                var dictType = TrimCurrentNamespace("System.Collections.Generic.Dictionary<string, string?>", currentNamespace);
                sb.AppendLine("        var __argsDict = new " + dictType + "(" + method.Parameters.Length + ");");

                foreach (var p in method.Parameters)
                {
                    var isOut = p.RefKind == RefKind.Out;
                    var isRefLike = p.Type.IsRefLikeType;

                    if (isOut || isRefLike)
                    {
                        sb.Append("        __argsDict[\"").Append(p.Name).Append("\"] = null;").AppendLine();
                    }
                    else
                    {
                        if (TryGetSkipPlaceholder(p.Type, out var __ph))
                        {
                            sb.Append("        __argsDict[\"").Append(p.Name).Append("\"] = \"")
                              .Append(__ph.Replace("\\", "\\\\").Replace("\"", "\\\""))
                              .Append("\";").AppendLine();
                        }
                        else
                        {
                            sb.Append("        try { __argsDict[\"").Append(p.Name).Append("\"] = JsonUtil.ToJson(")
                              .Append(EscapeIdentifier(p.Name)).Append("); } catch { __argsDict[\"").Append(p.Name).Append("\"] = Convert.ToString(")
                              .Append(EscapeIdentifier(p.Name)).Append("); }").AppendLine();
                        }
                    }
                }

                sb.AppendLine("        object? __argsObj = __argsDict;");
            }
            else
            {
                sb.AppendLine("        object? __argsObj = null;");
            }

            var requiresArgumentsKey = method.GetAttributes().Any(AutoProxyEligibility.RequiresArgumentsKey)
                || (impl?.GetAttributes().Any(AutoProxyEligibility.RequiresArgumentsKey) ?? false);
            AppendArgumentsKeySnapshot(sb, method, requiresArgumentsKey);

            sb.AppendLine("        var __logMethod = \"" + typeFullName + "\" + \"." + rawMethodName + "\";");
            AppendMethodKey(sb, cls, method, typeFullName);
            sb.AppendLine("        var __logger = __sp?.GetService<ILoggerFactory>()?.CreateLogger(\"ProxyRuntime\");");

            var hasByRef2 = isByRefReturn || method.Parameters.Any(p => p.RefKind != RefKind.None || p.Type.IsRefLikeType);

            var behaviorSnippets = new List<string>();

            var optionsSetters = new List<string>();

            foreach (var a in method.GetAttributes())
            {
                if (TryGetBehaviorSpec(a, out var behaviorFull, out var optInit))
                {
                    behaviorSnippets.Add($"new {behaviorFull}()");

                    if (!string.IsNullOrEmpty(optInit)) optionsSetters.Add(optInit!);
                }
            }

            if (impl is not null)
            {
                foreach (var a in impl.GetAttributes())
                {
                    if (TryGetBehaviorSpec(a, out var behaviorFull, out var optInit))
                    {
                        behaviorSnippets.Add($"new {behaviorFull}()");

                        if (!string.IsNullOrEmpty(optInit)) optionsSetters.Add(optInit!);
                    }
                }
            }

            sb.AppendLine("        var __behaviors = new IInvocationAsyncBehavior[] { " + string.Join(", ", behaviorSnippets) + " };");

            var __hasReturn = isGenericTask || isGenericValueTask || (!isTask && !isValueTask && !method.ReturnsVoid) || isAsyncEnumerable;

            var __allowRet = __hasReturn && IsAllowReturnSerialization(method);
            var cancellationTokenExpression = GetCancellationTokenExpression(method);

            sb.AppendLine("        var __ctx = new InvocationContext { Method = __logMethod, MethodKey = __methodKey, Args = __argsObj, ArgumentsKey = __argumentsKey, IsArgumentsKeyComplete = __isArgumentsKeyComplete, CancellationToken = " + cancellationTokenExpression + ", TraceId = Guid.CreateVersion7(), Log = true, HasReturnValue = " + (__hasReturn ? "true" : "false") + ", AllowReturnSerialization = " + (__allowRet ? "true" : "false") + ", ServiceProvider = __sp, Logger = __logger, Behaviors = __behaviors };");
            
            if (optionsSetters.Count > 0) sb.AppendLine("        " + string.Join("\n        ", optionsSetters));

            var call = "base." + methodName + typeParams + "(" + argList + ")";
            
            if (hasByRef2 || isAsyncEnumerable || isTaskOfAsyncEnumerable || isValueTaskOfAsyncEnumerable)
            {
                sb.AppendLine("        var __filters = new List<IInvocationBehavior>();");
                sb.AppendLine("        foreach (var __b in __behaviors) { if (__b is IInvocationBehavior __f) __filters.Add(__f); }");
                
                if (isAsyncEnumerable)
                {
                    var tArg = FormatType(((INamedTypeSymbol)method.ReturnType).TypeArguments[0], currentNamespace);
                    var callExpr2 = "base." + methodName + typeParams + "(" + argList + ")";
                    AppendAsyncStreamWrapper(sb, tArg, callExpr2, sourceIsParameter: false, invokeBefore: true);
                    sb.AppendLine($"        return __streamWrapper();");
                    sb.AppendLine("    }").AppendLine().AppendLine();
                    return;
                }
                
                if (isTask)
                {
                    var updateSnippet = BuildArgsUpdateSnippet(method);
                    sb.AppendLine("        try");
                    sb.AppendLine("        {");
                    sb.AppendLine("            foreach (var __f in __filters) __f.OnBefore(__ctx);");
                    sb.AppendLine($"            await {call};");
                    if (!string.IsNullOrEmpty(updateSnippet)) sb.AppendLine("            " + updateSnippet);
                    sb.AppendLine("            foreach (var __f in __filters) __f.OnAfter(__ctx, null);");
                    sb.AppendLine("        }");
                    sb.AppendLine("        catch (Exception __ex)");
                    sb.AppendLine("        {");
                    if (!string.IsNullOrEmpty(updateSnippet)) sb.AppendLine("            " + updateSnippet);
                    sb.AppendLine("            foreach (var __f in __filters) __f.OnException(__ctx, __ex);");
                    sb.AppendLine("            throw;");
                    sb.AppendLine("        }");
                }
                else if (isTaskOfAsyncEnumerable)
                {
                    var tItem = FormatType(((INamedTypeSymbol)((INamedTypeSymbol)method.ReturnType).TypeArguments[0]).TypeArguments[0], currentNamespace);
                    AppendAsyncStreamWrapper(sb, tItem, "__s", sourceIsParameter: true, invokeBefore: true);
                    sb.AppendLine($"        async Task<IAsyncEnumerable<{tItem}>> __taskWrapper(){{");
                    sb.AppendLine("            try");
                    sb.AppendLine("            {");
                    sb.AppendLine($"                var __s = await {call};");
                    sb.AppendLine("                return __streamWrapper(__s);");
                    sb.AppendLine("            }");
                    sb.AppendLine("            catch (Exception __ex)");
                    sb.AppendLine("            {");
                    sb.AppendLine("                try { foreach (var __f in __filters) __f.OnBefore(__ctx); } catch (Exception __beforeEx) { foreach (var __f in __filters) __f.OnException(__ctx, __beforeEx); throw; }");
                    sb.AppendLine("                foreach (var __f in __filters) __f.OnException(__ctx, __ex);");
                    sb.AppendLine("                throw;");
                    sb.AppendLine("            }");
                    sb.AppendLine("        }");
                    sb.AppendLine("        return __taskWrapper();");
                }
                else if (isGenericTask)
                {
                    var updateSnippet = BuildArgsUpdateSnippet(method);
                    sb.AppendLine("        try");
                    sb.AppendLine("        {");
                    sb.AppendLine("            foreach (var __f in __filters) __f.OnBefore(__ctx);");
                    sb.AppendLine($"            var __res = await {call};");
                    if (!string.IsNullOrEmpty(updateSnippet)) sb.AppendLine("            " + updateSnippet);
                    sb.AppendLine("            foreach (var __f in __filters) __f.OnAfter(__ctx, __res);");
                    sb.AppendLine("            return __res;");
                    sb.AppendLine("        }");
                    sb.AppendLine("        catch (Exception __ex)");
                    sb.AppendLine("        {");
                    if (!string.IsNullOrEmpty(updateSnippet)) sb.AppendLine("            " + updateSnippet);
                    sb.AppendLine("            foreach (var __f in __filters) __f.OnException(__ctx, __ex);");
                    sb.AppendLine("            throw;");
                    sb.AppendLine("        }");
                }
                else if (isValueTaskOfAsyncEnumerable)
                {
                    var tItem = FormatType(((INamedTypeSymbol)((INamedTypeSymbol)method.ReturnType).TypeArguments[0]).TypeArguments[0], currentNamespace);
                    AppendAsyncStreamWrapper(sb, tItem, "__s", sourceIsParameter: true, invokeBefore: true);
                    sb.AppendLine($"        async ValueTask<IAsyncEnumerable<{tItem}>> __valueTaskWrapper(){{");
                    sb.AppendLine("            try");
                    sb.AppendLine("            {");
                    sb.AppendLine($"                var __s = await {call};");
                    sb.AppendLine("                return __streamWrapper(__s);");
                    sb.AppendLine("            }");
                    sb.AppendLine("            catch (Exception __ex)");
                    sb.AppendLine("            {");
                    sb.AppendLine("                try { foreach (var __f in __filters) __f.OnBefore(__ctx); } catch (Exception __beforeEx) { foreach (var __f in __filters) __f.OnException(__ctx, __beforeEx); throw; }");
                    sb.AppendLine("                foreach (var __f in __filters) __f.OnException(__ctx, __ex);");
                    sb.AppendLine("                throw;");
                    sb.AppendLine("            }");
                    sb.AppendLine("        }");
                    sb.AppendLine("        return __valueTaskWrapper();");
                }
                else if (isValueTask)
                {
                    var updateSnippet = BuildArgsUpdateSnippet(method);
                    sb.AppendLine("        try");
                    sb.AppendLine("        {");
                    sb.AppendLine("            foreach (var __f in __filters) __f.OnBefore(__ctx);");
                    sb.AppendLine($"            await {call};");
                    if (!string.IsNullOrEmpty(updateSnippet)) sb.AppendLine("            " + updateSnippet);
                    sb.AppendLine("            foreach (var __f in __filters) __f.OnAfter(__ctx, null);");
                    sb.AppendLine("            return;");
                    sb.AppendLine("        }");
                    sb.AppendLine("        catch (Exception __ex)");
                    sb.AppendLine("        {");
                    if (!string.IsNullOrEmpty(updateSnippet)) sb.AppendLine("            " + updateSnippet);
                    sb.AppendLine("            foreach (var __f in __filters) __f.OnException(__ctx, __ex);");
                    sb.AppendLine("            throw;");
                    sb.AppendLine("        }");
                }
                else if (isGenericValueTask)
                {
                    var updateSnippet = BuildArgsUpdateSnippet(method);
                    sb.AppendLine("        try");
                    sb.AppendLine("        {");
                    sb.AppendLine("            foreach (var __f in __filters) __f.OnBefore(__ctx);");
                    sb.AppendLine($"            var __res = await {call};");
                    if (!string.IsNullOrEmpty(updateSnippet)) sb.AppendLine("            " + updateSnippet);
                    sb.AppendLine("            foreach (var __f in __filters) __f.OnAfter(__ctx, __res);");
                    sb.AppendLine("            return __res;");
                    sb.AppendLine("        }");
                    sb.AppendLine("        catch (Exception __ex)");
                    sb.AppendLine("        {");
                    if (!string.IsNullOrEmpty(updateSnippet)) sb.AppendLine("            " + updateSnippet);
                    sb.AppendLine("            foreach (var __f in __filters) __f.OnException(__ctx, __ex);");
                    sb.AppendLine("            throw;");
                    sb.AppendLine("        }");
                }
                else if (isByRefReturn)
                {
                    var updateSnippet = BuildArgsUpdateSnippet(method);
                    var refLocalModifier = method.ReturnsByRefReadonly ? "ref readonly var" : "ref var";
                    sb.AppendLine("        try { foreach (var __f in __filters) __f.OnBefore(__ctx); " + refLocalModifier + " __ret = ref " + call + "; var __snap = __ret; " + updateSnippet + " foreach (var __f in __filters) __f.OnAfter(__ctx, __snap); return ref __ret; } catch (Exception __ex) { " + updateSnippet + " foreach (var __f in __filters) __f.OnException(__ctx, __ex); throw; }");
                }
                else if (method.ReturnsVoid)
                {
                    var updateSnippet = BuildArgsUpdateSnippet(method);
                    sb.AppendLine("        try { foreach (var __f in __filters) __f.OnBefore(__ctx); " + call + "; " + updateSnippet + " foreach (var __f in __filters) __f.OnAfter(__ctx, null); } catch (Exception __ex) { " + updateSnippet + " foreach (var __f in __filters) __f.OnException(__ctx, __ex); throw; }");
                    sb.AppendLine("        return;");
                }
                else
                {
                    var updateSnippet = BuildArgsUpdateSnippet(method);
                    sb.AppendLine("        try { foreach (var __f in __filters) __f.OnBefore(__ctx); var __ret = " + call + "; " + updateSnippet + " foreach (var __f in __filters) __f.OnAfter(__ctx, __ret); return __ret; } catch (Exception __ex) { " + updateSnippet + " foreach (var __f in __filters) __f.OnException(__ctx, __ex); throw; }");
                }
            }
            else
            {
                if (isAsyncEnumerable)
                {
                    var tArg = FormatType(((INamedTypeSymbol)method.ReturnType).TypeArguments[0], currentNamespace);
                    tArg = TrimCurrentNamespace(tArg, currentNamespace);
                    var callExpr3 = "base." + methodName + typeParams + "(" + argList + ")";
                    sb.AppendLine("        var __behaviors = new IInvocationAsyncBehavior[] { " + string.Join(", ", behaviorSnippets) + " };")
                      .AppendLine("        var __ctx = new InvocationContext { Method = __logMethod, MethodKey = __methodKey, Args = __argsObj, ArgumentsKey = __argumentsKey, IsArgumentsKeyComplete = __isArgumentsKeyComplete, CancellationToken = " + cancellationTokenExpression + ", TraceId = Guid.CreateVersion7(), Log = true, HasReturnValue = true, AllowReturnSerialization = true, ServiceProvider = __sp, Logger = __logger, Behaviors = __behaviors };");
                    sb.AppendLine("        var __filters = new List<IInvocationBehavior>();");
                    sb.AppendLine("        foreach (var __b in __behaviors) { if (__b is IInvocationBehavior __f) __filters.Add(__f); }");
                    AppendAsyncStreamWrapper(sb, tArg, callExpr3, sourceIsParameter: false, invokeBefore: true);
                    sb.AppendLine($"        return __streamWrapper();");
                    sb.AppendLine("    }").AppendLine().AppendLine();
                    return;
                }

                var runtime = "ProxyRuntime";
                
                if (isTask)
                {
                    sb.AppendLine($"        return {runtime}.ExecuteTask(__ctx, () => {call});");
                }
                else if (isGenericTask)
                {
                    var tArg = FormatType(((INamedTypeSymbol)method.ReturnType).TypeArguments[0], currentNamespace);
                    
                    sb.AppendLine($"        return {runtime}.ExecuteAsync<{tArg}>(__ctx, () => {call});");
                }
                else if (isValueTask)
                {
                    sb.AppendLine($"        return {runtime}.ExecuteTask(__ctx, () => {call});");
                }
                else if (isGenericValueTask)
                {
                    var tArg = FormatType(((INamedTypeSymbol)method.ReturnType).TypeArguments[0], currentNamespace);
                    
                    sb.AppendLine($"        return {runtime}.ExecuteAsync<{tArg}>(__ctx, () => {call} );");
                }
                else if (method.ReturnsVoid)
                {
                    sb.AppendLine($"        {runtime}.Execute<object?>(__ctx, () => {{ {call}; return ValueTask.FromResult<object?>(null); }});");
                    sb.AppendLine("        return;");
                }
                else
                {
                    // 兜底保护 如果编译期检测未识别为 Task 或 Task<T> 则根据返回类型字符串进行判断
                    if (returnTypeFullText.StartsWith("global::System.Threading.Tasks.Task<", StringComparison.Ordinal))
                    {
                        var tArgText = returnTypeFullText.Substring("global::System.Threading.Tasks.Task<".Length);
                       
                        tArgText = tArgText.EndsWith(">", StringComparison.Ordinal) ? tArgText.Substring(0, tArgText.Length - 1) : tArgText;
                        tArgText = TrimCurrentNamespace(tArgText, currentNamespace);
                       
                        sb.AppendLine($"        return {runtime}.ExecuteAsync<{tArgText}>(__ctx, () => {call});");
                    }
                    else if (string.Equals(returnTypeFullText, "global::System.Threading.Tasks.Task", StringComparison.Ordinal))
                    {
                        sb.AppendLine($"        return {runtime}.ExecuteTask(__ctx, () => {call});");
                    }
                    // 兜底保护 如果编译期检测未识别为 ValueTask 或 ValueTask<T> 则根据返回类型字符串进行判断
                    else if (returnTypeFullText.StartsWith("global::System.Threading.Tasks.ValueTask<", StringComparison.Ordinal))
                    {
                        var tArgText = returnTypeFullText.Substring("global::System.Threading.Tasks.ValueTask<".Length);
                        
                        tArgText = tArgText.EndsWith(">", StringComparison.Ordinal) ? tArgText.Substring(0, tArgText.Length - 1) : tArgText;
                        tArgText = TrimCurrentNamespace(tArgText, currentNamespace);
                        
                        sb.AppendLine($"        return {runtime}.ExecuteAsync<{tArgText}>(__ctx, () => {call} );");
                    }
                    else if (string.Equals(returnTypeFullText, "global::System.Threading.Tasks.ValueTask", StringComparison.Ordinal))
                    {
                        sb.AppendLine($"        return {runtime}.ExecuteTask(__ctx, () => {call});");
                    }
                    else
                    {
                        sb.AppendLine($"        return {runtime}.Execute<{returnType}>(__ctx, () => ValueTask.FromResult({call}));");
                    }
                }
            }

            sb.AppendLine("    }").AppendLine().AppendLine();
        }


        /// <summary>
        /// 为接口属性生成在代理类中的显式接口实现 直接转发到基类属性
        /// </summary>
        private static void AppendExplicitInterfaceProperty(StringBuilder sb, INamedTypeSymbol iface, IPropertySymbol prop, INamedTypeSymbol cls, string currentNamespace)
        {
            var typeName = FormatType(prop.Type, currentNamespace);

            var returnModifier = prop.ReturnsByRefReadonly
                ? "ref readonly "
                : prop.ReturnsByRef
                    ? "ref "
                    : string.Empty;

            var ifaceDisplay = FormatType(iface, currentNamespace);

            var propName = EscapeIdentifier(prop.Name);

            var declarationName = prop.IsIndexer
                ? ifaceDisplay + ".this[" + string.Join(", ", prop.Parameters.Select(parameter => FormatParameter(parameter, includeDefault: false, currentNamespace))) + "]"
                : ifaceDisplay + "." + propName;

            var baseAccess = prop.IsIndexer
                ? "base[" + string.Join(", ", prop.Parameters.Select(FormatArgument)) + "]"
                : "base." + propName;

            sb.Append("    ").Append(returnModifier).Append(typeName).Append(' ').Append(declarationName).AppendLine()
              .AppendLine("    {");

            if (prop.GetMethod is not null)
            {
                var getterRefModifier = prop.ReturnsByRef || prop.ReturnsByRefReadonly ? "ref " : string.Empty;
                sb.AppendLine("        get => " + getterRefModifier + baseAccess + ";");
            }

            if (prop.SetMethod is not null)
            {
                var setterKeyword = prop.SetMethod.IsInitOnly ? "init" : "set";
                sb.AppendLine("        " + setterKeyword + " => " + baseAccess + " = value;");
            }

            sb.AppendLine("    }");
        }


        /// <summary>
        /// 为接口事件生成在代理类中的显式接口实现 直接转发到基类事件
        /// </summary>
        private static void AppendExplicitInterfaceEvent(StringBuilder sb, INamedTypeSymbol iface, IEventSymbol ev, INamedTypeSymbol cls, string currentNamespace)
        {
            var typeName = FormatType(ev.Type, currentNamespace);

            var ifaceDisplay = FormatType(iface, currentNamespace);

            var eventName = EscapeIdentifier(ev.Name);

            sb.Append("    event ").Append(typeName).Append(' ').Append(ifaceDisplay).Append('.').Append(eventName).AppendLine()
              .AppendLine("    {")
              .AppendLine("        add => base." + eventName + " += value;")
              .AppendLine("        remove => base." + eventName + " -= value;")
              .AppendLine("    }");
        }


        /// <summary>
        /// 从行为特性中提取行为类型和可选配置初始化代码
        /// </summary>
        private static bool TryGetBehaviorSpec(AttributeData a, out string behaviorFull, out string? optSetter)
        {
            behaviorFull = string.Empty;
            optSetter = null;

            if (!AutoProxyEligibility.TryGetProxyBehaviorTypes(a, out var behaviorTypeSymbol, out var optionsTypeSymbol) || behaviorTypeSymbol is null)
                return false;

            behaviorFull = FormatType(behaviorTypeSymbol, string.Empty);

            if (optionsTypeSymbol is not null)
            {
                var assigns = new List<string>();
                foreach (var kv in a.NamedArguments)
                {
                    var propName = kv.Key;
                    var prop = AutoProxyEligibility.FindOptionsProperty(optionsTypeSymbol, propName);
                    if (prop is null) continue;
                    if (!AutoProxyEligibility.TryFormatAttributeArgument(kv.Value, out var lit)) continue;
                    assigns.Add(EscapeIdentifier(propName) + " = " + lit);
                }
                var optFull = FormatType(optionsTypeSymbol, string.Empty);
                if (optFull.StartsWith("Options.", StringComparison.Ordinal))
                {
                    optFull = optFull.Substring("Options.".Length);
                }
                var init = assigns.Count > 0 ? " { " + string.Join(", ", assigns) + " }" : "()";
                optSetter = $"__ctx.SetFeature(new {optFull}{init});";
            }

            return true;
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
        private static string FormatParameter(IParameterSymbol p, bool includeDefault, string? currentNamespace = null)
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
        private static string FormatDefaultValue(IParameterSymbol parameter)
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

            return parameter.Type.TypeKind == TypeKind.Enum
                ? "(" + FormatType(parameter.Type) + ")" + literal
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
        /// 将类型符号格式化为可安全输出到 C# 源码中的类型文本
        /// </summary>
        private static string FormatType(ITypeSymbol type, string? currentNamespace = null)
        {

            var typeName = type.ToDisplayString(SourceTypeDisplayFormat);

            if (!string.IsNullOrEmpty(currentNamespace))
            {
                typeName = TrimCurrentNamespace(typeName, currentNamespace!);
            }

            return typeName;

        }


        /// <summary>
        /// 如果类型在当前命名空间下，裁剪掉重复的命名空间前缀提升可读性
        /// </summary>
        private static string TrimCurrentNamespace(string typeName, string currentNamespace)
        {
            if (string.IsNullOrEmpty(typeName)) return typeName;

            // 先移除 global:: 前缀
            typeName = typeName.Replace("global::", string.Empty);

            // 移除当前命名空间前缀
            if (!string.IsNullOrEmpty(currentNamespace))
            {
                typeName = typeName.Replace(currentNamespace + ".", string.Empty);
            }

            // 常用 BCL 命名空间前缀去除，便于输出简洁类型名
            typeName = typeName.Replace("System.Collections.Generic.", string.Empty)
                               .Replace("System.Threading.Tasks.", string.Empty)
                               ;
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
            // 常用类型简化（确保生成代码不出现 Net.* / Threading.* 这种根命名空间）
            typeName = typeName.Replace("System.Net.Http.HttpClient", "HttpClient")
                               .Replace("System.Net.Http.HttpRequestMessage", "HttpRequestMessage")
                               .Replace("System.Net.Http.HttpResponseMessage", "HttpResponseMessage")
                               .Replace("System.Threading.CancellationToken", "CancellationToken")
                               .Replace("System.Threading.CancellationTokenSource", "CancellationTokenSource");
            // 常用 Runtime 命名空间前缀去除
            typeName = typeName.Replace("SourceGenerator.Runtime.Pipeline.Behaviors.", string.Empty)
                               .Replace("SourceGenerator.Runtime.Pipeline.", string.Empty)
                               .Replace("SourceGenerator.Runtime.", string.Empty);

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
        private static bool IsAllowReturnSerialization(IMethodSymbol method)
        {
            var rt = method.ReturnType;

            // 异步流类型使用占位符记录日志 视为可记录类型
            if ((rt is INamedTypeSymbol nts4 && nts4.IsGenericType && IsType(nts4.ConstructedFrom, "System.Collections.Generic.IAsyncEnumerable"))
                || rt.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).StartsWith("global::System.Collections.Generic.IAsyncEnumerable<", StringComparison.Ordinal))
                return true;

            // 无返回值的 Task 或 ValueTask
            if (rt is INamedTypeSymbol ntsTask && !ntsTask.IsGenericType && IsType(ntsTask, "System.Threading.Tasks.Task"))
                return false;

            if (rt is INamedTypeSymbol ntsVt && !ntsVt.IsGenericType && IsType(ntsVt, "System.Threading.Tasks.ValueTask"))
                return false;

            // 对 Task<T> 和 ValueTask<T> 进行解包 使用其泛型参数做判断
            if (rt is INamedTypeSymbol ntsG && ntsG.IsGenericType)
            {
                if (IsType(ntsG.ConstructedFrom, "System.Threading.Tasks.Task") || IsType(ntsG.ConstructedFrom, "System.Threading.Tasks.ValueTask"))
                {
                    var tArg = ntsG.TypeArguments[0];
                    return IsReturnTypeLoggableCore(tArg);
                }
            }

            if (method.ReturnsVoid) return false;

            return IsReturnTypeLoggableCore(rt);
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
        /// 为带 ref 或 out 参数的方法构建调用后刷新参数快照的代码片段
        /// </summary>
        private static string BuildArgsUpdateSnippet(IMethodSymbol method)
        {
            var updates = new List<string>();

            foreach (var p in method.Parameters)
            {
                if (p.RefKind != RefKind.None)
                {
                    // 调用完成后刷新 ref out in 参数在参数字典中的值
                    if (TryGetSkipPlaceholder(p.Type, out var ph))
                    {
                        var escaped = ph.Replace("\\", "\\\\").Replace("\"", "\\\"");
                        updates.Add($"__argsDict[\"{p.Name}\"] = \"{escaped}\";");
                    }
                    else
                    {
                        var parameterName = EscapeIdentifier(p.Name);
                        updates.Add($"try {{ __argsDict[\"{p.Name}\"] = JsonUtil.ToJson({parameterName}); }} catch {{ __argsDict[\"{p.Name}\"] = Convert.ToString({parameterName}); }}");
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
        private static void AppendArgumentsKeySnapshot(StringBuilder sb, IMethodSymbol method, bool requiresArgumentsKey)
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
                sb.Append("        if (JsonUtil.TryToCanonicalJson(").Append(parameterName).Append(", out var ").Append(keyVariableName).AppendLine("))");
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
        private static void AppendMethodKey(StringBuilder sb, INamedTypeSymbol targetType, IMethodSymbol method, string typeFullName)
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
        private static string BuildTypeParameterConstraints(INamedTypeSymbol cls)
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
                    parts.Add(FormatType(ct));
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


        /// <summary>
        /// 获取需要在代理类中生成显式实现的接口列表
        /// </summary>
        private static string[] GetInterfacesNeedingExplicitImplementations(INamedTypeSymbol cls)
        {
            var set = new HashSet<string>();
            foreach (var iface in cls.AllInterfaces)
            {
                foreach (var member in iface.GetMembers())
                {
                    switch (member)
                    {
                        case IMethodSymbol m:
                            if (AutoProxyEligibility.ShouldGenerateExplicitInterfaceMethod(cls, m, out _))
                                set.Add(FormatType(iface));
                            break;
                        case IPropertySymbol p:
                            if (AutoProxyEligibility.ShouldGenerateExplicitInterfaceProperty(cls, p, out _))
                                set.Add(FormatType(iface));
                            break;
                        case IEventSymbol e:
                            if (AutoProxyEligibility.ShouldGenerateExplicitInterfaceEvent(cls, e, out _))
                                set.Add(FormatType(iface));
                            break;
                    }
                }
            }
            return set.ToArray();
        }
    }

}
