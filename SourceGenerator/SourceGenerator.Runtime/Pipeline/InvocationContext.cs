using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

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
    public IReadOnlyList<IInvocationBehavior>? Behaviors { get; init; }

    // Feature bag for behavior-specific data/configuration
    public Dictionary<Type, object> Features { get; } = new();

    public T? GetFeature<T>() where T : class
        => Features.TryGetValue(typeof(T), out var value) ? (T)value : null;

    public void SetFeature<T>(T feature) where T : class
    {
        if (feature is null) return;
        Features[typeof(T)] = feature;
    }
}
