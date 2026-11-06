namespace Application.Model.TaskCenter;

/// <summary>
/// 编辑任务配置
/// </summary>
public class EditTaskSettingDto
{

    /// <summary>
    /// 参数(JSON)
    /// </summary>
    public string? Parameter { get; set; }


    /// <summary>
    /// 并发值，为 null 时恢复任务声明默认值
    /// </summary>
    public int? Semaphore { get; set; }


    /// <summary>
    /// Cron 表达式，为空或空白时恢复任务声明默认值
    /// </summary>
    public string? Cron { get; set; }


    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnable { get; set; }


    /// <summary>
    /// 备注
    /// </summary>
    public string? Remark { get; set; }
}
