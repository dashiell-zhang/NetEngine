using System.Globalization;

namespace Logger.DataBase;

/// <summary>
/// 统一识别本组件生成的批次、临时及异常保留文件名
/// </summary>
internal static class LogFileName
{

    /// <summary>
    /// 识别正常批次或目标重名后生成的恢复批次
    /// </summary>
    internal static bool IsBatchFile(string path)
    {

        return IsBatchName(Path.GetFileName(path).AsSpan());

    }


    /// <summary>
    /// 仅识别正常批次写入时产生的临时文件
    /// </summary>
    internal static bool IsTemporaryFile(string path)
    {

        var name = Path.GetFileName(path).AsSpan();
        return name.EndsWith(".tmp", StringComparison.Ordinal) && IsNormalBatchName(name[..^4]);

    }


    /// <summary>
    /// 识别正常或恢复批次因内容损坏而保留的完整原文件
    /// </summary>
    internal static bool IsRejectedFile(string path)
    {

        var name = Path.GetFileName(path).AsSpan();
        return name.EndsWith(".rejected", StringComparison.Ordinal) && IsBatchName(name[..^9]);

    }


    /// <summary>
    /// 校验完整批次名称，恢复批次只包含固定前缀和无连字符的 GUID
    /// </summary>
    private static bool IsBatchName(ReadOnlySpan<char> name)
    {

        return IsNormalBatchName(name)
            || (name.Length == 52 && name.StartsWith("batch-recovered-", StringComparison.Ordinal)
                && name.EndsWith(".log", StringComparison.Ordinal) && Guid.TryParseExact(name.Slice(16, 32), "N", out _));

    }


    /// <summary>
    /// 校验固定长度、UTC 公历时间戳及无连字符的 GUID，排除相似名称
    /// </summary>
    private static bool IsNormalBatchName(ReadOnlySpan<char> name)
    {

        if (name.Length != 60 || !name.StartsWith("batch-", StringComparison.Ordinal)
            || name[23] != '-' || !name.EndsWith(".log", StringComparison.Ordinal))
        {
            return false;
        }

        var timestamp = name.Slice(6, 17);
        foreach (var character in timestamp)
        {
            if (character is < '0' or > '9')
            {
                return false;
            }
        }

        return DateTime.TryParseExact(timestamp, "yyyyMMddHHmmssfff", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
            && Guid.TryParseExact(name.Slice(24, 32), "N", out _);

    }

}
