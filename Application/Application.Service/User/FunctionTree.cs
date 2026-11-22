using Microsoft.EntityFrameworkCore;
using Repository;
using Repository.Database.Enums;

namespace Application.Service.User;

/// <summary>
/// 根据有效功能和授权投影组装树形权限结果
/// </summary>
internal static class FunctionTree
{

    /// <summary>
    /// 批量读取节点和勾选项，保持模块与叶子的返回结构
    /// </summary>
    internal static async Task<List<T>> LoadAsync<T>(DatabaseContext db, long? parentId, IQueryable<long> grants, Func<long, string, string, bool, T> create, Action<T, List<T>, List<T>> setCollections)
    {

        var rows = await db.Function.Where(t => t.Type == FunctionType.Module || t.Type == FunctionType.Function)
            .OrderBy(t => t.Id)
            .Select(t => new { t.Id, t.ParentId, t.Type, Name = t.Name.Replace(t.Parent!.Name + "-", ""), t.Sign })
            .ToListAsync();
        var modules = rows.Where(t => t.Type == FunctionType.Module).ToLookup(t => t.ParentId);
        if (!modules.Contains(parentId))
        {
            return [];
        }

        var functions = rows.Where(t => t.Type == FunctionType.Function).ToLookup(t => t.ParentId);
        var checkedIds = (await grants.Distinct().ToListAsync()).ToHashSet();

        return BuildChildren(parentId);

        List<T> BuildChildren(long? parent)
        {

            var result = new List<T>();
            foreach (var module in modules[parent])
            {
                var node = create(module.Id, module.Name, module.Sign, checkedIds.Contains(module.Id));
                var leaves = functions[module.Id]
                    .Select(t => create(t.Id, t.Name, t.Sign, checkedIds.Contains(t.Id)))
                    .ToList();
                setCollections(node, BuildChildren(module.Id), leaves);
                result.Add(node);
            }

            return result;

        }

    }

}
