namespace Application.Model.Events;

/// <summary>
/// 通知任务宿主重新读取队列和定时任务配置
/// </summary>
public sealed record TaskSettingChangedEvent;
