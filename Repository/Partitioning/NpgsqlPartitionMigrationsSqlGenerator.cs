#pragma warning disable EF1001

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure.Internal;
using Npgsql.EntityFrameworkCore.PostgreSQL.Migrations;

namespace Repository.Partitioning;

/// <summary>
/// 扩展 Npgsql Migration SQL 以创建 PostgreSQL RANGE 分区父表和初始子分区
/// </summary>
public sealed class NpgsqlPartitionMigrationsSqlGenerator : NpgsqlMigrationsSqlGenerator
{

    /// <summary>
    /// 当前整批 Migration SQL 统一使用的 UTC 时间
    /// </summary>
    private DateTimeOffset? generationUtcNow;


    /// <summary>
    /// 创建 PostgreSQL 分区 Migration SQL 生成器
    /// </summary>
    /// <param name="dependencies">Migration SQL 生成器依赖</param>
    /// <param name="npgsqlSingletonOptions">Npgsql 单例选项</param>
    public NpgsqlPartitionMigrationsSqlGenerator(MigrationsSqlGeneratorDependencies dependencies, INpgsqlSingletonOptions npgsqlSingletonOptions) : base(dependencies, npgsqlSingletonOptions)
    {

    }


    /// <summary>
    /// 使用同一个 UTC 时间生成整批 Migration 操作的 SQL
    /// </summary>
    /// <param name="operations">Migration 操作集合</param>
    /// <param name="model">EF Core 关系模型</param>
    /// <param name="options">Migration SQL 生成选项</param>
    /// <returns>生成的 Migration 命令</returns>
    public override IReadOnlyList<MigrationCommand> Generate(IReadOnlyList<MigrationOperation> operations, IModel? model = null, MigrationsSqlGenerationOptions options = MigrationsSqlGenerationOptions.Default)
    {

        generationUtcNow = DateTimeOffset.UtcNow;

        try
        {
            return base.Generate(operations, model, options);
        }
        finally
        {
            generationUtcNow = null;
        }

    }


    /// <summary>
    /// 为带分区 Annotation 的建表操作生成分区父表和当前子分区 SQL
    /// </summary>
    /// <param name="operation">建表操作</param>
    /// <param name="model">EF Core 关系模型</param>
    /// <param name="builder">Migration 命令构建器</param>
    /// <param name="terminate">是否结束当前命令</param>
    protected override void Generate(CreateTableOperation operation, IModel? model, MigrationCommandListBuilder builder, bool terminate = true)
    {

        if (operation.FindAnnotation(PartitionAnnotationNames.Strategy) is null)
        {
            base.Generate(operation, model, builder, terminate);
            return;
        }

        var definition = PartitionTableDefinition.Create(operation, operation.Schema, operation.Name);
        var temporaryBuilder = new MigrationCommandListBuilder(Dependencies);
        base.Generate(operation, model, temporaryBuilder, true);
        var commands = temporaryBuilder.GetCommandList();

        if (commands.Count == 0)
        {
            throw CreateSqlShapeException(operation.Name, "Npgsql 没有生成 CREATE TABLE 命令");
        }

        var createTableFound = false;
        foreach (var command in commands)
        {
            var commandText = command.CommandText;
            if (!createTableFound && TryInsertPartitionClause(commandText, definition.KeyColumnName, out var partitionedSql))
            {
                commandText = partitionedSql;
                createTableFound = true;
            }

            builder.Append(commandText);
            builder.EndCommand(command.TransactionSuppressed);
        }

        if (!createTableFound)
        {
            throw CreateSqlShapeException(operation.Name, "无法定位 CREATE TABLE 的表定义结束位置");
        }

        var range = definition.CreateInitialRange(generationUtcNow ?? DateTimeOffset.UtcNow);
        AppendCreatePartitionSql(builder, definition, range);

    }


