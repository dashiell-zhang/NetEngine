using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Repository.Partitioning;

/// <summary>
/// 为 PostgreSQL 子分区生成稳定且不超过标识符长度限制的名称
/// </summary>
public static class PartitionNameBuilder
{

    /// <summary>
    /// PostgreSQL 默认允许的标识符最大字节数
    /// </summary>
    private const int MaxIdentifierBytes = 63;


    /// <summary>
    /// 根据父表和实际起始时间创建子分区名称
    /// </summary>
    /// <param name="tableName">父表名称</param>
    /// <param name="startTime">子分区起始时间</param>
    /// <returns>稳定的子分区名称</returns>
    public static string Create(string tableName, DateTimeOffset startTime)
    {

        var partitionStartTime = PartitionTimeLayout.ToPartitionTime(startTime);
        var timeSuffix = partitionStartTime.ToString("yyyyMMddHH", CultureInfo.InvariantCulture);
        var suffix = "_p" + timeSuffix;
        var fullName = tableName + suffix;

        if (Encoding.UTF8.GetByteCount(fullName) <= MaxIdentifierBytes)
        {
            return fullName;
        }

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(tableName)))[..8].ToLowerInvariant();
        suffix += "_" + hash;
        var prefix = tableName;

        while (prefix.Length > 0 && Encoding.UTF8.GetByteCount(prefix + suffix) > MaxIdentifierBytes)
        {
            prefix = prefix[..^1];
        }

        return prefix + suffix;

    }

}
