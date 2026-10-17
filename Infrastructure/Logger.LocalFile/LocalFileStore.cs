using System.Globalization;

namespace Logger.LocalFile;

/// <summary>
/// 协调小时文件的追加、失败回退、尾部恢复与过期清理
/// </summary>
internal sealed class LocalFileStore(string basePath)
{

    private readonly SemaphoreSlim gate = new(1, 1);
    private string? pendingPath;
    private long pendingOffset;
    private string? verifiedPath;
    private long verifiedLength;


    /// <summary>
    /// 使用按小时段数据量选择的文件缓冲追加，失败后保留原始长度供重试回退
    /// </summary>
    internal async Task AppendAsync(DateTimeOffset timestampUtc, int bufferSize, Func<Stream, CancellationToken, Task> writeAsync, CancellationToken cancellationToken)
    {

        var path = Path.Combine(basePath, timestampUtc.UtcDateTime.ToString("yyyyMMddHH", CultureInfo.InvariantCulture) + ".log");
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (pendingPath != null && pendingPath != path)
            {
                throw new InvalidOperationException("必须先完成等待重试的小时文件");
            }

            Directory.CreateDirectory(basePath);
            long writtenLength;
            await using (var stream = new FileStream(path, pendingPath == null ? FileMode.OpenOrCreate : FileMode.Open, FileAccess.Write, FileShare.Read, bufferSize, FileOptions.Asynchronous))
            {
                if (pendingPath == null)
                {
                    // 当前写入器已确认的文件长度不变时，无需每批重新读取尾部
                    if (verifiedPath != path || verifiedLength != stream.Length)
                    {
                        await using var reader = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 65536, FileOptions.Asynchronous);
                        await RepairTailAsync(reader, stream, path, cancellationToken);
                    }

                    pendingOffset = stream.Length;
                    pendingPath = path;
                }
                else if (stream.Length < pendingOffset)
                {
                    // 不把外部删除或截短造成的数据变化当成一次正常重试
                    throw new InvalidDataException("等待重试的文件已被外部截短");
                }

                if (stream.Length != pendingOffset)
                {
                    stream.SetLength(pendingOffset);
                }

                stream.Position = pendingOffset;
                await writeAsync(stream, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                writtenLength = stream.Length;
            }

            // 关闭成功也属于确认条件，关闭失败时下次仍从原位置重写
            pendingPath = null;
            verifiedPath = path;
            verifiedLength = writtenLength;
        }
        finally
        {
            gate.Release();
        }

    }


    /// <summary>
    /// 启动时检查本组件所有小时文件，保留上次中断留下的未完成尾部
    /// </summary>
    internal async Task RecoverAsync(CancellationToken cancellationToken)
    {

        await gate.WaitAsync(cancellationToken);
        try
        {
            if (!Directory.Exists(basePath))
            {
                return;
            }

            foreach (var path in Directory.EnumerateFiles(basePath, "*.log"))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!IsHourlyFile(path) || path == pendingPath)
                {
                    continue;
                }

                try
                {
                    await using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read, 65536, FileOptions.Asynchronous);
                    await RepairTailAsync(stream, stream, path, cancellationToken);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    LogDiagnostics.ReportFailure("recovery", exception);
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            LogDiagnostics.ReportFailure("recovery", exception);
        }
        finally
        {
            gate.Release();
        }

    }


    /// <summary>
    /// 保存末行未结束的原始字节后截回完整行边界，保存失败时不截短原文件
    /// </summary>
    private static async Task RepairTailAsync(FileStream source, FileStream target, string path, CancellationToken cancellationToken)
    {

        var length = source.Length;
        if (length == 0)
        {
            return;
        }

        var buffer = new byte[4096];
        var end = length;
        long validLength = 0;
        while (end > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var count = (int)Math.Min(buffer.Length, end);
            source.Position = end - count;
            await source.ReadExactlyAsync(buffer.AsMemory(0, count), cancellationToken);
            var newline = Array.LastIndexOf(buffer, (byte)'\n', count - 1, count);
            if (newline >= 0)
            {
                validLength = end - count + newline + 1;
                break;
            }

            end -= count;
        }

        if (validLength == length)
        {
            return;
        }

        var rejectedPath = path + ".tail-" + Guid.NewGuid().ToString("N") + ".rejected";
        source.Position = validLength;
        await using (var rejected = new FileStream(rejectedPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 65536, FileOptions.Asynchronous))
        {
            await source.CopyToAsync(rejected, cancellationToken);
            await rejected.FlushAsync(cancellationToken);
        }

        target.SetLength(validLength);
        await target.FlushAsync(cancellationToken);
        LogDiagnostics.ReportFailure("recovery", new InvalidDataException("已保留未完成的日志尾部"));

    }


    /// <summary>
    /// 仅清理本组件的过期小时文件，保留最近写入、正在追加或等待重试的文件
    /// </summary>
    internal async Task ClearExpiredFilesAsync(int saveDays, CancellationToken cancellationToken)
    {

        if (saveDays == -1)
        {
            return;
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            if (!Directory.Exists(basePath))
            {
                return;
            }

            var now = DateTime.UtcNow;
            var deleteTime = now.AddDays(-Math.Min(saveDays, (now - DateTime.MinValue).Days));
            foreach (var file in new DirectoryInfo(basePath).EnumerateFiles("*.log"))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!IsHourlyFile(file.Name) || file.FullName == pendingPath)
                {
                    continue;
                }

                try
                {
                    if (file.LastWriteTimeUtc < deleteTime)
                    {
                        file.Delete();
                    }
                }
                catch (FileNotFoundException)
                {
                    // 文件可能已被外部维护操作移走
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    LogDiagnostics.ReportFailure("cleanup", exception);
                }
            }
        }
        finally
        {
            gate.Release();
        }

    }


    /// <summary>
    /// 精确识别 yyyyMMddHH.log 文件，排除数据库批次、临时文件及尾部保留文件
    /// </summary>
    private static bool IsHourlyFile(string path)
    {

        var name = Path.GetFileName(path);
        return name.Length == 14 && name.EndsWith(".log", StringComparison.Ordinal)
            && DateTime.TryParseExact(name.AsSpan(0, 10), "yyyyMMddHH", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    }

}
