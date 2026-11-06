using Npgsql;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Logger.Database;

/// <summary>
/// 将本进程日志积压固定到存储目标和项目，切换数据库不消费旧目标文件
/// </summary>
internal sealed class LogSpoolDirectory
{

    /// <summary>
    /// 从不含密码的目标信息计算目录，账号参与默认 schema 解析边界
    /// </summary>
    public LogSpoolDirectory(string connectionString, string databaseIdentity, string project)
    {

        var connection = new NpgsqlConnectionStringBuilder(connectionString);
        var identity = JsonSerializer.Serialize(new[] { databaseIdentity, project, connection.Host, connection.Port.ToString(System.Globalization.CultureInfo.InvariantCulture), connection.Database, connection.Username, connection.SearchPath, connection.Options });
        var name = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
        DirectoryPath = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "Database", name);

    }


    /// <summary>
    /// 本次运行唯一消费的批次目录
    /// </summary>
    public string DirectoryPath { get; }

}
