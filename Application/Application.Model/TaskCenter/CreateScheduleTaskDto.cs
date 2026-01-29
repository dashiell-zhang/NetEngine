using System.ComponentModel.DataAnnotations;

namespace Application.Model.TaskCenter;

/// <summary>
/// 新增带参定时任务
/// </summary>
public class CreateScheduleTaskDto
{

    /// <summary>
    /// 任务名称（对应 ScheduleTaskAttribute.Name）
    /// </summary>
    [Required(ErrorMessage = "Name 不可以空")]
    public string Name { get; set; }


    /// <summary>
    /// 参数(JSON) - 必填
    /// </summary>
    [Required(ErrorMessage = "Parameter 不可以空")]
    public string Parameter { get; set; }


    /// <summary>
    /// Cron 表达式（可选；为空则使用特性默认值）
    /// </summary>
    public string? Cron { get; set; }


    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnable { get; set; } = true;


    /// <summary>
    /// 备注
    /// </summary>
    public string? Remarks { get; set; }
}

