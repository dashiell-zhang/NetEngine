using IdentifierGenerator;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Repository.Partitioning;

/// <summary>
/// 表示从 EF Core 模型或 Migration Annotation 解析出的分区表定义
/// </summary>
public sealed class PartitionTableDefinition
{

    /// <summary>
    /// 一个小时包含的毫秒数
    /// </summary>
    private const long HourMilliseconds = 60L * 60 * 1000;

    /// <summary>
    /// 创建分区表定义
    /// </summary>
    /// <param name="schema">数据库 Schema</param>
    /// <param name="tableName">父表名称</param>
    /// <param name="keyColumnName">分区键数据库列名称</param>
    /// <param name="intervalHours">单个子分区包含的小时数</param>
    public PartitionTableDefinition(string? schema, string tableName, string keyColumnName, int intervalHours)
    {

        Schema = schema;
        TableName = tableName;
        KeyColumnName = keyColumnName;
        IntervalHours = intervalHours;

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
    /// 单个子分区包含的小时数
    /// </summary>
    public int IntervalHours { get; }


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
        var intervalHours = GetRequiredAnnotation<int>(annotatable, PartitionAnnotationNames.IntervalHours, tableName);

        if (intervalHours <= 0)
        {
            throw new InvalidOperationException($"分区表 {tableName} 的分区间隔小时数必须大于 0");
        }

        return new PartitionTableDefinition(schema, tableName, keyColumn, intervalHours);

    }


    /// <summary>
    /// 按配置的小时数计算固定间隔毫秒数
    /// </summary>
    /// <returns>固定间隔毫秒数</returns>
    public long GetIntervalMilliseconds()
    {

        return checked(IntervalHours * HourMilliseconds);

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

        var intervalMilliseconds = GetIntervalMilliseconds();
        var partitionIndex = (currentMilliseconds - SnowflakeIdLayout.EpochMilliseconds) / intervalMilliseconds;
        var startMilliseconds = checked(SnowflakeIdLayout.EpochMilliseconds + partitionIndex * intervalMilliseconds);
        return CreateRangeFromMilliseconds(startMilliseconds);

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

        return CreateRangeFromMilliseconds(startTime.ToUnixTimeMilliseconds());

    }


    /// <summary>
    /// 从起始 Unix 毫秒创建固定时长分区范围
    /// </summary>
    /// <param name="startMilliseconds">范围起始 Unix 毫秒</param>
    /// <returns>固定时长分区范围</returns>
    private PartitionRange CreateRangeFromMilliseconds(long startMilliseconds)
    {

        var endMilliseconds = checked(startMilliseconds + GetIntervalMilliseconds());
        var startTime = DateTimeOffset.FromUnixTimeMilliseconds(startMilliseconds);
        var endTime = DateTimeOffset.FromUnixTimeMilliseconds(endMilliseconds);
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
