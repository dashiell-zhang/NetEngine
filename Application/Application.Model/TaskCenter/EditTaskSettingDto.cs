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
}

