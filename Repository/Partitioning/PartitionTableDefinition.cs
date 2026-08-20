using IdentifierGenerator;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Repository.Attributes;

namespace Repository.Partitioning;

/// <summary>
/// 表示从 EF Core 模型或 Migration Annotation 解析出的分区表定义
/// </summary>
public sealed class PartitionTableDefinition
{

    /// <summary>
    /// 创建分区表定义
    /// </summary>
    /// <param name="schema">数据库 Schema</param>
    /// <param name="tableName">父表名称</param>
    /// <param name="keyColumnName">分区键数据库列名称</param>
    /// <param name="interval">单个子分区包含的周期数量</param>
    /// <param name="unit">分区周期单位</param>
    public PartitionTableDefinition(string? schema, string tableName, string keyColumnName, int interval, PartitionUnit unit)
    {

        Schema = schema;
        TableName = tableName;
        KeyColumnName = keyColumnName;
        Interval = interval;
        Unit = unit;

    }


    /// <summary>
    /// 数据库 Schema
    /// </summary>
    public string? Schema { get; }


    /// <summary>
    /// 父表名称
    /// </summary>
    public string TableName { get; }


    /// <summary>
    /// 分区键数据库列名称
    /// </summary>
    public string KeyColumnName { get; }


    /// <summary>
    /// 单个子分区包含的周期数量
    /// </summary>
    public int Interval { get; }


    /// <summary>
    /// 分区周期单位
    /// </summary>
    public PartitionUnit Unit { get; }


    /// <summary>
    /// 从实体模型尝试读取分区表定义
    /// </summary>
    /// <param name="entityType">EF Core 实体类型</param>
    /// <param name="definition">读取成功的分区表定义</param>
    /// <returns>实体包含分区 Annotation 时返回 true</returns>
    public static bool TryCreate(IEntityType entityType, out PartitionTableDefinition? definition)
    {

        if (entityType.FindAnnotation(PartitionAnnotationNames.Strategy) is null)
        {
            definition = null;
            return false;
        }

        var tableName = entityType.GetTableName() ?? throw new InvalidOperationException($"分区实体 {entityType.Name} 没有映射到数据库表");
        definition = Create(entityType, entityType.GetSchema(), tableName);
        return true;

    }


    /// <summary>
    /// 从 Migration Annotation 创建分区表定义
    /// </summary>
    /// <param name="annotatable">包含分区 Annotation 的对象</param>
    /// <param name="schema">数据库 Schema</param>
    /// <param name="tableName">父表名称</param>
    /// <returns>分区表定义</returns>
    public static PartitionTableDefinition Create(IReadOnlyAnnotatable annotatable, string? schema, string tableName)
    {

        var strategy = GetRequiredAnnotation<string>(annotatable, PartitionAnnotationNames.Strategy, tableName);
        if (!string.Equals(strategy, PartitionAnnotationNames.RangeStrategy, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"分区表 {tableName} 的策略 {strategy} 不受支持");
        }

        var keyType = GetRequiredAnnotation<string>(annotatable, PartitionAnnotationNames.KeyType, tableName);
        if (!string.Equals(keyType, PartitionAnnotationNames.SnowflakeIdKeyType, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"分区表 {tableName} 的分区键类型 {keyType} 不受支持");
        }

        var keyColumn = GetRequiredAnnotation<string>(annotatable, PartitionAnnotationNames.KeyColumn, tableName);
        var interval = GetRequiredAnnotation<int>(annotatable, PartitionAnnotationNames.Interval, tableName);
        var unitName = GetRequiredAnnotation<string>(annotatable, PartitionAnnotationNames.Unit, tableName);

        if (interval <= 0)
        {
            throw new InvalidOperationException($"分区表 {tableName} 的分区周期数量必须大于 0");
        }

        if (!Enum.TryParse<PartitionUnit>(unitName, false, out var unit)
            || !Enum.IsDefined(unit)
            || !string.Equals(unit.ToString(), unitName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"分区表 {tableName} 的分区周期单位 {unitName} 不受支持");
        }

        return new PartitionTableDefinition(schema, tableName, keyColumn, interval, unit);

    }


    /// <summary>
    /// 根据当前 UTC 时间计算尚无子分区时的初始范围
    /// </summary>
    /// <param name="utcNow">当前 UTC 时间</param>
    /// <returns>当前时刻所属的初始分区范围</returns>
    public PartitionRange CreateInitialRange(DateTimeOffset utcNow)
    {

        var currentMilliseconds = utcNow.ToUniversalTime().ToUnixTimeMilliseconds();
        if (currentMilliseconds < SnowflakeIdLayout.EpochMilliseconds)
        {
            throw new InvalidOperationException($"当前时间早于雪花纪元，无法为分区表 {TableName} 计算范围");
        }

        var startTime = AlignRangeStart(PartitionTimeLayout.ToPartitionTime(utcNow));
        return CreateRange(startTime, AddInterval(startTime));

    }


    /// <summary>
    /// 从已有分区的结束雪花 ID 开始计算下一个范围
    /// </summary>
    /// <param name="startId">新范围包含的起始雪花 ID</param>
    /// <returns>按当前策略计算的新分区范围</returns>
    public PartitionRange CreateNextRange(long startId)
    {

        var startTime = SnowflakeIdLayout.GetTimeById(startId);
        if (SnowflakeIdLayout.GetMinIdByTime(startTime) != startId)
        {
            throw new InvalidOperationException($"分区表 {TableName} 的已有上界 {startId} 不是完整毫秒对应的最小雪花 ID");
        }

        var partitionStartTime = PartitionTimeLayout.ToPartitionTime(startTime);
        var alignedStartTime = AlignRangeStart(partitionStartTime);
        var endTime = AddInterval(alignedStartTime);
        return CreateRange(partitionStartTime, endTime);

    }


