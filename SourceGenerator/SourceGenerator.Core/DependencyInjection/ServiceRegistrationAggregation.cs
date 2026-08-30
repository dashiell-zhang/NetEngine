using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SourceGenerator.Core.DependencyInjection;

/// <summary>
/// 提供服务注册生成器共用的跨程序集方法发现与聚合代码生成能力
/// </summary>
internal static class ServiceRegistrationAggregation
{

    private const string GeneratedExtensionTypeMetadataName = "NetEngine.Generated.ServiceCollectionExtensions";


    /// <summary>
    /// 判断当前编译是否属于可生成批量注册入口的启动项目
    /// </summary>
    /// <param name="compilation">当前编译</param>
    /// <returns>属于可执行启动项目时返回 true</returns>
    public static bool IsStartupLike(Compilation compilation)
        => compilation.Options.OutputKind is OutputKind.ConsoleApplication
           or OutputKind.WindowsApplication
           or OutputKind.WindowsRuntimeApplication;


    /// <summary>
    /// 查找引用程序集公开的指定服务注册扩展方法
    /// </summary>
    /// <param name="compilation">当前编译</param>
    /// <param name="serviceCollectionSymbol">IServiceCollection 类型符号</param>
    /// <param name="methodPrefix">引用注册方法名称前缀</param>
    /// <returns>按照引用程序集遍历顺序排列的方法名称</returns>
    public static IReadOnlyList<string> FindReferencedRegistrationMethods(Compilation compilation, INamedTypeSymbol serviceCollectionSymbol, string methodPrefix)
    {

        var assemblyName = compilation.AssemblyName ?? "Assembly";
        var methodNames = new List<string>();

        foreach (var reference in compilation.References)
        {
            if (compilation.GetAssemblyOrModuleSymbol(reference) is not IAssemblySymbol assembly)
                continue;

            if (string.Equals(assembly.Name, assemblyName, StringComparison.Ordinal))
                continue;

            var extensionType = assembly.GetTypeByMetadataName(GeneratedExtensionTypeMetadataName);
            if (extensionType is null)
                continue;

            var methodName = methodPrefix + SanitizeIdentifier(assembly.Name);
            var hasMethod = extensionType
                .GetMembers(methodName)
                .OfType<IMethodSymbol>()
                .Any(method => method.IsStatic
                               && method.IsExtensionMethod
                               && method.Parameters.Length == 1
                               && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, serviceCollectionSymbol));

            if (hasMethod)
            {
                methodNames.Add(methodName);
            }
        }

        return methodNames;

    }


    /// <summary>
    /// 生成依次调用各程序集注册方法的批量注册扩展方法
    /// </summary>
    /// <param name="builder">生成代码构建器</param>
    /// <param name="batchMethodName">批量注册方法名称</param>
    /// <param name="methodNames">按调用顺序排列的注册方法名称</param>
    public static void AppendBatchMethod(StringBuilder builder, string batchMethodName, IReadOnlyList<string> methodNames)
    {

        builder.AppendLine();
        builder.Append("    public static IServiceCollection ").Append(batchMethodName).AppendLine("(this IServiceCollection services)");
        builder.AppendLine("    {");

        foreach (var methodName in methodNames)
        {
            builder.Append("        services.").Append(methodName).AppendLine("();");
        }

        builder.AppendLine("        return services;");
        builder.AppendLine("    }");

    }


    /// <summary>
    /// 将任意字符串转换为合法的 C# 标识符
    /// </summary>
    /// <param name="name">原始名称</param>
    /// <returns>可作为标识符使用的安全名称</returns>
    public static string SanitizeIdentifier(string name)
    {

        if (string.IsNullOrEmpty(name))
            return "_";

        var builder = new StringBuilder(name.Length);

        if (!SyntaxFacts.IsIdentifierStartCharacter(name[0]))
        {
            builder.Append('_');
        }

        foreach (var character in name)
        {
            builder.Append(SyntaxFacts.IsIdentifierPartCharacter(character) ? character : '_');
        }

        return builder.ToString();

    }

}
