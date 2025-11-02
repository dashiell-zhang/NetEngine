using Microsoft.Extensions.Logging;
using System;

namespace SourceGenerator.Runtime;

public sealed class InvocationContext
{
    public required string Method { get; init; }
    public string? ArgsJson { get; init; }
    public required Guid TraceId { get; init; }
    public bool Log { get; init; }
    public bool Measure { get; init; }
    public IServiceProvider? ServiceProvider { get; init; }
    public ILogger? Logger { get; init; }
    public ProxyRuntime.CacheOptions? Cache { get; init; }
    public IReadOnlyList<IInvocationBehavior>? Behaviors { get; init; }
}
