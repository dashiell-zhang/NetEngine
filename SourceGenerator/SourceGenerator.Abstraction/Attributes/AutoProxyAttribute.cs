namespace SourceGenerator.Abstraction.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, Inherited = false, AllowMultiple = false)]
    public sealed class AutoProxyAttribute : Attribute
    {
    }
}
