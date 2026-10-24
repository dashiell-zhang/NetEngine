using System.Collections.Frozen;
using Npgsql;

namespace Repository.Partitioning;

/// <summary>
/// 保存宿主启动时发现的分区父表，仅用于 xmin 适配，不声明分区维护策略
/// </summary>
public sealed class PartitionTableSnapshot
{

    private readonly FrozenSet<(string? Schema, string Name)> tables;


    /// <summary>
    /// 将目录查询结果冻结为可供上下文池和工厂共同读取的快照
    /// </summary>
    private PartitionTableSnapshot(HashSet<(string? Schema, string Name)> tables)
    {

        this.tables = tables.ToFrozenSet();

    }


    /// <summary>
    /// 在宿主启动时使用写库配置读取一次目录，查询失败直接阻止启动
    /// </summary>
    public static PartitionTableSnapshot Load(string connectionString)
    {

        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        // 目录快照独立于业务事务，避免启动初始化意外登记到调用方的环境事务
        var settings = new NpgsqlConnectionStringBuilder(connectionString) { Enlist = false };
        using var connection = new NpgsqlConnection(settings.ConnectionString);
        connection.Open();
        using var command = new NpgsqlCommand("""
            SELECT n.nspname, c.relname, pg_catalog.pg_table_is_visible(c.oid)
            FROM pg_catalog.pg_class AS c
            JOIN pg_catalog.pg_namespace AS n ON n.oid = c.relnamespace
            WHERE c.relkind = 'p'
            """, connection);
        using var reader = command.ExecuteReader();
        var tables = new HashSet<(string? Schema, string Name)>();
        while (reader.Read())
        {
            var name = reader.GetString(1);
            tables.Add((reader.GetString(0), name));
            // 未显式映射 schema 的表按启动连接的 search_path 解析，不能误匹配同名隐藏表
            if (reader.GetBoolean(2)) tables.Add((null, name));
        }
        return new PartitionTableSnapshot(tables);

    }


    /// <summary>
    /// 根据 EF 实际使用的表名和 schema 判断是否需要分区 xmin 适配
    /// </summary>
    internal bool Contains(string name, string? schema) => tables.Contains((schema, name));

}
