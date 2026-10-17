using Common;
using Logger.LocalFile.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Buffers;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace Logger.LocalFile;

/// <summary>
/// 非阻塞接收日志并在后台批量落盘，满载时优先使用重要日志预留缓冲
/// </summary>
public sealed class LocalFileLogWriter : BackgroundService
{

    private static readonly byte[] newLine = Encoding.UTF8.GetBytes(Environment.NewLine);
    private readonly LocalFileStore store = new(Path.Combine(Directory.GetCurrentDirectory(), "Logs"));
    private readonly Channel<BufferedLog> channel;
    private readonly Channel<BufferedLog> importantChannel;
    private readonly long maxBufferedBytes;
    private readonly long importantMaxBufferedBytes;
    private readonly int batchSize;
    private readonly TimeSpan flushInterval;
    private readonly CancellationTokenSource abort = new();
    // 仅由后台消费者使用，跨批次复用常规大小的 UTF-8 缓冲
    private ArrayBufferWriter<byte> utf8Buffer = new(4096);
    private long bufferedBytes;
    private long importantBufferedBytes;
    private int accepting = 1;


    /// <summary>
    /// 创建共用队列和重要日志预留队列并固定本次运行的缓冲配置
    /// </summary>
    public LocalFileLogWriter(IOptions<LoggerSetting> options)
    {

        var settings = options.Value;
        channel = CreateChannel(settings.QueueCapacity);
        importantChannel = CreateChannel(settings.ImportantQueueCapacity);
        maxBufferedBytes = settings.MaxBufferedBytes;
        importantMaxBufferedBytes = settings.ImportantMaxBufferedBytes;
        batchSize = settings.BatchSize;
        flushInterval = settings.FlushInterval;
        LogDiagnostics.ChangeBuffered(0, 0);

    }


    /// <summary>
    /// 只尝试立即入队，不等待队列空位或执行存储操作
    /// </summary>
    internal void Enqueue(string category, string content, LogLevel level)
    {

        var log = new LogMessage(category, GetLevelName(level), content);
        var timestamp = DateTimeOffset.UtcNow;
        var bytes = 256L + 2L * (content.Length + (long)category.Length + log.Level.Length);
        if (Volatile.Read(ref accepting) != 0)
        {
            if (TryEnqueue(channel, log, timestamp, level, bytes, false, ref bufferedBytes, maxBufferedBytes))
            {
                return;
            }

            if (level >= LogLevel.Warning && TryEnqueue(importantChannel, log, timestamp, level, bytes, true, ref importantBufferedBytes, importantMaxBufferedBytes))
            {
                return;
            }
        }

        LogDiagnostics.RecordDropped(level);

    }


    /// <summary>
    /// 在估算内存预算内原子预留空间并尝试写入队列
    /// </summary>
    private static bool TryEnqueue(Channel<BufferedLog> target, LogMessage log, DateTimeOffset timestamp, LogLevel level, long bytes, bool important, ref long usedBytes, long limit)
    {

        var used = Interlocked.Read(ref usedBytes);
        while (bytes <= limit - used)
        {
            var observed = Interlocked.CompareExchange(ref usedBytes, used + bytes, used);
            if (observed == used)
            {
                LogDiagnostics.ChangeBuffered(1, bytes);
                if (target.Writer.TryWrite(new BufferedLog(log, timestamp, level, bytes, important)))
                {
                    return true;
                }

                Interlocked.Add(ref usedBytes, -bytes);
                LogDiagnostics.ChangeBuffered(-1, -bytes);
                return false;
            }

            used = observed;
        }

        return false;

    }


