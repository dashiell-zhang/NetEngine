using Microsoft.CodeAnalysis;

namespace SourceGenerator.Core.Internal;

internal readonly struct HandlerContext
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

