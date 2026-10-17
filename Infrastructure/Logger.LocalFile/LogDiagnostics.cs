using Microsoft.Extensions.Logging;
using System.Diagnostics.Metrics;

namespace Logger.LocalFile;

/// <summary>
/// 提供独立于日志管线的运行指标，业务线程只更新原子计数
/// </summary>
internal static class LogDiagnostics
{

    private static readonly Meter meter = new("Logger.LocalFile");
    private static readonly long[] dropped = new long[7];
    private static readonly long[] failures = new long[4];
    private static readonly long[] lastReports = new long[4];
    private static readonly string[] operations = ["write", "recovery", "shutdown", "cleanup"];
    private static long bufferedCount;
    private static long bufferedBytes;


    /// <summary>
    /// 注册拉取式指标，避免在业务日志调用中执行监控回调
    /// </summary>
    static LogDiagnostics()
    {

        meter.CreateObservableCounter("logger.localfile.dropped", ObserveDropped, unit: "{log}");
        meter.CreateObservableCounter("logger.localfile.failures", ObserveFailures, unit: "{failure}");
        meter.CreateObservableGauge("logger.localfile.buffered", () => Interlocked.Read(ref bufferedCount), unit: "{log}");
        meter.CreateObservableGauge("logger.localfile.buffered.bytes", () => Interlocked.Read(ref bufferedBytes), unit: "By");

    }


    /// <summary>
    /// 按级别累计未接收或停机时未完成落盘的日志数量
    /// </summary>
    internal static void RecordDropped(LogLevel level)
    {

        Interlocked.Increment(ref dropped[Math.Clamp((int)level, 0, dropped.Length - 1)]);

    }


    /// <summary>
    /// 更新内存中待落盘日志的数量和估算字节数
    /// </summary>
    internal static void ChangeBuffered(long count, long bytes)
    {

        Interlocked.Add(ref bufferedCount, count);
        Interlocked.Add(ref bufferedBytes, bytes);

    }


    /// <summary>
    /// 在后台记录失败并限频输出阶段和异常类型，避免递归记录或泄露日志内容
    /// </summary>
    internal static void ReportFailure(string operation, Exception exception)
    {

        var index = Array.IndexOf(operations, operation);
        Interlocked.Increment(ref failures[index]);
        var now = Environment.TickCount64;
        var last = Interlocked.Read(ref lastReports[index]);
        if (last != 0 && now - last < 30000)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref lastReports[index], now, last) != last)
        {
            return;
        }

        try
        {
            Console.Error.WriteLine($"[Logger.LocalFile] {operation}: {exception.GetType().Name}; 请检查日志目录和监控指标");
        }
        catch (IOException)
        {
            // 标准错误不可写时仍保留失败指标
        }

    }


    /// <summary>
    /// 拉取各级别累计舍弃数量
    /// </summary>
    private static IEnumerable<Measurement<long>> ObserveDropped()
    {

        for (var index = 0; index < dropped.Length; index++)
        {
            yield return new Measurement<long>(Interlocked.Read(ref dropped[index]), new KeyValuePair<string, object?>("level", ((LogLevel)index).ToString()));
        }

    }


    /// <summary>
    /// 拉取各后台阶段累计失败次数
    /// </summary>
    private static IEnumerable<Measurement<long>> ObserveFailures()
    {

        for (var index = 0; index < failures.Length; index++)
        {
            yield return new Measurement<long>(Interlocked.Read(ref failures[index]), new KeyValuePair<string, object?>("operation", operations[index]));
        }

    }

}
