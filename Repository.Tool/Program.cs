using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Repository.Tool.Scripts;

/// <summary>
/// 显式选择数据库模型并执行一次性只读工具操作
/// </summary>
internal static class Program
{

    /// <summary>
    /// 交互选择数据库上下文并输出 JSON 索引差异 SQL
    /// </summary>
    private static async Task<int> Main()
    {

        try
        {
            await using var db = SelectContext();
            if (db is null)
            {
                return 0;
            }

            var connection = db.Database.GetDbConnection();
            Console.Error.WriteLine($"目标模型={db.GetType().FullName}，服务器={connection.DataSource}，数据库={connection.Database}");
            var sql = await JsonIndexScript.CreateAsync(db);
            Console.WriteLine(sql.Length == 0 ? "没有 JSON 索引差异" : sql);
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"数据库工具操作失败：{exception.GetType().Name}：{exception.Message}");
            return 1;
        }

    }


    /// <summary>
    /// 扫描设计时工厂并按输入的序号创建对应上下文
    /// </summary>
    private static DbContext? SelectContext()
    {

        var factories = typeof(Program).Assembly.GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract && !type.ContainsGenericParameters)
            .SelectMany(type => type.GetInterfaces()
                .Where(contract => contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IDesignTimeDbContextFactory<>))
                .Select(contract => (
                    FactoryType: type,
                    Contract: contract,
                    ContextType: contract.GenericTypeArguments[0],
                    Display: type.GetCustomAttribute<DisplayAttribute>())))
            .OrderBy(factory => factory.Display?.GetOrder() ?? int.MaxValue)
            .ThenBy(factory => factory.ContextType.FullName, StringComparer.Ordinal)
            .ToArray();

        if (factories.Length == 0)
        {
            throw new InvalidOperationException("Repository.Tool 中没有 EF Core 设计时工厂");
        }

        Console.WriteLine("请选择要检查 JSON 索引的数据库上下文");
        for (var index = 0; index < factories.Length; index++)
        {
            var factory = factories[index];
            Console.WriteLine($"{index + 1}. {factory.Display?.GetName() ?? factory.ContextType.FullName}");
        }

        while (true)
        {
            Console.Write("请输入序号：");
            var input = Console.ReadLine();
            if (input is null)
            {
                return null;
            }

            if (int.TryParse(input, out var number) && number > 0 && number <= factories.Length)
            {
                var factory = factories[number - 1];
                var instance = Activator.CreateInstance(factory.FactoryType);
                var createContext = factory.Contract.GetMethod(nameof(IDesignTimeDbContextFactory<DbContext>.CreateDbContext))!
                    .CreateDelegate<Func<string[], DbContext>>(instance);
                return createContext([]);
            }

            Console.WriteLine($"请输入 1 到 {factories.Length} 之间的序号");
        }

    }

}
