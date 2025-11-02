namespace SourceGenerator.Runtime.Options;

public sealed class CacheOptions
{
    public required string Seed { get; init; }
    public int TtlSeconds { get; init; }
}

