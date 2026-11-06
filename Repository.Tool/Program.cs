using Microsoft.EntityFrameworkCore;
using Repository.Tool.Configuration;
using Repository.Tool.Scripts;

/// <summary>
/// 显式选择数据库模型并执行一次性只读工具操作
/// </summary>
internal static class Program
{

    /// <summary>
    /// 无参数显示帮助，不构造宿主或连接数据库
    /// </summary>
    private static async Task<int> Main(string[] args)
    {

        if (args.Length == 0 || args.Contains("--help"))
        {
            Console.WriteLine("Repository.Tool json-index --target <Business|Log|EventBus>");
            Console.WriteLine("只读数据库并输出 JSON 索引差异 SQL，不执行结构变更");
            Console.WriteLine("连接配置读取工具目录的 appsettings.json");
            Console.WriteLine("迁移使用 Add-Migration -Context 或 dotnet ef --context 明确选择上下文；只在本机 Debug 执行迁移");
            return 0;
        }

        try
        {
            if (args.Length != 3 || args[0] != "json-index" || args[1] != "--target")
            {

                throw new ArgumentException("用法：Repository.Tool json-index --target <Business|Log|EventBus>");

            }
            await using var db = ToolDatabaseOptions.Create(args[2]);
            var connection = db.Database.GetDbConnection();
            Console.Error.WriteLine($"目标模型={db.GetType().FullName}，服务器={connection.DataSource}，数据库={connection.Database}");
            Console.WriteLine(await JsonIndexScript.CreateAsync(db));
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"数据库工具操作失败：{exception.GetType().Name}：{exception.Message}");
            return 1;
        }

    }

}
