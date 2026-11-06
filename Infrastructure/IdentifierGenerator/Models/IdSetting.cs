namespace IdentifierGenerator.Models;
/// <summary>
/// 配置固定雪花节点或共享 Redis 中的自动节点租约
/// </summary>
public class IdSetting
{


    /// <summary>
    /// 数据中心标识Id
    /// </summary>
    public int? DataCenterId { get; set; }



    /// <summary>
    /// 机器标识Id
    /// </summary>
    public int? MachineId { get; set; }


    /// <summary>
    /// 自动分配节点时使用的共享 Redis 连接，独立于业务缓存
    /// </summary>
    public string RedisConfiguration { get; set; } = "";


    /// <summary>
    /// 全部共享 ID 节点使用的协调键前缀
    /// </summary>
    public string InstanceName { get; set; } = "NetEngine.Identifier";


    /// <summary>
    /// 节点占用时长，全部共享节点应采用相同配置
    /// </summary>
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromDays(2);

}
