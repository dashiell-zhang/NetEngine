using Microsoft.EntityFrameworkCore;
using System.Reflection;
using System.Xml.Linq;

namespace Repository.Core.Extensions;

/// <summary>
/// 根据实体及属性声明程序集读取模型文档
/// </summary>
public static class ModelDocumentation
{

    /// <summary>
    /// 应用直接声明实体的模型注释和外键删除约定
    /// </summary>
    public static void ApplyModelDocumentation(this ModelBuilder modelBuilder, DbContext context)
    {

#if DEBUG
        var types = context.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.PropertyType.IsGenericType && property.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
            .Select(property => property.PropertyType.GetGenericArguments()[0]).ToHashSet();
        foreach (var entity in modelBuilder.Model.GetEntityTypes().Where(entity => types.Contains(entity.ClrType)))
        {
            foreach (var foreignKey in entity.GetForeignKeys())
            {
                foreignKey.DeleteBehavior = DeleteBehavior.Restrict;
            }

            modelBuilder.Entity(entity.ClrType).ToTable(table => table.HasComment(GetComment(entity.ClrType)));
            foreach (var property in entity.GetProperties())
            {
                property.SetComment(GetComment(property.PropertyInfo?.DeclaringType ?? entity.ClrType, property.Name));
            }
        }
#endif

    }


    /// <summary>
    /// 读取指定类型或属性的中文说明，缺少文档时使用成员名称
    /// </summary>
    public static string GetComment(Type type, string? propertyName = null)
    {

        var declaringType = type;
        if (propertyName is not null)
        {
            for (var current = type; current is not null; current = current.BaseType)
            {
                if (current.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Any(property => property.Name == propertyName))
                {
                    declaringType = current;
                    break;
                }
            }
        }
        var path = Path.ChangeExtension(declaringType.Assembly.Location, ".xml");
        var fallback = propertyName ?? type.Name;
        if (!File.Exists(path))
        {
            return fallback;
        }

        var memberName = propertyName is null ? $"T:{declaringType.FullName}" : $"P:{declaringType.FullName}.{propertyName}";
        var document = XDocument.Load(path);
        return document.Root?.Element("members")?.Elements("member")
            .FirstOrDefault(member => (string?)member.Attribute("name") == memberName)?.Element("summary")?.Value.Trim() ?? fallback;

    }

}
