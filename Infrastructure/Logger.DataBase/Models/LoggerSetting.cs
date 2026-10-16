using System.Reflection;

namespace Logger.DataBase.Models;
/// <summary>
/// 配置数据库日志的保留时间和有界内存缓冲
/// </summary>
public class LoggerSetting
{

    /// <summary>
    /// 项目
    /// </summary>
    public string Project { get; set; } = Assembly.GetEntryAssembly()?.GetName().Name!;



    /// <summary>
    /// 保存天数
    /// </summary>
    /// <remarks>永久保存：-1</remarks>
    public int SaveDays { get; set; } = 14;


    /// <summary>
    /// 所有级别共用的队列容量
    /// </summary>
    public int QueueCapacity { get; set; } = 8192;


    /// <summary>
    /// 共用缓冲的估算字节上限，包含正在落盘的日志
    /// </summary>
    public long MaxBufferedBytes { get; set; } = 48 * 1024 * 1024;


    /// <summary>
    /// 共用缓冲满载后为 Warning 及以上日志预留的队列容量
    /// </summary>
    public int ImportantQueueCapacity { get; set; } = 2048;


    /// <summary>
    /// 重要日志预留缓冲的估算字节上限
    /// </summary>
    public long ImportantMaxBufferedBytes { get; set; } = 16 * 1024 * 1024;


    /// <summary>
    /// 每批最多落盘的日志条数
    /// </summary>
    public int BatchSize { get; set; } = 1000;


    /// <summary>
    /// 未满批次的日志落盘时间间隔
    /// </summary>
    public TimeSpan FlushInterval { get; set; } = TimeSpan.FromSeconds(1);

}
