using System.ComponentModel.DataAnnotations;

namespace Application.Model.TaskCenter;

/// <summary>
/// 任务配置
/// </summary>
public class TaskSettingDto
{

    /// <summary>
    /// 标识ID
    /// </summary>
    public long Id { get; set; }


    /// <summary>
    /// 任务名称
    /// </summary>
    [Required(ErrorMessage = "Name 不可以空")]
    public string Name { get; set; }


    /// <summary>
    /// 任务种类["QueueTask","ScheduleTask"]
    /// </summary>
    [Required(ErrorMessage = "Category 不可以空")]
    public string Category { get; set; }


    /// <summary>
    /// 参数(JSON)
    /// </summary>
    public string? Parameter { get; set; }


    /// <summary>
    /// 并发值
    /// </summary>
    public int? Semaphore { get; set; }


    /// <summary>
    /// 预期持续时间(单位：分)
    /// </summary>
    public int? Duration { get; set; }


    /// <summary>
    /// Cron 表达式
    /// </summary>
    public string? Cron { get; set; }


    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnable { get; set; }


    /// <summary>
    /// 备注
    /// </summary>
    public string? Remarks { get; set; }


    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }


    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTimeOffset? UpdateTime { get; set; }
}

