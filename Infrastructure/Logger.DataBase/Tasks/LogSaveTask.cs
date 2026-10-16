using Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Repository;
using Repository.Database;
using System.Buffers;
using System.IO.Pipelines;
using System.Text;
using System.Text.Json;

namespace Logger.DataBase.Tasks;

/// <summary>
/// 连续消费已落盘的日志文件，提交成功后删除并保留损坏文件
/// </summary>
internal class LogSaveTask(IServiceProvider serviceProvider) : BackgroundService
{

    private static readonly TimeSpan TmpRecoverAge = TimeSpan.FromMinutes(5);


    /// <summary>
    /// 有积压时持续入库，空闲或失败后等待再检查
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {

        var basePath = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
        var nextMaintenance = DateTime.MinValue;
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var processed = false;
                var failed = false;
                try
                {
                    if (DateTime.UtcNow >= nextMaintenance)
                    {
                        if (Directory.Exists(basePath))
                        {
                            RecoverTmpFiles(basePath, stoppingToken);
                        }

                        LogDiagnostics.RefreshSpool(basePath);
                        nextMaintenance = DateTime.UtcNow.AddSeconds(30);
                    }

                    if (Directory.Exists(basePath))
                    {
                        // 惰性遍历文件，避免大量积压时一次性将所有路径载入内存
                        foreach (var logPath in Directory.EnumerateFiles(basePath, "batch-*.log"))
                        {
                            stoppingToken.ThrowIfCancellationRequested();
                            await SaveFileAsync(logPath, stoppingToken);
                            processed = true;
                        }
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    failed = true;
                    LogDiagnostics.ReportFailure("database", exception);
                }

                if (!processed || failed)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // 正常停机不删除尚未完整处理的文件
        }

    }


    /// <summary>
    /// 每个文件使用独立上下文和同一 ID 集合去重，仅保存新增日志时临时关闭自动变更检测
    /// </summary>
    private async Task SaveFileAsync(string logPath, CancellationToken cancellationToken)
    {

        var (logs, corrupted) = await ReadFileAsync(logPath, cancellationToken);
        if (logs.Count > 0)
        {
            await using var scope = serviceProvider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
            var ids = logs.Select(log => log.Id).ToList();
            var existingIds = await db.Log.IgnoreQueryFilters().Where(log => ids.Contains(log.Id)).Select(log => log.Id).ToHashSetAsync(cancellationToken);
            var newLogs = logs.Where(log => existingIds.Add(log.Id)).ToList();
            if (newLogs.Count > 0)
            {
                db.Log.AddRange(newLogs);
                var originalAutoDetectChanges = db.ChangeTracker.AutoDetectChangesEnabled;
                try
                {
                    // 日志已显式标记为新增且不再修改属性，保存前无需再次检测变化
                    db.ChangeTracker.AutoDetectChangesEnabled = false;
                    await db.SaveChangesAsync(cancellationToken);
                }
                finally
                {
                    db.ChangeTracker.AutoDetectChangesEnabled = originalAutoDetectChanges;
                }
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (corrupted)
        {
            // 保留整个文件供人工核对，只有有效记录提交成功后才移出待入库集合
            File.Move(logPath, logPath + ".rejected");
            LogDiagnostics.ReportFailure("quarantine", new InvalidDataException());
        }
        else
        {
            File.Delete(logPath);
        }

    }


    /// <summary>
    /// 按 UTF-8 字节读取完整日志并标记损坏行，取消或读取失败时保留原始文件
    /// </summary>
    private static async Task<(List<Log> Logs, bool Corrupted)> ReadFileAsync(string logPath, CancellationToken cancellationToken)
    {

        var logs = new List<Log>();
        var corrupted = false;
        var firstLine = true;
        long scannedBytes = 0;
        byte[]? lineBuffer = null;
        await using var stream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous);
        var reader = PipeReader.Create(stream, new StreamPipeReaderOptions(leaveOpen: true));
        try
        {
            while (true)
            {
                var result = await reader.ReadAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                var consumed = ConsumeLines(result.Buffer, result.IsCompleted, logs, ref corrupted, ref firstLine, ref lineBuffer, ref scannedBytes, cancellationToken);
                reader.AdvanceTo(consumed, result.Buffer.End);
                if (result.IsCompleted)
                {
                    break;
                }
            }
        }
        finally
        {
            await reader.CompleteAsync();
        }

        return (logs, corrupted);

    }


    /// <summary>
    /// 处理完整行并保留未结束的长行，下次仅扫描新读入的字节
    /// </summary>
    private static SequencePosition ConsumeLines(ReadOnlySequence<byte> buffer, bool completed, List<Log> logs, ref bool corrupted, ref bool firstLine, ref byte[]? lineBuffer, ref long scannedBytes, CancellationToken cancellationToken)
    {

        var lineStart = buffer.Start;
        var reader = new SequenceReader<byte>(buffer.Slice(scannedBytes));
        while (reader.TryReadToAny(out ReadOnlySequence<byte> tail, "\r\n"u8))
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReadLogLine(buffer.Slice(lineStart, tail.End), logs, ref corrupted, firstLine, ref lineBuffer);
            firstLine = false;
            lineStart = reader.Position;
        }

        var remaining = buffer.Slice(lineStart);
        if (completed)
        {
            if (!remaining.IsEmpty)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ReadLogLine(remaining, logs, ref corrupted, firstLine, ref lineBuffer);
            }

            scannedBytes = 0;
            return buffer.End;
        }

