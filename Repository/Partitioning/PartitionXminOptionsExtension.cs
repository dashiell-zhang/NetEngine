using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Update;
using Microsoft.Extensions.DependencyInjection;

namespace Repository.Partitioning;

/// <summary>
/// 装饰提供程序的公共命令工厂，保留 Npgsql 原有命令和 JSON 等专用行为
/// </summary>
internal sealed class PartitionXminOptionsExtension(PartitionTableSnapshot? snapshot) : IDbContextOptionsExtension
{

    /// <summary>
    /// 当前写库在启动时加载的只读快照，不参与共享模型或单例服务的构建
    /// </summary>
    public PartitionTableSnapshot? Snapshot { get; } = snapshot;


    /// <summary>
    /// 服务配置与数据库快照无关的扩展信息
    /// </summary>
    public DbContextOptionsExtensionInfo Info => new ExtensionInfo(this);


    /// <summary>
    /// 沿用原服务描述和生命周期，不引用或反射访问提供程序内部成员
    /// </summary>
    public void ApplyServices(IServiceCollection services)
    {

        // 公共注册入口使用 TryAdd，使分区扩展与 UseNpgsql 的调用顺序无关
        services.AddEntityFrameworkNpgsql();
        var original = services.Last(service => service.ServiceType == typeof(IModificationCommandFactory));
        services.Remove(original);
        services.Add(new ServiceDescriptor(typeof(IModificationCommandFactory), provider =>
        {
            var factory = (IModificationCommandFactory)(original.ImplementationInstance
                ?? original.ImplementationFactory?.Invoke(provider)
                ?? ActivatorUtilities.CreateInstance(provider, original.ImplementationType!));
            return new PartitionXminModificationCommandFactory(factory);
        }, original.Lifetime));

    }


    /// <summary>
    /// 本扩展没有需要额外校验的配置
    /// </summary>
    public void Validate(IDbContextOptions options)
    {

    }


    /// <summary>
    /// 允许相同分区扩展配置复用 EF 内部服务提供程序
    /// </summary>
    private sealed class ExtensionInfo(IDbContextOptionsExtension extension) : DbContextOptionsExtensionInfo(extension)
    {

        /// <summary>
        /// 分区能力不替代数据库提供程序
        /// </summary>
        public override bool IsDatabaseProvider => false;


        /// <summary>
        /// 输出启用的保存能力
        /// </summary>
        public override string LogFragment => "PartitionXmin ";


        /// <summary>
        /// 扩展没有影响服务实例的可变选项
        /// </summary>
        public override int GetServiceProviderHashCode() => 0;


        /// <summary>
        /// 同类型扩展使用相同服务配置
        /// </summary>
        public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other) => other is ExtensionInfo;


        /// <summary>
        /// 标识分区命令能力已经启用
        /// </summary>
        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo) => debugInfo["NetEngine:PartitionXmin"] = "1";

    }

}