    /// <summary>
    /// 将指定时间对齐到当前分区策略的范围起点
    /// </summary>
    /// <param name="partitionTime">固定 UTC+8 下的时间</param>
    /// <returns>包含指定时间的范围起点</returns>
    private DateTimeOffset AlignRangeStart(DateTimeOffset partitionTime)
    {

        var anchor = PartitionTimeLayout.Anchor;

        return Unit switch
        {
            PartitionUnit.Hour => AlignFixedTicks(partitionTime, anchor, checked(TimeSpan.TicksPerHour * Interval)),
            PartitionUnit.Day => AlignFixedTicks(partitionTime, anchor, checked(TimeSpan.TicksPerDay * Interval)),
            PartitionUnit.Month => anchor.AddMonths(CalculateAlignedMonthOffset(partitionTime, anchor)),
            PartitionUnit.Year => anchor.AddYears(CalculateAlignedYearOffset(partitionTime, anchor)),
            _ => throw new InvalidOperationException($"分区表 {TableName} 的分区周期单位 {Unit} 不受支持")
        };

    }


    /// <summary>
    /// 按当前单位增加一个完整分区周期
    /// </summary>
    /// <param name="startTime">固定 UTC+8 下的范围起点</param>
    /// <returns>增加一个周期后的时间</returns>
    private DateTimeOffset AddInterval(DateTimeOffset startTime)
    {

        return Unit switch
        {
            PartitionUnit.Hour => startTime.AddHours(Interval),
            PartitionUnit.Day => startTime.AddDays(Interval),
            PartitionUnit.Month => startTime.AddMonths(Interval),
            PartitionUnit.Year => startTime.AddYears(Interval),
            _ => throw new InvalidOperationException($"分区表 {TableName} 的分区周期单位 {Unit} 不受支持")
        };

    }


    /// <summary>
    /// 按固定 Tick 周期对齐时间
    /// </summary>
    /// <param name="time">需要对齐的时间</param>
    /// <param name="anchor">对齐锚点</param>
    /// <param name="intervalTicks">单个周期包含的 Tick 数量</param>
    /// <returns>包含指定时间的周期起点</returns>
    private static DateTimeOffset AlignFixedTicks(DateTimeOffset time, DateTimeOffset anchor, long intervalTicks)
    {

        var elapsedTicks = checked(time.Ticks - anchor.Ticks);
        var alignedTicks = checked(anchor.Ticks + elapsedTicks / intervalTicks * intervalTicks);
        return new DateTimeOffset(alignedTicks, PartitionTimeLayout.Offset);

    }


    /// <summary>
    /// 计算从锚点开始按自然月对齐后的月份偏移量
    /// </summary>
    /// <param name="time">需要对齐的时间</param>
    /// <param name="anchor">对齐锚点</param>
    /// <returns>对齐后的月份偏移量</returns>
    private int CalculateAlignedMonthOffset(DateTimeOffset time, DateTimeOffset anchor)
    {

        var monthOffset = checked((time.Year - anchor.Year) * 12 + time.Month - anchor.Month);
        return monthOffset / Interval * Interval;

    }


    /// <summary>
    /// 计算从锚点开始按自然年对齐后的年份偏移量
    /// </summary>
    /// <param name="time">需要对齐的时间</param>
    /// <param name="anchor">对齐锚点</param>
    /// <returns>对齐后的年份偏移量</returns>
    private int CalculateAlignedYearOffset(DateTimeOffset time, DateTimeOffset anchor)
    {

        var yearOffset = time.Year - anchor.Year;
        return yearOffset / Interval * Interval;

    }


    /// <summary>
    /// 根据固定 UTC+8 起止时间创建雪花 ID 分区范围
    /// </summary>
    /// <param name="startTime">范围起始时间</param>
    /// <param name="endTime">范围结束时间</param>
    /// <returns>雪花 ID 分区范围</returns>
    private PartitionRange CreateRange(DateTimeOffset startTime, DateTimeOffset endTime)
    {

        if (endTime <= startTime)
        {
            throw new InvalidOperationException($"分区表 {TableName} 计算出的时间范围无效");
        }

        return new PartitionRange(SnowflakeIdLayout.GetMinIdByTime(startTime), SnowflakeIdLayout.GetMinIdByTime(endTime), startTime, endTime);

    }


    /// <summary>
    /// 获取指定名称且类型正确的必需 Annotation
    /// </summary>
    /// <typeparam name="TValue">Annotation 值类型</typeparam>
    /// <param name="annotatable">Annotation 所属对象</param>
    /// <param name="name">Annotation 名称</param>
    /// <param name="tableName">用于错误信息的表名</param>
    /// <returns>Annotation 值</returns>
    private static TValue GetRequiredAnnotation<TValue>(IReadOnlyAnnotatable annotatable, string name, string tableName)
    {

        var value = annotatable.FindAnnotation(name)?.Value;
        if (value is TValue typedValue)
        {
            return typedValue;
        }

        throw new InvalidOperationException($"分区表 {tableName} 缺少有效的 {name} Annotation");

    }

}
