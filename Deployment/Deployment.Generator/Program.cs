using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Deployment.Generator;

/// <summary>
/// 部署配置生成程序
/// </summary>
internal static class Program
{

    /// <summary>
    /// 启动部署配置生成程序
    /// </summary>
    private static void Main()
    {

        try
        {
            Generate();
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"生成部署配置失败：{exception.Message}");
            Environment.ExitCode = 1;
        }

    }


    /// <summary>
    /// 根据固定位置的配置和模板生成部署文件
    /// </summary>
    private static void Generate()
    {

        var generatorDirectory = FindGeneratorDirectory();
        var settingsPath = Path.Combine(generatorDirectory, "deploysettings.json");
        var settingsContent = File.ReadAllText(settingsPath, Encoding.UTF8);
        var settings = JsonSerializer.Deserialize<DeploymentSettings>(settingsContent) ?? throw new InvalidOperationException("无法读取 deploysettings.json");

        ValidateSettings(settings);

        var templateValues = CreateTemplateValues(settings);
        var generatedDirectory = Path.Combine(generatorDirectory, "Generated");
        var projectName = settings.ProjectName.ToLowerInvariant();
        (string TemplateRelativePath, string OutputRelativePath)[] generatedFiles =
        {
            ("Templates/nginx/nginx.conf.template", "nginx/nginx.conf"),
            ("Templates/nginx/Admin.App.conf.template", $"nginx/{projectName}-admin-app.conf"),
            ("Templates/nginx/Admin.WebAPI.conf.template", $"nginx/{projectName}-admin-webapi.conf"),
            ("Templates/nginx/Client.WebAPI.conf.template", $"nginx/{projectName}-client-webapi.conf"),
            ("Templates/service/Admin.WebAPI.service.template", $"service/{projectName}-admin-webapi.service"),
            ("Templates/service/Client.WebAPI.service.template", $"service/{projectName}-client-webapi.service"),
            ("Templates/service/TaskService.service.template", $"service/{projectName}-task-service.service"),
            ("Templates/service/install-services.txt.template", $"service/{projectName}-install-services.txt"),
            ("Templates/yunxiao/Admin.App.yaml.template", $"yunxiao/{projectName}-admin-app.yaml"),
            ("Templates/yunxiao/Admin.WebAPI.yaml.template", $"yunxiao/{projectName}-admin-webapi.yaml"),
            ("Templates/yunxiao/Client.WebAPI.yaml.template", $"yunxiao/{projectName}-client-webapi.yaml"),
            ("Templates/yunxiao/TaskService.yaml.template", $"yunxiao/{projectName}-task-service.yaml")
        };

        GenerateFilesAtomically(generatorDirectory, generatedDirectory, generatedFiles, templateValues);

        Console.WriteLine($"部署配置已生成到 {generatedDirectory}");

    }


    /// <summary>
    /// 在临时目录中完整生成文件并原子替换现有结果
    /// </summary>
    /// <param name="generatorDirectory">生成器目录</param>
    /// <param name="generatedDirectory">生成文件根目录</param>
    /// <param name="generatedFiles">待生成文件定义</param>
    /// <param name="templateValues">模板占位符值</param>
    private static void GenerateFilesAtomically(string generatorDirectory, string generatedDirectory, IReadOnlyCollection<(string TemplateRelativePath, string OutputRelativePath)> generatedFiles, Dictionary<string, string> templateValues)
    {

        var directorySuffix = Guid.NewGuid().ToString("N");
        var stagingDirectory = Path.Combine(generatorDirectory, $".Generated-{directorySuffix}");
        var backupDirectory = Path.Combine(generatorDirectory, $".Generated-backup-{directorySuffix}");
        var existingDirectoryMoved = false;

        try
        {
            foreach (var generatedFile in generatedFiles)
            {
                GenerateFile(generatorDirectory, stagingDirectory, generatedFile.TemplateRelativePath, generatedFile.OutputRelativePath, templateValues);
            }

            if (Directory.Exists(generatedDirectory))
            {
                Directory.Move(generatedDirectory, backupDirectory);
                existingDirectoryMoved = true;
            }

            try
            {
                Directory.Move(stagingDirectory, generatedDirectory);
            }
            catch (Exception replaceException)
            {
                if (existingDirectoryMoved)
                {
                    try
                    {
                        Directory.Move(backupDirectory, generatedDirectory);
                    }
                    catch (Exception restoreException)
                    {
                        throw new AggregateException("替换生成目录失败，并且无法恢复原有生成结果", replaceException, restoreException);
                    }
                }

                throw;
            }

            if (existingDirectoryMoved)
            {
                TryDeleteTemporaryDirectory(backupDirectory);
            }

            foreach (var generatedFile in generatedFiles)
            {
                Console.WriteLine($"已生成 {Path.Combine("Generated", generatedFile.OutputRelativePath.Replace('/', Path.DirectorySeparatorChar))}");
            }
        }
        finally
        {
            TryDeleteTemporaryDirectory(stagingDirectory);
        }

    }


    /// <summary>
    /// 尝试清理生成过程中使用的临时目录
    /// </summary>
    /// <param name="directory">待清理目录</param>
    private static void TryDeleteTemporaryDirectory(string directory)
    {

        if (Directory.Exists(directory) == false)
        {
            return;
        }

        try
        {
            Directory.Delete(directory, true);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"警告：无法清理临时目录 {directory}：{exception.Message}");
        }

    }


    /// <summary>
    /// 查找包含配置和模板的生成器目录
    /// </summary>
    /// <returns>生成器目录绝对路径</returns>
    private static string FindGeneratorDirectory()
    {

        var generatorDirectory = FindGeneratorDirectory(Directory.GetCurrentDirectory());
        if (generatorDirectory is not null)
        {
            return generatorDirectory;
        }

        generatorDirectory = FindGeneratorDirectory(AppContext.BaseDirectory);
        if (generatorDirectory is not null)
        {
            return generatorDirectory;
        }

        throw new DirectoryNotFoundException("未找到包含 deploysettings.json 和 Templates 的 Deployment.Generator 目录");

    }


    /// <summary>
    /// 从指定路径向上查找生成器目录
    /// </summary>
    /// <param name="startPath">查找起始路径</param>
    /// <returns>生成器目录绝对路径</returns>
    private static string? FindGeneratorDirectory(string startPath)
    {

        DirectoryInfo? currentDirectory = new(startPath);

        while (currentDirectory is not null)
        {
            var candidates = new[]
            {
                currentDirectory.FullName,
                Path.Combine(currentDirectory.FullName, "Deployment.Generator"),
                Path.Combine(currentDirectory.FullName, "Deployment", "Deployment.Generator")
            };

            foreach (var candidate in candidates)
            {
                if (IsGeneratorDirectory(candidate))
                {
                    return candidate;
                }
            }

            currentDirectory = currentDirectory.Parent;
        }

        return null;

    }


    /// <summary>
    /// 判断指定路径是否为生成器目录
    /// </summary>
    /// <param name="directory">待判断目录</param>
    /// <returns>是否为生成器目录</returns>
    private static bool IsGeneratorDirectory(string directory)
    {

        return File.Exists(Path.Combine(directory, "deploysettings.json")) && Directory.Exists(Path.Combine(directory, "Templates"));

    }


    /// <summary>
    /// 校验生成部署文件所需的基础配置
    /// </summary>
    /// <param name="settings">部署配置</param>
    private static void ValidateSettings(DeploymentSettings settings)
    {

        Dictionary<string, string> requiredValues = new()
        {
            [nameof(settings.ProjectName)] = settings.ProjectName,
            ["Host.AdminApp.Domain"] = settings.Host.AdminApp.Domain,
            ["Host.AdminApp.CertificateFile"] = settings.Host.AdminApp.CertificateFile,
            ["Host.AdminApp.CertificateKeyFile"] = settings.Host.AdminApp.CertificateKeyFile,
            ["Host.AdminWebAPI.Domain"] = settings.Host.AdminWebAPI.Domain,
            ["Host.AdminWebAPI.CertificateFile"] = settings.Host.AdminWebAPI.CertificateFile,
            ["Host.AdminWebAPI.CertificateKeyFile"] = settings.Host.AdminWebAPI.CertificateKeyFile,
            ["Host.ClientWebAPI.Domain"] = settings.Host.ClientWebAPI.Domain,
            ["Host.ClientWebAPI.CertificateFile"] = settings.Host.ClientWebAPI.CertificateFile,
            ["Host.ClientWebAPI.CertificateKeyFile"] = settings.Host.ClientWebAPI.CertificateKeyFile,
            [nameof(settings.DeployRoot)] = settings.DeployRoot,
            ["YunXiao.Codeup.Name"] = settings.YunXiao.Codeup.Name,
            ["YunXiao.Codeup.Endpoint"] = settings.YunXiao.Codeup.Endpoint,
            ["YunXiao.Codeup.Branch"] = settings.YunXiao.Codeup.Branch,
            ["YunXiao.Codeup.ServiceConnection"] = settings.YunXiao.Codeup.ServiceConnection,
            ["YunXiao.MachineGroup"] = settings.YunXiao.MachineGroup
        };

        foreach (var requiredValue in requiredValues)
        {
            if (string.IsNullOrWhiteSpace(requiredValue.Value))
            {
                throw new InvalidOperationException($"配置项 {requiredValue.Key} 不能为空");
            }
        }

        if (Regex.IsMatch(settings.ProjectName, "^[A-Za-z0-9]+(?:-[A-Za-z0-9]+)*$", RegexOptions.CultureInvariant) == false)
        {
            throw new InvalidOperationException("配置项 ProjectName 只能包含英文字母、数字和中间连字符，并且必须以字母或数字开头和结尾");
        }

        if (Regex.IsMatch(settings.DeployRoot, "^/(?:[A-Za-z0-9._-]+/)*[A-Za-z0-9._-]+/?$", RegexOptions.CultureInvariant) == false || settings.DeployRoot.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(pathSegment => pathSegment is "." or ".."))
        {
            throw new InvalidOperationException("配置项 DeployRoot 必须是非根目录的 Linux 绝对路径，路径段只能包含英文字母、数字、点、下划线和中间连字符，并且不能包含 . 或 .. 路径段");
        }

        if (settings.Port.AdminWebAPI is < 1 or > 65535)
        {
            throw new InvalidOperationException("配置项 Port.AdminWebAPI 必须在 1 到 65535 之间");
        }

        if (settings.Port.ClientWebAPI is < 1 or > 65535)
        {
            throw new InvalidOperationException("配置项 Port.ClientWebAPI 必须在 1 到 65535 之间");
        }

        if (settings.Port.AdminWebAPI == settings.Port.ClientWebAPI)
        {
            throw new InvalidOperationException("配置项 Port.AdminWebAPI 与 Port.ClientWebAPI 不能使用相同端口");
        }

    }


    /// <summary>
    /// 创建模板占位符与配置值的对应关系
    /// </summary>
    /// <param name="settings">部署配置</param>
    /// <returns>模板占位符值</returns>
    private static Dictionary<string, string> CreateTemplateValues(DeploymentSettings settings)
    {

        return new(StringComparer.Ordinal)
        {
            [nameof(settings.ProjectName)] = settings.ProjectName,
            ["ProjectNameLower"] = settings.ProjectName.ToLowerInvariant(),
            ["Host.AdminApp.Domain"] = settings.Host.AdminApp.Domain,
            ["Host.AdminApp.CertificateFile"] = settings.Host.AdminApp.CertificateFile,
            ["Host.AdminApp.CertificateKeyFile"] = settings.Host.AdminApp.CertificateKeyFile,
            ["Host.AdminWebAPI.Domain"] = settings.Host.AdminWebAPI.Domain,
            ["Host.AdminWebAPI.CertificateFile"] = settings.Host.AdminWebAPI.CertificateFile,
            ["Host.AdminWebAPI.CertificateKeyFile"] = settings.Host.AdminWebAPI.CertificateKeyFile,
            ["Host.ClientWebAPI.Domain"] = settings.Host.ClientWebAPI.Domain,
            ["Host.ClientWebAPI.CertificateFile"] = settings.Host.ClientWebAPI.CertificateFile,
            ["Host.ClientWebAPI.CertificateKeyFile"] = settings.Host.ClientWebAPI.CertificateKeyFile,
            ["Port.AdminWebAPI"] = settings.Port.AdminWebAPI.ToString(),
            ["Port.ClientWebAPI"] = settings.Port.ClientWebAPI.ToString(),
            [nameof(settings.DeployRoot)] = settings.DeployRoot.TrimEnd('/'),
            ["YunXiao.Codeup.Name"] = settings.YunXiao.Codeup.Name,
            ["YunXiao.Codeup.Endpoint"] = settings.YunXiao.Codeup.Endpoint,
            ["YunXiao.Codeup.Branch"] = settings.YunXiao.Codeup.Branch,
            ["YunXiao.Codeup.ServiceConnection"] = settings.YunXiao.Codeup.ServiceConnection,
            ["YunXiao.MachineGroup"] = settings.YunXiao.MachineGroup
        };

    }


    /// <summary>
    /// 使用指定模板生成单个部署文件
    /// </summary>
    /// <param name="deploymentDirectory">Deployment 目录</param>
    /// <param name="generatedDirectory">生成文件根目录</param>
    /// <param name="templateRelativePath">模板相对路径</param>
    /// <param name="outputRelativePath">输出相对路径</param>
    /// <param name="templateValues">模板占位符值</param>
    private static void GenerateFile(string deploymentDirectory, string generatedDirectory, string templateRelativePath, string outputRelativePath, Dictionary<string, string> templateValues)
    {

        var templatePath = Path.Combine(deploymentDirectory, templateRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(templatePath) == false)
        {
            throw new FileNotFoundException($"未找到模板文件 {templateRelativePath}", templatePath);
        }

        var content = File.ReadAllText(templatePath, Encoding.UTF8);
        foreach (var templateValue in templateValues)
        {
            content = content.Replace($"{{{{{templateValue.Key}}}}}", templateValue.Value, StringComparison.Ordinal);
        }

        if (content.Contains("{{", StringComparison.Ordinal) || content.Contains("}}", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"模板 {templateRelativePath} 中存在未替换的占位符");
        }

        var outputPath = Path.Combine(generatedDirectory, outputRelativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllText(outputPath, content, new UTF8Encoding(false));

    }

}
