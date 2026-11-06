using IdentifierGenerator.Models;
using Microsoft.Extensions.DependencyInjection;

namespace IdentifierGenerator;

/// <summary>
/// 为整个宿主注册唯一的雪花 ID 服务
/// </summary>
public static class ServiceCollectionExtensions
{

    /// <summary>
    /// 明确配置固定节点或共享 Redis 自动节点，重复注册直接失败
    /// </summary>
    public static void AddIdentifierGenerator(this IServiceCollection services, Action<IdSetting> action)
    {

        if (services.Any(descriptor => descriptor.ServiceType == typeof(IdService)))
        {
            throw new InvalidOperationException("一个宿主只能注册一个 IdService");
        }

        var configured = new IdSetting();
        action(configured);
        var settings = new IdSetting
        {
            DataCenterId = configured.DataCenterId,
            MachineId = configured.MachineId,
            RedisConfiguration = configured.RedisConfiguration,
            InstanceName = configured.InstanceName,
            LeaseDuration = configured.LeaseDuration
        };
        if (settings.DataCenterId.HasValue != settings.MachineId.HasValue)
        {
            throw new ArgumentException("固定节点必须同时指定数据中心与机器编号");
        }

        if (settings.DataCenterId is int dataCenter && settings.MachineId is int machine)
        {
            if (dataCenter is < 0 or > 31 || machine is < 0 or > 31)
            {
                throw new ArgumentOutOfRangeException(nameof(action), "固定节点编号必须在 0 到 31 之间");
            }
            services.AddSingleton(_ => new IdService(dataCenter, machine));
            return;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(settings.RedisConfiguration);
        ArgumentException.ThrowIfNullOrWhiteSpace(settings.InstanceName);
        if (settings.LeaseDuration < TimeSpan.FromSeconds(3) || settings.LeaseDuration > TimeSpan.FromDays(30))
        {
            throw new ArgumentOutOfRangeException(nameof(action), "节点租约必须介于 3 秒与 30 天之间");
        }
        services.AddSingleton(_ => IdService.CreateAutomatic(settings));

    }

}
