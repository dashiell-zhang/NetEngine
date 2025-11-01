using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SourceGenerator.Core.Abstractions;

namespace SourceGenerator.Core;

[Generator(LanguageNames.CSharp)]
public sealed class ProxyGenerator : IIncrementalGenerator
{
    private const string AutoProxyAttributeMetadataName = "SourceGenerator.Abstraction.Attributes.AutoProxyAttribute";

    // 单一处理器：接口代理

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider.ForAttributeWithMetadataName(
            AutoProxyAttributeMetadataName,
            static (node, _) => node is InterfaceDeclarationSyntax or ClassDeclarationSyntax,
            static (syntaxContext, _) => syntaxContext
        );

        var combined = candidates.Combine(context.CompilationProvider);

        context.RegisterSourceOutput(combined, static (spc, tuple) =>
        {
            var (ctx, compilation) = (tuple.Left, tuple.Right);
            if (ctx.TargetSymbol is not INamedTypeSymbol typeSymbol)
                return;

            var attrData = ctx.Attributes.FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == AutoProxyAttributeMetadataName);
            var options = ParseOptions(attrData);

            var handler = new InterfaceProxyHandler();
            if (handler.CanHandle(typeSymbol, attrData))
            {
                handler.Execute(new HandlerContext(spc, typeSymbol, attrData, options));
            }
        });
    }

    private static ProxyOptionsModel ParseOptions(AttributeData? attr)
    {
        var model = new ProxyOptionsModel();
        if (attr == null) return model;
        foreach (var arg in attr.NamedArguments)
        {
            switch (arg.Key)
            {
                case "EnableLogging":
                    if (arg.Value.Value is bool b1) model.EnableLogging = b1;
                    break;
                case "CaptureArguments":
                    if (arg.Value.Value is bool b2) model.CaptureArguments = b2;
                    break;
                case "MeasureTime":
                    if (arg.Value.Value is bool b3) model.MeasureTime = b3;
                    break;
            }
        }
        return model;
    }
}