        scannedBytes = remaining.Length;
        return lineStart;

    }


    /// <summary>
    /// 判断空行并保留原有 Unicode 空白行处理，普通 JSON 行无需解码为字符串
    /// </summary>
    private static bool IsWhiteSpace(ReadOnlySequence<byte> line)
    {

        var reader = new SequenceReader<byte>(line);
        while (reader.TryRead(out var value))
        {
            if (value is 9 or 10 or 11 or 12 or 13 or 32)
            {
                continue;
            }

            return value >= 128 && string.IsNullOrWhiteSpace(Encoding.UTF8.GetString(line.ToArray()));
        }

        return true;

    }


    /// <summary>
    /// 解析单条 UTF-8 日志并复用跨段缓冲，损坏行不影响同一文件中的有效记录
    /// </summary>
    private static void ReadLogLine(ReadOnlySequence<byte> line, List<Log> logs, ref bool corrupted, bool firstLine, ref byte[]? lineBuffer)
    {

        if (firstLine)
        {
            var prefix = new SequenceReader<byte>(line);
            if (prefix.IsNext(Encoding.UTF8.Preamble, advancePast: true))
            {
                line = line.Slice(prefix.Position);
            }
        }

        if (IsWhiteSpace(line))
        {
            return;
        }

        try
        {
            Log? log;
            try
            {
                if (line.IsSingleSegment)
                {
                    log = JsonSerializer.Deserialize<Log>(line.FirstSpan, JsonHelper.DeserializeOpts);
                }
                else
                {
                    var length = checked((int)line.Length);
                    if (lineBuffer == null || lineBuffer.Length < length)
                    {
                        var capacity = Math.Max(length, (int)Math.Min(2L * (lineBuffer?.Length ?? 4096), Array.MaxLength));
                        lineBuffer = new byte[capacity];
                    }

                    line.CopyTo(lineBuffer);
                    log = JsonSerializer.Deserialize<Log>(lineBuffer.AsSpan(0, length), JsonHelper.DeserializeOpts);
                }
            }
            catch (JsonException)
            {
                // 保留原读取器对无效 UTF-8 字节的替换字符处理，仍由公共 JSON 规则校验
                log = JsonHelper.JsonToObject<Log>(Encoding.UTF8.GetString(line.ToArray()));
            }

            if (log == null || log.Id <= 0 || log.Content == null || log.Category == null || log.Level == null || log.Project == null || log.MachineName == null)
            {
                corrupted = true;
                return;
            }

            logs.Add(log);
        }
        catch (Exception exception) when (exception is JsonException or FormatException or OverflowException or ArgumentException)
        {
            corrupted = true;
        }

    }


    /// <summary>
    /// 恢复中断后残留且不再被写入的临时文件，已有目标文件时保留两份待去重数据
    /// </summary>
    private static void RecoverTmpFiles(string basePath, CancellationToken cancellationToken)
    {

        foreach (var tmpPath in Directory.EnumerateFiles(basePath, "batch-*.log.tmp"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (DateTime.UtcNow - File.GetLastWriteTimeUtc(tmpPath) < TmpRecoverAge)
                {
                    continue;
                }

                using (new FileStream(tmpPath, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    // 确认没有落盘任务仍持有该文件
                }

                var finalPath = tmpPath[..^4];
                if (File.Exists(finalPath))
                {
                    finalPath = Path.Combine(basePath, $"batch-recovered-{Guid.NewGuid():N}.log");
                }

                File.Move(tmpPath, finalPath);
            }
            catch (FileNotFoundException)
            {
                // 文件可能已由落盘任务完成重命名
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                LogDiagnostics.ReportFailure("recovery", exception);
            }
        }

    }

}