    /// <summary>
    /// 校验已有表的分区 Annotation 变更并忽略只影响未来分区的周期变化
    /// </summary>
    /// <param name="operation">修改表操作</param>
    /// <param name="model">EF Core 关系模型</param>
    /// <param name="builder">Migration 命令构建器</param>
    protected override void Generate(AlterTableOperation operation, IModel? model, MigrationCommandListBuilder builder)
    {

        var currentHasPartition = operation.FindAnnotation(PartitionAnnotationNames.Strategy) is not null;
        var oldHasPartition = operation.OldTable.FindAnnotation(PartitionAnnotationNames.Strategy) is not null;

        if (currentHasPartition != oldHasPartition)
        {
            throw new NotSupportedException($"表 {operation.Name} 不能通过普通 Migration 新增或移除分区配置，请单独设计数据迁移");
        }

        if (!currentHasPartition)
        {
            base.Generate(operation, model, builder);
            return;
        }

        EnsureImmutableAnnotationUnchanged(operation, PartitionAnnotationNames.Strategy, "分区策略");
        EnsureImmutableAnnotationUnchanged(operation, PartitionAnnotationNames.KeyColumn, "分区键列");
        EnsureImmutableAnnotationUnchanged(operation, PartitionAnnotationNames.KeyType, "分区键类型");

        if (ContainsOnlyPolicyChanges(operation))
        {
            return;
        }

        base.Generate(operation, model, builder);

    }


    /// <summary>
    /// 把 PARTITION BY RANGE 子句插入 Npgsql 生成的 CREATE TABLE SQL
    /// </summary>
    /// <param name="sql">Npgsql 生成的 SQL</param>
    /// <param name="keyColumnName">分区键列名称</param>
    /// <param name="result">插入分区子句后的 SQL</param>
    /// <returns>能够安全定位表定义时返回 true</returns>
    private bool TryInsertPartitionClause(string sql, string keyColumnName, out string result)
    {

        result = sql;
        var openParenthesisIndex = FindFirstStructuralParenthesis(sql);
        if (openParenthesisIndex < 0)
        {
            return false;
        }

        var closeParenthesisIndex = FindMatchingParenthesis(sql, openParenthesisIndex);
        if (closeParenthesisIndex < 0)
        {
            return false;
        }

        var clause = " PARTITION BY RANGE (" + Dependencies.SqlGenerationHelper.DelimitIdentifier(keyColumnName) + ")";
        result = sql.Insert(closeParenthesisIndex + 1, clause);
        return true;

    }


    /// <summary>
    /// 定位 CREATE TABLE 语句中的首个结构性左括号
    /// </summary>
    /// <param name="sql">待分析 SQL</param>
    /// <returns>左括号位置，未找到时返回 -1</returns>
    private static int FindFirstStructuralParenthesis(string sql)
    {

        var tableTokenFound = false;

        for (var index = 0; index < sql.Length;)
        {
            if (TrySkipNonStructuralToken(sql, ref index))
            {
                continue;
            }

            if (IsIdentifierStart(sql[index]))
            {
                var start = index++;
                while (index < sql.Length && IsIdentifierPart(sql[index]))
                {
                    index++;
                }

                if (string.Equals(sql[start..index], "TABLE", StringComparison.OrdinalIgnoreCase))
                {
                    tableTokenFound = true;
                }

                continue;
            }

            if (tableTokenFound && sql[index] == '(')
            {
                return index;
            }

            index++;
        }

        return -1;

    }


    /// <summary>
    /// 定位与表定义左括号匹配的右括号
    /// </summary>
    /// <param name="sql">待分析 SQL</param>
    /// <param name="openParenthesisIndex">表定义左括号位置</param>
    /// <returns>匹配右括号位置，未找到时返回 -1</returns>
    private static int FindMatchingParenthesis(string sql, int openParenthesisIndex)
    {

        var depth = 0;

        for (var index = openParenthesisIndex; index < sql.Length;)
        {
            if (TrySkipNonStructuralToken(sql, ref index))
            {
                continue;
            }

            if (sql[index] == '(')
            {
                depth++;
            }
            else if (sql[index] == ')')
            {
                depth--;
                if (depth == 0)
                {
                    return index;
                }
            }

            index++;
        }

        return -1;

    }


