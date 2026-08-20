using Repository.Partitioning;
using TaskService.Core;
using TaskService.Core.ScheduleTask;

namespace TaskService.Tasks;

/// <summary>
/// 提供数据库结构的标准定时维护任务
/// </summary>
public class DatabaseMaintenanceTask(PartitionMaintenanceService partitionMaintenanceService) : TaskBase
{

    /// <summary>
    /// 检查分区表并创建当前写入所需及一个后续子分区
    /// </summary>
    /// <returns>分区维护任务</returns>
    [ScheduleTask(Name = "Database.EnsurePartitions", Cron = "0 0/10 * * * ?", SkipIfRunning = true)]
    public Task EnsurePartitionsAsync()
    {

        return partitionMaintenanceService.EnsurePartitionsAsync();

    }

}