    /// <summary>
    /// 创建仅通过 TryWrite 写入的有界队列
    /// </summary>
    private static Channel<BufferedLog> CreateChannel(int capacity)
    {

        return Channel.CreateBounded<BufferedLog>(new BoundedChannelOptions(capacity)
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
            FullMode = BoundedChannelFullMode.Wait
        });

    }


    /// <summary>
    /// 消费有界队列并保留失败批次，按退避间隔重试落盘
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {

        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, abort.Token);
        var token = cancellation.Token;
        var buffer = new List<BufferedLog>(batchSize);
        using var writer = new Utf8JsonWriter(utf8Buffer, new JsonWriterOptions { Encoder = JsonHelper.SerializeOpts.Encoder, Indented = JsonHelper.SerializeOpts.WriteIndented });
        using var timer = new PeriodicTimer(flushInterval);
        var tickTask = timer.WaitForNextTickAsync(token).AsTask();
        Task<bool>? readTask = null;
        Task<bool>? importantReadTask = null;
        var completedReader = new TaskCompletionSource<bool>().Task;
        var retrySeconds = 1;

        try
        {
            await store.RecoverAsync(token);
            while (true)
            {
                token.ThrowIfCancellationRequested();
                while (buffer.Count < batchSize && (importantChannel.Reader.TryRead(out var item) || channel.Reader.TryRead(out item)))
                {
                    buffer.Add(item);
                }

                var flushDue = tickTask.IsCompleted || Volatile.Read(ref accepting) == 0 || retrySeconds > 1;
                if (tickTask.IsCompleted)
                {
                    await tickTask;
                    tickTask = timer.WaitForNextTickAsync(token).AsTask();
                }

                if (buffer.Count == 0 && channel.Reader.Completion.IsCompleted && importantChannel.Reader.Completion.IsCompleted)
                {
                    break;
                }

                if (buffer.Count < batchSize && (!flushDue || buffer.Count == 0))
                {
                    readTask ??= channel.Reader.WaitToReadAsync(token).AsTask();
                    importantReadTask ??= importantChannel.Reader.WaitToReadAsync(token).AsTask();
                    await Task.WhenAny(readTask, importantReadTask, tickTask);
                    if (readTask.IsCompleted)
                    {
                        readTask = await readTask ? null : completedReader;
                    }

                    if (importantReadTask.IsCompleted)
                    {
                        importantReadTask = await importantReadTask ? null : completedReader;
                    }

                    continue;
                }

                try
                {
                    await FlushAsync(buffer, writer, token);
                    retrySeconds = 1;
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    LogDiagnostics.ReportFailure("write", exception);
                    await Task.Delay(TimeSpan.FromSeconds(retrySeconds), token);
                    retrySeconds = Math.Min(retrySeconds * 2, 30);
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // 停机期限耗尽后退出，保留已写入字节，未完成尾部由下次启动恢复
        }
        finally
        {
            Complete();
            await cancellation.CancelAsync();
            while (importantChannel.Reader.TryRead(out var item) || channel.Reader.TryRead(out item))
            {
                LogDiagnostics.RecordDropped(item.Level);
                Release(item);
            }

            foreach (var entry in buffer)
            {
                LogDiagnostics.RecordDropped(entry.Level);
            }

            ReleaseBatch(buffer, buffer.Count);
        }

    }


    /// <summary>
    /// 复用后台写入缓冲按连续小时段追加日志，每段成功关闭文件后才释放预算
    /// </summary>
    private async Task FlushAsync(List<BufferedLog> buffer, Utf8JsonWriter writer, CancellationToken cancellationToken)
    {

        while (buffer.Count > 0)
        {
            var hour = buffer[0].TimestampUtc.UtcTicks / TimeSpan.TicksPerHour;
            var count = 1;
            while (count < buffer.Count && buffer[count].TimestampUtc.UtcTicks / TimeSpan.TicksPerHour == hour)
            {
                count++;
            }

            try
            {
                await store.AppendAsync(buffer[0].TimestampUtc, GetFileBufferSize(buffer, count), async (stream, token) =>
                {
                    for (var index = 0; index < count; index++)
                    {
                        token.ThrowIfCancellationRequested();
                        utf8Buffer.Clear();
                        writer.Reset(utf8Buffer);
                        // 固定字段仍由 JSON 写入器转义，完整序列化单条日志后再写入文件
                        var log = buffer[index].Log;
                        writer.WriteStartObject();
                        writer.WriteString("category", log.Category);
                        writer.WriteString("level", log.Level);
                        writer.WriteString("content", log.Content);
                        writer.WriteEndObject();
                        writer.Flush();
                        newLine.CopyTo(utf8Buffer.GetSpan(newLine.Length));
                        utf8Buffer.Advance(newLine.Length);
                        await stream.WriteAsync(utf8Buffer.WrittenMemory, token);
                    }
                }, cancellationToken);
            }
            finally
            {
                // 成功、失败或取消后都丢弃超大缓冲，避免偶发大日志长期占用内存
                if (utf8Buffer.Capacity > 65536)
                {
                    utf8Buffer = new ArrayBufferWriter<byte>(4096);
                }
                else
                {
                    utf8Buffer.Clear();
                }

                writer.Reset(utf8Buffer);
            }

            ReleaseBatch(buffer, count);
            buffer.RemoveRange(0, count);
        }

    }


    /// <summary>
    /// 按当前小时段的 JSON 数据量选择文件缓冲，大段快速返回，小段保守计算转义开销
    /// </summary>
    private static int GetFileBufferSize(List<BufferedLog> buffer, int count)
    {

        long lengthEstimateBytes = 0;
        for (var index = 0; index < count; index++)
        {
            var log = buffer[index].Log;
            lengthEstimateBytes += 64L + log.Category.Length + log.Level.Length + log.Content.Length;
            if (lengthEstimateBytes > 16384)
            {
                return 65536;
            }
        }

        var remainingBytes = 16384;
        for (var index = 0; index < count; index++)
        {
            var log = buffer[index].Log;
            // 预留固定字段、分隔符和换行，非普通 ASCII 字符按最多六字节估算
            remainingBytes = SubtractJsonStringBytes(log.Category, remainingBytes - 64);
            remainingBytes = SubtractJsonStringBytes(log.Level, remainingBytes);
            remainingBytes = SubtractJsonStringBytes(log.Content, remainingBytes);
            if (remainingBytes < 0)
            {
                return 65536;
            }
        }

        return 16384;

    }


    /// <summary>
    /// 扣除字符串的保守 UTF-8 和 JSON 转义开销，超过小缓冲预算后立即停止扫描
    /// </summary>
    private static int SubtractJsonStringBytes(string value, int remainingBytes)
    {

        foreach (var character in value)
        {
            if (remainingBytes < 0)
            {
                break;
            }

            remainingBytes -= character is >= ' ' and <= '~' and not '"' and not '\\' ? 1 : 6;
        }

        return remainingBytes;

    }


    /// <summary>
    /// 使用同一个文件存储协调清理与追加，保护正在写入或等待重试的文件
    /// </summary>
    internal Task ClearExpiredFilesAsync(int saveDays, CancellationToken cancellationToken)
    {

        return store.ClearExpiredFilesAsync(saveDays, cancellationToken);

    }


    /// <summary>
    /// 标准日志级别使用固定文本，未定义值保留原枚举转换
    /// </summary>
    private static string GetLevelName(LogLevel level)
    {

        return level switch
        {
            LogLevel.Trace => nameof(LogLevel.Trace),
            LogLevel.Debug => nameof(LogLevel.Debug),
            LogLevel.Information => nameof(LogLevel.Information),
            LogLevel.Warning => nameof(LogLevel.Warning),
            LogLevel.Error => nameof(LogLevel.Error),
            LogLevel.Critical => nameof(LogLevel.Critical),
            LogLevel.None => nameof(LogLevel.None),
            _ => level.ToString()
        };

    }


    /// <summary>
    /// 释放退出时从队列取出的单条日志预算
    /// </summary>
    private void Release(BufferedLog entry)
    {

        if (entry.Important)
        {
            Interlocked.Add(ref importantBufferedBytes, -entry.Bytes);
        }
        else
        {
            Interlocked.Add(ref bufferedBytes, -entry.Bytes);
        }

        LogDiagnostics.ChangeBuffered(-1, -entry.Bytes);

    }


    /// <summary>
    /// 汇总批次占用后统一释放预算和更新指标，减少逐条原子操作
    /// </summary>
    private void ReleaseBatch(List<BufferedLog> buffer, int count)
    {

        if (count == 0)
        {
            return;
        }

        long regularBytes = 0, importantBytes = 0;
        for (var index = 0; index < count; index++)
        {
            var entry = buffer[index];
            if (entry.Important)
            {
                importantBytes += entry.Bytes;
            }
            else
            {
                regularBytes += entry.Bytes;
            }
        }

        if (regularBytes != 0)
        {
            Interlocked.Add(ref bufferedBytes, -regularBytes);
        }

        if (importantBytes != 0)
        {
            Interlocked.Add(ref importantBufferedBytes, -importantBytes);
        }

        LogDiagnostics.ChangeBuffered(-count, -(regularBytes + importantBytes));

    }


    /// <summary>
    /// 关闭日志接收并允许后台消费者排空现有队列
    /// </summary>
    private void Complete()
    {

        Interlocked.Exchange(ref accepting, 0);
        channel.Writer.TryComplete();
        importantChannel.Writer.TryComplete();

    }


    /// <summary>
    /// 在宿主停机期限内继续落盘，期限耗尽时取消后台操作
    /// </summary>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {

        Complete();
        using var registration = cancellationToken.Register(() => abort.Cancel());
        try
        {
            if (ExecuteTask != null)
            {
                await ExecuteTask.WaitAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            LogDiagnostics.ReportFailure("shutdown", new TimeoutException());
        }
        finally
        {
            await base.StopAsync(cancellationToken);
        }

    }


    /// <summary>
    /// 释放服务时停止接收并取消后台操作
    /// </summary>
    public override void Dispose()
    {

        Complete();
        abort.Cancel();
        base.Dispose();

    }


    /// <summary>
    /// 以内联值保存日志快照及缓冲预算，避免为每次入队单独分配包装对象
    /// </summary>
    private readonly record struct BufferedLog(LogMessage Log, DateTimeOffset TimestampUtc, LogLevel Level, long Bytes, bool Important);


    /// <summary>
    /// 保存不可变的消息快照，保留原有 JSON 字段
    /// </summary>
    private readonly record struct LogMessage(string Category, string Level, string Content);

}
