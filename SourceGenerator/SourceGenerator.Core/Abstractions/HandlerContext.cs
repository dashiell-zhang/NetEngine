using Microsoft.CodeAnalysis;

namespace SourceGenerator.Core.Abstractions;

internal readonly struct HandlerContext
{
    public SourceProductionContext Context { get; }
    public INamedTypeSymbol Type { get; }
    public AttributeData? Attribute { get; }
    public ProxyOptionsModel Options { get; }

    public HandlerContext(SourceProductionContext context, INamedTypeSymbol type, AttributeData? attribute, ProxyOptionsModel options)
    {
        Context = context;
        Type = type;
        Attribute = attribute;
        Options = options;
    }
}

