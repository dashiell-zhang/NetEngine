using Microsoft.Extensions.Hosting;
using System.Text;
using System.Threading.Channels;

namespace Logger.DataBase;

public sealed class DataBaseLogWriter : BackgroundService
{

    private const int MaxBatchSize = 1000;

    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(3);


    private readonly Channel<string> channel = Channel.CreateBounded<string>(new BoundedChannelOptions(MaxBatchSize)
    {
        SingleReader = true,
        SingleWriter = false,
        FullMode = BoundedChannelFullMode.Wait
    });


    public void Enqueue(string logJsonLine)
    {
        if (!channel.Writer.TryWrite(logJsonLine))
        {
            channel.Writer.WriteAsync(logJsonLine).AsTask().GetAwaiter().GetResult();
        }
    }


    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var basePath = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
        Directory.CreateDirectory(basePath);

        var buffer = new List<string>(MaxBatchSize);
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

    private static async Task FlushAsync(List<string> buffer, string basePath, CancellationToken cancellationToken)
    {
        var fileName = $"batch-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}.log";
        var tempPath = Path.Combine(basePath, fileName + ".tmp");
        var finalPath = Path.Combine(basePath, fileName);

        Directory.CreateDirectory(basePath);

        using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous))
        using (var writer = new StreamWriter(stream, Encoding.UTF8))
        {
            foreach (var line in buffer)
            {
                await writer.WriteLineAsync(line.AsMemory(), cancellationToken);
            }

            await writer.FlushAsync(cancellationToken);
        }

        File.Move(tempPath, finalPath, overwrite: false);
        buffer.Clear();
    }
}
