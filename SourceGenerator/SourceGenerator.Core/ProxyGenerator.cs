using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SourceGenerator.Core.Internal;

namespace SourceGenerator.Core;

[Generator(LanguageNames.CSharp)]
public sealed class ProxyGenerator : IIncrementalGenerator
{
    private const string AutoProxyAttributeMetadataName = "SourceGenerator.Abstraction.Attributes.AutoProxyAttribute";


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
            // 接口：生成接口代理；类：生成接口、代理和 DI 扩展
            if (typeSymbol.TypeKind == TypeKind.Interface)
            {
                var ifaceHandler = new InterfaceProxyHandler();
                if (ifaceHandler.CanHandle(typeSymbol, attrData))
                {
                    ifaceHandler.Execute(new HandlerContext(spc, typeSymbol, attrData));
                }
            }
            else if (typeSymbol.TypeKind == TypeKind.Class)
            {
                var classHandler = new ClassProxyHandler();
                if (classHandler.CanHandle(typeSymbol, attrData))
                {
                    classHandler.Execute(new HandlerContext(spc, typeSymbol, attrData));
                }
            }
        });
    }

}






