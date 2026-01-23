namespace Application.Model.Basic.Log;

/// <summary>
/// 日志
/// </summary>
public class LogDto
{

    /// <summary>
    /// 标识ID
    /// </summary>
    public long Id { get; set; }


    /// <summary>
    /// 项目
    /// </summary>
    public string Project { get; set; }


    /// <summary>
    /// 机器名称
    /// </summary>
    public string MachineName { get; set; }


    /// <summary>
    /// 日志等级
    /// </summary>
    public string Level { get; set; }


    /// <summary>
    /// 类别
    /// </summary>
    public string Category { get; set; }


    /// <summary>
    /// 内容
    /// </summary>
    public string Content { get; set; }


    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }
}