    /// <summary>
    /// 跳过字符串、标识符、美元字符串和注释中的非结构性内容
    /// </summary>
    /// <param name="sql">待分析 SQL</param>
    /// <param name="index">当前扫描位置</param>
    /// <returns>当前位置包含可跳过内容时返回 true</returns>
    private static bool TrySkipNonStructuralToken(string sql, ref int index)
    {

        if (sql[index] is '\'' or '"')
        {
            var quote = sql[index];
            SkipQuoted(sql, ref index, quote, quote == '\'' && IsEscapeStringPrefix(sql, index));
            return true;
        }

        if (sql[index] == '-' && index + 1 < sql.Length && sql[index + 1] == '-')
        {
            index += 2;
            while (index < sql.Length && sql[index] is not '\r' and not '\n')
            {
                index++;
            }

            return true;
        }

        if (sql[index] == '/' && index + 1 < sql.Length && sql[index + 1] == '*')
        {
            SkipBlockComment(sql, ref index);
            return true;
        }

        if (sql[index] == '$' && TryReadDollarQuoteTag(sql, index, out var tag))
        {
            var endIndex = sql.IndexOf(tag, index + tag.Length, StringComparison.Ordinal);
            index = endIndex < 0 ? sql.Length : endIndex + tag.Length;
            return true;
        }

        return false;

    }


    /// <summary>
    /// 跳过单引号字符串或双引号标识符
    /// </summary>
    /// <param name="sql">待分析 SQL</param>
    /// <param name="index">当前扫描位置</param>
    /// <param name="quote">引号字符</param>
    /// <param name="supportsBackslashEscape">是否处理 PostgreSQL E 字符串的反斜杠转义</param>
    private static void SkipQuoted(string sql, ref int index, char quote, bool supportsBackslashEscape)
    {

        index++;
        while (index < sql.Length)
        {
            if (supportsBackslashEscape && sql[index] == '\\')
            {
                index += index + 1 < sql.Length ? 2 : 1;
                continue;
            }

            if (sql[index] != quote)
            {
                index++;
                continue;
            }

            if (index + 1 < sql.Length && sql[index + 1] == quote)
            {
                index += 2;
                continue;
            }

            index++;
            return;
        }

    }


    /// <summary>
    /// 判断单引号是否由 PostgreSQL E 前缀引入
    /// </summary>
    /// <param name="sql">待分析 SQL</param>
    /// <param name="quoteIndex">单引号位置</param>
    /// <returns>当前单引号属于 E 字符串时返回 true</returns>
    private static bool IsEscapeStringPrefix(string sql, int quoteIndex)
    {

        var prefixIndex = quoteIndex - 1;
        if (prefixIndex < 0 || sql[prefixIndex] is not 'E' and not 'e')
        {
            return false;
        }

        return prefixIndex == 0 || !IsIdentifierPart(sql[prefixIndex - 1]);

    }


    /// <summary>
    /// 跳过支持嵌套的 PostgreSQL 块注释
    /// </summary>
    /// <param name="sql">待分析 SQL</param>
    /// <param name="index">当前扫描位置</param>
    private static void SkipBlockComment(string sql, ref int index)
    {

        var depth = 1;
        index += 2;

        while (index < sql.Length && depth > 0)
        {
            if (index + 1 < sql.Length && sql[index] == '/' && sql[index + 1] == '*')
            {
                depth++;
                index += 2;
            }
            else if (index + 1 < sql.Length && sql[index] == '*' && sql[index + 1] == '/')
            {
                depth--;
                index += 2;
            }
            else
            {
                index++;
            }
        }

    }


    /// <summary>
    /// 尝试读取 PostgreSQL 美元字符串分隔标签
    /// </summary>
    /// <param name="sql">待分析 SQL</param>
    /// <param name="startIndex">美元符号位置</param>
    /// <param name="tag">完整美元字符串标签</param>
    /// <returns>当前位置是合法标签时返回 true</returns>
    private static bool TryReadDollarQuoteTag(string sql, int startIndex, out string tag)
    {

        var index = startIndex + 1;
        while (index < sql.Length && (char.IsLetterOrDigit(sql[index]) || sql[index] == '_'))
        {
            index++;
        }

        if (index < sql.Length && sql[index] == '$')
        {
            tag = sql[startIndex..(index + 1)];
            return true;
        }

        tag = string.Empty;
        return false;

    }


    /// <summary>
    /// 判断字符是否可以作为未加引号标识符的起始字符
    /// </summary>
    /// <param name="value">待判断字符</param>
    /// <returns>可以作为起始字符时返回 true</returns>
    private static bool IsIdentifierStart(char value)
    {

        return char.IsLetter(value) || value == '_';

    }


