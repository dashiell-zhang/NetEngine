namespace SourceGenerator.Runtime.Pipeline;

/// <summary>
/// 表示异步流调用完成后的有界结果快照
/// </summary>
public sealed class AsyncStreamResultSnapshot
{

    /// <summary>
    /// 默认允许保留的异步流元素数量
    /// </summary>
    public const int DefaultCaptureLimit = 100;


    /// <summary>
    /// 实际交付给调用方的元素总数
    /// </summary>
    public long EnumeratedCount { get; init; }


    /// <summary>
    /// 是否由底层异步流自然完成枚举
    /// </summary>
    public bool CompletedNaturally { get; init; }


    /// <summary>
    /// 是否存在未保留到快照中的元素
    /// </summary>
    public bool Truncated { get; init; }


    /// <summary>
    /// 按枚举顺序保留的有限元素快照
    /// </summary>
    public IReadOnlyList<object?> CapturedItems { get; init; } = Array.Empty<object?>();

}
