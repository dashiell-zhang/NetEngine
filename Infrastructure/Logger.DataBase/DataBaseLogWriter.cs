using Common;
using IdentifierGenerator;
using Logger.DataBase.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Repository.Database;
using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace Logger.DataBase;

/// <summary>
/// 非阻塞接收日志并在后台批量落盘，满载时优先使用重要日志预留缓冲
/// </summary>
public sealed class DataBaseLogWriter : BackgroundService
{

    private static readonly byte[] newLine = Encoding.UTF8.GetBytes(Environment.NewLine);
    private readonly IServiceProvider serviceProvider;
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
    public DataBaseLogWriter(IOptions<LoggerSetting> options, IServiceProvider serviceProvider)
    {

        var settings = options.Value;
        this.serviceProvider = serviceProvider;
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
    internal void Enqueue(Log log, LogLevel level)
    {

        var bytes = 512L + 2L * (log.Content.Length + (long)log.Category.Length + log.Project.Length + log.MachineName.Length + log.Level.Length);
        if (Volatile.Read(ref accepting) != 0)
        {
            if (TryEnqueue(channel, log, level, bytes, false, ref bufferedBytes, maxBufferedBytes))
            {
                return;
            }

            if (level >= LogLevel.Warning && TryEnqueue(importantChannel, log, level, bytes, true, ref importantBufferedBytes, importantMaxBufferedBytes))
            {
                return;
            }
        }

        LogDiagnostics.RecordDropped(level);

    }


    /// <summary>
    /// 在估算内存预算内原子预留空间并尝试写入队列
    /// </summary>
    private static bool TryEnqueue(Channel<BufferedLog> target, Log log, LogLevel level, long bytes, bool important, ref long usedBytes, long limit)
    {

        var used = Interlocked.Read(ref usedBytes);
        while (bytes <= limit - used)
        {
            var observed = Interlocked.CompareExchange(ref usedBytes, used + bytes, used);
            if (observed == used)
            {
                LogDiagnostics.ChangeBuffered(1, bytes);
                if (target.Writer.TryWrite(new BufferedLog(log, level, bytes, important)))
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
        var basePath = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
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
                    await FlushAsync(buffer, basePath, writer, token);
                    ReleaseBatch(buffer);
                    buffer.Clear();
                    retrySeconds = 1;
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    LogDiagnostics.ReportFailure("spool", exception);
                    await Task.Delay(TimeSpan.FromSeconds(retrySeconds), token);
                    retrySeconds = Math.Min(retrySeconds * 2, 30);
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // 停机期限耗尽后退出，已经写出的临时文件留待下次恢复
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

            ReleaseBatch(buffer);
        }

    }


    /// <summary>
    /// 后台生成稳定 ID 并跨批次复用 UTF-8 缓冲，完整写出临时文件后交给入库任务
    /// </summary>
    private async Task FlushAsync(List<BufferedLog> buffer, string basePath, Utf8JsonWriter writer, CancellationToken cancellationToken)
    {

        var idService = serviceProvider.GetRequiredService<IdService>();
        Directory.CreateDirectory(basePath);
        var finalPath = Path.Combine(basePath, $"batch-{DateTime.UtcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture)}-{Guid.NewGuid():N}.log");
        var tempPath = finalPath + ".tmp";

        try
        {
            await using var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, GetFileBufferSize(buffer), FileOptions.Asynchronous);
            foreach (var entry in buffer)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (entry.Log.Id == 0)
                {
                    entry.Log.Id = idService.GetId();
                }

                // 每条日志先完整序列化，成功后再写入文件，避免序列化失败留下半条记录
                utf8Buffer.Clear();
                writer.Reset(utf8Buffer);
                JsonSerializer.Serialize(writer, (object)entry.Log, JsonHelper.SerializeOpts);
                writer.Flush();
                newLine.CopyTo(utf8Buffer.GetSpan(newLine.Length));
                utf8Buffer.Advance(newLine.Length);
                await stream.WriteAsync(utf8Buffer.WrittenMemory, cancellationToken);
            }

            await stream.FlushAsync(cancellationToken);
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

        File.Move(tempPath, finalPath);

    }


    /// <summary>
    /// 按批次的保守 JSON 数据量选择文件缓冲，大批次直接使用常规缓冲
    /// </summary>
    private static int GetFileBufferSize(List<BufferedLog> buffer)
    {

        long lengthEstimateBytes = 0;
        foreach (var entry in buffer)
        {
            var log = entry.Log;
            lengthEstimateBytes += 384L + log.Project.Length + log.MachineName.Length + log.Level.Length + log.Category.Length + log.Content.Length;
            if (lengthEstimateBytes > 16384)
            {
                return 65536;
            }
        }

        var remainingBytes = 16384;
        foreach (var entry in buffer)
        {
            var log = entry.Log;
            // 预留固定字段、继承字段、日期、数字及换行，字符串按保守转义开销估算
            remainingBytes = SubtractJsonStringBytes(log.Project, remainingBytes - 384);
            remainingBytes = SubtractJsonStringBytes(log.MachineName, remainingBytes);
            remainingBytes = SubtractJsonStringBytes(log.Level, remainingBytes);
            remainingBytes = SubtractJsonStringBytes(log.Category, remainingBytes);
            remainingBytes = SubtractJsonStringBytes(log.Content, remainingBytes);
            if (remainingBytes < 0)
            {
                return 65536;
            }
        }

        return 16384;

    }


    /// <summary>
    /// 扣除字符串的 UTF-8 和 JSON 转义开销，超过小缓冲预算后停止扫描
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
    private void ReleaseBatch(List<BufferedLog> buffer)
    {

        if (buffer.Count == 0)
        {
            return;
        }

        long regularBytes = 0, importantBytes = 0;
        foreach (var entry in buffer)
        {
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

        LogDiagnostics.ChangeBuffered(-buffer.Count, -(regularBytes + importantBytes));

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
    /// 以内联值保存日志快照引用及缓冲预算，避免为每次入队单独分配包装对象
    /// </summary>
    private readonly record struct BufferedLog(Log Log, LogLevel Level, long Bytes, bool Important);

}
