namespace Deployment.Generator;

/// <summary>
/// 部署配置生成参数
/// </summary>
internal sealed class DeploymentSettings
{

    /// <summary>
    /// 项目标识
    /// </summary>
    public required string ProjectName { get; init; }


    /// <summary>
    /// 各项目访问域名
    /// </summary>
    public required HostSettings Host { get; init; }


    /// <summary>
    /// 各项目监听端口
    /// </summary>
    public required PortSettings Port { get; init; }


    /// <summary>
    /// 应用部署根目录
    /// </summary>
    public required string DeployRoot { get; init; }


    /// <summary>
    /// 云效流水线配置
    /// </summary>
    public required YunXiaoSettings YunXiao { get; init; }

}

/// <summary>
/// 各项目访问域名配置
/// </summary>
internal sealed class HostSettings
{

    /// <summary>
    /// 管理端站点域名
    /// </summary>
    public required HostItemSettings AdminApp { get; init; }


    /// <summary>
    /// 管理端接口域名
    /// </summary>
    public required HostItemSettings AdminWebAPI { get; init; }


    /// <summary>
    /// 客户端接口域名
    /// </summary>
    public required HostItemSettings ClientWebAPI { get; init; }

}

/// <summary>
/// 单个项目访问域名配置
/// </summary>
internal sealed class HostItemSettings
{

    /// <summary>
    /// 访问域名
    /// </summary>
    public required string Domain { get; init; }


    /// <summary>
    /// Nginx 证书文件路径
    /// </summary>
    public required string CertificateFile { get; init; }


    /// <summary>
    /// Nginx 证书私钥文件路径
    /// </summary>
    public required string CertificateKeyFile { get; init; }

}

/// <summary>
/// 各项目监听端口配置
/// </summary>
internal sealed class PortSettings
{

    /// <summary>
    /// 管理端接口端口
    /// </summary>
    public int AdminWebAPI { get; init; }


    /// <summary>
    /// 客户端接口端口
    /// </summary>
    public int ClientWebAPI { get; init; }

}

/// <summary>
/// 云效流水线配置
/// </summary>
internal sealed class YunXiaoSettings
{

    /// <summary>
    /// Codeup 代码源配置
    /// </summary>
    public required CodeupSettings Codeup { get; init; }


    /// <summary>
    /// 部署机器组标识
    /// </summary>
    public required string MachineGroup { get; init; }

}

/// <summary>
/// Codeup 代码源配置
/// </summary>
internal sealed class CodeupSettings
{

    /// <summary>
    /// 代码源名称
    /// </summary>
    public required string Name { get; init; }


    /// <summary>
    /// 仓库地址
    /// </summary>
    public required string Endpoint { get; init; }


    /// <summary>
    /// 构建分支
    /// </summary>
    public required string Branch { get; init; }


    /// <summary>
    /// 服务连接标识
    /// </summary>
    public required string ServiceConnection { get; init; }

}