    /// <summary>
    /// 判断字符是否可以作为未加引号标识符的后续字符
    /// </summary>
    /// <param name="value">待判断字符</param>
    /// <returns>可以作为后续字符时返回 true</returns>
    private static bool IsIdentifierPart(char value)
    {

        return char.IsLetterOrDigit(value) || value is '_' or '$';

    }


    /// <summary>
    /// 追加创建当前子分区的 SQL
    /// </summary>
    /// <param name="builder">Migration 命令构建器</param>
    /// <param name="definition">分区表定义</param>
    /// <param name="range">初始分区范围</param>
    private void AppendCreatePartitionSql(MigrationCommandListBuilder builder, PartitionTableDefinition definition, PartitionRange range)
    {

        var partitionName = PartitionNameBuilder.Create(definition.TableName, range.StartTime);
        var parentIdentifier = Dependencies.SqlGenerationHelper.DelimitIdentifier(definition.TableName, definition.Schema);
        var partitionIdentifier = Dependencies.SqlGenerationHelper.DelimitIdentifier(partitionName, definition.Schema);

        builder.Append("CREATE TABLE ")
            .Append(partitionIdentifier)
            .Append(" PARTITION OF ")
            .Append(parentIdentifier)
            .Append(" FOR VALUES FROM (")
            .Append(range.StartId.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .Append(") TO (")
            .Append(range.EndId.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .Append(")")
            .AppendLine(Dependencies.SqlGenerationHelper.StatementTerminator)
            .EndCommand();

    }


    /// <summary>
    /// 确保不能在线变更的分区 Annotation 保持不变
    /// </summary>
    /// <param name="operation">修改表操作</param>
    /// <param name="annotationName">Annotation 名称</param>
    /// <param name="displayName">用于错误信息的配置名称</param>
    private static void EnsureImmutableAnnotationUnchanged(AlterTableOperation operation, string annotationName, string displayName)
    {

        var currentValue = operation.FindAnnotation(annotationName)?.Value;
        var oldValue = operation.OldTable.FindAnnotation(annotationName)?.Value;
        if (!Equals(currentValue, oldValue))
        {
            throw new NotSupportedException($"表 {operation.Name} 的{displayName}不能通过普通 Migration 修改，请单独设计数据迁移");
        }

    }


    /// <summary>
    /// 判断修改表操作是否只变更允许向前生效的周期策略
    /// </summary>
    /// <param name="operation">修改表操作</param>
    /// <returns>只包含分区周期策略变化时返回 true</returns>
    private static bool ContainsOnlyPolicyChanges(AlterTableOperation operation)
    {

        if (!string.Equals(operation.Comment, operation.OldTable.Comment, StringComparison.Ordinal))
        {
            return false;
        }

        var currentAnnotations = operation.GetAnnotations()
            .Where(annotation => !PartitionAnnotationNames.GetAll().Contains(annotation.Name))
            .ToDictionary(annotation => annotation.Name, annotation => annotation.Value, StringComparer.Ordinal);
        var oldAnnotations = operation.OldTable.GetAnnotations()
            .Where(annotation => !PartitionAnnotationNames.GetAll().Contains(annotation.Name))
            .ToDictionary(annotation => annotation.Name, annotation => annotation.Value, StringComparer.Ordinal);

        return currentAnnotations.Count == oldAnnotations.Count
            && currentAnnotations.All(annotation => oldAnnotations.TryGetValue(annotation.Key, out var oldValue) && Equals(annotation.Value, oldValue));

    }


    /// <summary>
    /// 创建包含 Npgsql 版本信息的 SQL 结构兼容异常
    /// </summary>
    /// <param name="tableName">目标表名</param>
    /// <param name="reason">无法生成的原因</param>
    /// <returns>SQL 结构兼容异常</returns>
    private static InvalidOperationException CreateSqlShapeException(string tableName, string reason)
    {

        var providerVersion = typeof(NpgsqlMigrationsSqlGenerator).Assembly.GetName().Version?.ToString() ?? "unknown";
        return new InvalidOperationException($"Npgsql {providerVersion} 为分区表 {tableName} 生成的 CREATE TABLE SQL 无法安全扩展：{reason}");

    }

}
