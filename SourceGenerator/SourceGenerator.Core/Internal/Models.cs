namespace SourceGenerator.Core.Internal;

internal sealed class ProxyOptionsModel
{
    public bool EnableLogging { get; set; } = true;
    public bool CaptureArguments { get; set; } = true;
    public bool MeasureTime { get; set; } = true;
}
