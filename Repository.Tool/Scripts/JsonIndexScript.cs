using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Repository.Core.Attributes;
using Repository.Core.Partitioning;
using System.Data;
using System.Data.Common;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Repository.Tool.Scripts;

/// <summary>
/// 读取选定模型拥有的表并输出 JSON 索引差异，不执行 DDL
/// </summary>
public static class JsonIndexScript
{

    private static readonly Regex ManagedName = new("^jsonbIX_[0-9a-fA-F]{32,48}$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));


    /// <summary>
    /// 仅比较当前模型实际映射表中的工具管理索引
    /// </summary>
    public static async Task<string> CreateAsync(DbContext db, CancellationToken cancellationToken = default)
    {

        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        try
        {
            if (shouldClose)
            {
                await connection.OpenAsync(cancellationToken);
            }

            var result = new StringBuilder();
            var groups = db.Model.GetEntityTypes().Where(entity => entity.GetTableName() is not null)
                .GroupBy(entity => (Schema: entity.GetSchema(), Table: entity.GetTableName()!));
            foreach (var group in groups)
            {
                var schema = await ResolveSchemaAsync(connection, group.Key.Schema, group.Key.Table, cancellationToken);
                var expected = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var entity in group)
                {
                    foreach (var declaration in entity.ClrType.GetCustomAttributes<JsonIndexAttribute>())
                    {
                        var expressions = declaration.PropertyNames.Select((path, index) => BuildExpression(entity, group.Key.Schema, group.Key.Table, path) +
                            (declaration.AllDescending || declaration.IsDescending?.ElementAtOrDefault(index) == true ? " DESC" : "")).ToArray();
                        if (expressions.Length == 0)
                        {
                            throw new InvalidOperationException($"实体 {entity.Name} 的 JSON 索引没有字段");
                        }
                        if (declaration.IsUnique && PartitionTableDefinition.TryCreate(entity, out var partition) && !declaration.PropertyNames.Any(path =>
                            entity.FindProperty(path)?.GetColumnName(StoreObjectIdentifier.Table(group.Key.Table, group.Key.Schema)) == partition!.KeyColumnName))
                        {
                            throw new InvalidOperationException($"分区表 {group.Key.Table} 的唯一 JSON 索引必须包含分区键");
                        }

                        var unique = declaration.IsUnique ? " UNIQUE" : "";
                        var definition = $"{unique} ON {Quote(schema)}.{Quote(group.Key.Table)} USING btree ({string.Join(", ", expressions)})";
                        var name = "jsonbIX_" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(definition)))[..40];
                        expected[name] = $"CREATE{unique} INDEX {Quote(name)} ON {Quote(schema)}.{Quote(group.Key.Table)} USING btree ({string.Join(", ", expressions)});";
                    }
                }

                var existing = new HashSet<string>(StringComparer.Ordinal);
                await using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT indexname FROM pg_catalog.pg_indexes WHERE schemaname = @schema AND tablename = @table";
                    AddParameter(command, "schema", schema);
                    AddParameter(command, "table", group.Key.Table);
                    await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                    while (await reader.ReadAsync(cancellationToken))
                    {
                        var name = reader.GetString(0);
                        if (ManagedName.IsMatch(name))
                        {
                            existing.Add(name);
                        }
                    }
                }

                foreach (var name in existing.Except(expected.Keys).Order(StringComparer.Ordinal))
                {
                    result.AppendLine($"DROP INDEX {Quote(schema)}.{Quote(name)};");
                }
                foreach (var name in expected.Keys.Except(existing).Order(StringComparer.Ordinal))
                {
                    result.AppendLine(expected[name]);
                }
            }
            return result.ToString();
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }

    }


    /// <summary>
    /// 从 EF 属性映射解析真实列名和 JSON 属性名，拒绝未映射或集合路径
    /// </summary>
    private static string BuildExpression(IEntityType entity, string? schema, string table, string path)
    {

        var parts = path.Split('.');
        if (parts.Length == 1)
        {
            var scalar = entity.FindProperty(path) ?? throw new InvalidOperationException($"实体 {entity.Name} 未映射属性 {path}");
            var column = scalar.GetColumnName(StoreObjectIdentifier.Table(table, schema)) ?? throw new InvalidOperationException($"属性 {path} 未映射到表 {table}");
            return Quote(column);
        }

        var root = entity.FindComplexProperty(parts[0]) ?? throw new InvalidOperationException($"实体 {entity.Name} 不存在 JSON 复杂属性 {parts[0]}");
        if (root.IsCollection)
        {
            throw new InvalidOperationException("JSON 索引不支持集合路径");
        }
        IComplexType current = root.ComplexType;
        var expression = Quote(current.GetContainerColumnName() ?? throw new InvalidOperationException($"属性 {parts[0]} 没有 JSON 列映射"));
        for (var i = 1; i < parts.Length - 1; i++)
        {
            var nested = current.FindComplexProperty(parts[i]) ?? throw new InvalidOperationException($"JSON 路径 {path} 不存在");
            if (nested.IsCollection)
            {
                throw new InvalidOperationException("JSON 索引不支持集合路径");
            }
            expression = $"({expression} -> {Literal(nested.GetJsonPropertyName() ?? nested.Name)}::text)";
            current = nested.ComplexType;
        }
        var property = current.FindProperty(parts[^1]) ?? throw new InvalidOperationException($"JSON 路径 {path} 不存在");
        expression = $"({expression} ->> {Literal(property.GetJsonPropertyName() ?? property.Name)}::text)";
        var type = property.GetRelationalTypeMapping().Converter?.ProviderClrType ?? Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
        var cast = type == typeof(string) ? null
            : type == typeof(short) ? "smallint"
            : type == typeof(int) || type == typeof(ushort) ? "integer"
            : type == typeof(long) || type == typeof(uint) ? "bigint"
            : type == typeof(ulong) ? "numeric(20,0)"
            : type == typeof(decimal) ? "numeric"
            : type == typeof(float) ? "real"
            : type == typeof(double) ? "double precision"
            : type == typeof(bool) ? "boolean"
            : type == typeof(Guid) ? "uuid"
            : throw new InvalidOperationException($"JSON 索引不支持字段 {path} 的类型 {type.Name}");
        return cast is null ? $"({expression})" : $"(({expression})::{cast})";

    }


    /// <summary>
    /// 按选定连接的实际搜索路径解析表所属 Schema
    /// </summary>
    private static async Task<string> ResolveSchemaAsync(DbConnection connection, string? schema, string table, CancellationToken cancellationToken)
    {

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT n.nspname FROM pg_catalog.pg_class c JOIN pg_catalog.pg_namespace n ON n.oid = c.relnamespace WHERE c.oid = pg_catalog.to_regclass(@name)";
        AddParameter(command, "name", schema is null ? Quote(table) : $"{Quote(schema)}.{Quote(table)}");
        return await command.ExecuteScalarAsync(cancellationToken) as string ?? throw new InvalidOperationException($"所选模型的表 {schema ?? "<search_path>"}.{table} 不存在");

    }


    /// <summary>
    /// 添加工具只读目录查询的字符串参数
    /// </summary>
    private static void AddParameter(DbCommand command, string name, string value)
    {

        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);

    }


    /// <summary>
    /// 引用数据库标识符
    /// </summary>
    private static string Quote(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";


    /// <summary>
    /// 引用 JSON 属性名称字面量
    /// </summary>
    private static string Literal(string value) => "'" + value.Replace("'", "''") + "'";

}
