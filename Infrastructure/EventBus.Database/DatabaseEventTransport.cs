using IdentifierGenerator;
using global::EventBus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using EventBus.Database.Persistence;
using System.Globalization;

namespace EventBus.Database;

/// <summary>
/// 通过 EF Core 保存广播事件并协调每个范围的提交顺序
/// </summary>
public sealed class DatabaseEventTransport(IDbContextFactory<EventBusDatabaseContext> factory, IOptions<EventBusOptions> options, TimeProvider timeProvider, IdService idService) : IEventTransport
{

    /// <summary>
    /// 当前实例的广播范围
    /// </summary>
    private readonly string scope = options.Value.Scope;


    /// <summary>
    /// 取得已经提交的位置，首次使用时建立范围状态
    /// </summary>
    public async Task<string> GetPositionAsync(CancellationToken cancellationToken = default)
    {

        return (await EnsureStreamAsync(cancellationToken)).ToString(CultureInfo.InvariantCulture);

    }


    /// <summary>
    /// 通过上下文的执行策略重试完整的独立发布事务
    /// </summary>
    public async Task AppendAsync(EventEnvelope message, CancellationToken cancellationToken = default)
    {

        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(token => AppendCoreAsync(message, token), cancellationToken);

    }


    /// <summary>
    /// 每次重试使用新上下文更新范围状态并写入事件，保持提交顺序
    /// </summary>
    private async Task AppendCoreAsync(EventEnvelope message, CancellationToken cancellationToken)
    {

        await EnsureStreamAsync(cancellationToken);
        for (int attempt = 0; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var db = await factory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var stream = await db.Set<EventBusStream>().IgnoreQueryFilters().AsTracking().SingleAsync(t => t.Scope == scope, cancellationToken);
                stream.Position = checked(stream.Position + 1);

                // 先持有状态行的写锁，事务提交前不允许后续位置越过当前事件
                await db.SaveChangesAsync(cancellationToken);

                db.Set<EventBusMessage>().Add(new EventBusMessage
                {
                    Id = idService.GetId(),
                    Scope = scope,
                    Position = stream.Position,
                    EventId = message.EventId,
                    EventName = message.EventName,
                    Payload = message.Payload,
                    CreateTime = timeProvider.GetUtcNow()
                });

                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return;
            }
            catch (DbUpdateConcurrencyException) when (attempt < 63)
            {
                await transaction.RollbackAsync(cancellationToken);
                await Task.Delay(TimeSpan.FromMilliseconds(Random.Shared.Next(5, 25)), cancellationToken);
            }
        }

    }


    /// <summary>
    /// 按已提交位置读取后续事件，所有实例分别维护自己的进度
    /// </summary>
    public async Task<IReadOnlyList<EventRecord>> ReadAsync(string position, int count, CancellationToken cancellationToken = default)
    {

        long after = long.Parse(position, CultureInfo.InvariantCulture);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var messages = await db.Set<EventBusMessage>().AsNoTracking()
            .Where(t => t.Scope == scope && t.Position > after)
            .OrderBy(t => t.Position)
            .Take(count)
            .ToListAsync(cancellationToken);

        return messages.Select(t => new EventRecord(t.Position.ToString(CultureInfo.InvariantCulture), t.CreateTime, new EventEnvelope(t.EventId, t.EventName, t.Payload))).ToArray();

    }


    /// <summary>
    /// 批量物理删除过期记录，保留范围状态与最新位置
    /// </summary>
    public async Task TrimAsync(DateTimeOffset before, CancellationToken cancellationToken = default)
    {

        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await db.Set<EventBusMessage>().IgnoreQueryFilters().Where(t => t.Scope == scope && t.CreateTime < before).ExecuteDeleteAsync(cancellationToken);

    }


    /// <summary>
    /// 初始化范围状态并处理多个实例首次插入时的竞争
    /// </summary>
    private async Task<long> EnsureStreamAsync(CancellationToken cancellationToken)
    {

        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var stream = await db.Set<EventBusStream>().IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(t => t.Scope == scope, cancellationToken);
        if (stream != null)
        {
            return stream.Position;
        }

        db.Set<EventBusStream>().Add(new EventBusStream { Id = idService.GetId(), Scope = scope, CreateTime = timeProvider.GetUtcNow() });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return 0;
        }
        catch (DbUpdateException)
        {
            // 只在确认其他实例已经建立状态时接受首次插入冲突
            await using var verify = await factory.CreateDbContextAsync(cancellationToken);
            var existing = await verify.Set<EventBusStream>().IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(t => t.Scope == scope, cancellationToken);
            if (existing == null)
            {
                throw;
            }

            return existing.Position;
        }

    }

}
