using Microsoft.Extensions.Hosting;
using System.Text;
using System.Threading.Channels;

namespace Logger.LocalFile;

public sealed class LocalFileLogWriter : BackgroundService
{

    private const int MaxBatchSize = 1000;

    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(3);


    private readonly Channel<LogEntry> channel = Channel.CreateBounded<LogEntry>(new BoundedChannelOptions(MaxBatchSize)
    {
        SingleReader = true,
        SingleWriter = false,
        FullMode = BoundedChannelFullMode.Wait
    });


    public void Enqueue(string logLine)
    {
        var entry = new LogEntry(DateTimeOffset.UtcNow, logLine);

        if (!channel.Writer.TryWrite(entry))
        {
            channel.Writer.WriteAsync(entry).AsTask().GetAwaiter().GetResult();
        }
    }


    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var basePath = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
        Directory.CreateDirectory(basePath);

        var buffer = new List<LogEntry>(MaxBatchSize);
        using var timer = new PeriodicTimer(FlushInterval);
        var tickTask = timer.WaitForNextTickAsync(stoppingToken).AsTask();

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                while (buffer.Count < MaxBatchSize && channel.Reader.TryRead(out var item))
                {
                    buffer.Add(item);
                }

                if (buffer.Count >= MaxBatchSize)
                {
                    await FlushAsync(buffer, basePath, stoppingToken);
                    continue;
                }

                var waitToReadTask = channel.Reader.WaitToReadAsync(stoppingToken).AsTask();

                var completed = await Task.WhenAny(waitToReadTask, tickTask);

                if (completed == tickTask)
                {
                    if (buffer.Count > 0)
                    {
                        await FlushAsync(buffer, basePath, stoppingToken);
                    }

                    tickTask = timer.WaitForNextTickAsync(stoppingToken).AsTask();
                    continue;
                }

                await waitToReadTask;
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            while (channel.Reader.TryRead(out var item))
            {
                buffer.Add(item);
                if (buffer.Count >= MaxBatchSize)
                {
                    await FlushAsync(buffer, basePath, CancellationToken.None);
                }
            }

            if (buffer.Count > 0)
            {
                await FlushAsync(buffer, basePath, CancellationToken.None);
            }
        }
    }


    private static async Task FlushAsync(List<LogEntry> buffer, string basePath, CancellationToken cancellationToken)
    {
        var grouped = buffer
            .GroupBy(x => Path.Combine(basePath, x.TimestampUtc.ToString("yyyyMMddHH") + ".log"))
            .ToList();

        foreach (var group in grouped)
        {
            var logPath = group.Key;
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

            using var stream = new FileStream(logPath, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, FileOptions.Asynchronous);
            using var writer = new StreamWriter(stream, Encoding.UTF8);

            foreach (var entry in group)
            {
                await writer.WriteLineAsync(entry.Line.AsMemory(), cancellationToken);
            }

            await writer.FlushAsync(cancellationToken);
        }

        buffer.Clear();
    }


    private readonly record struct LogEntry(DateTimeOffset TimestampUtc, string Line);

}
