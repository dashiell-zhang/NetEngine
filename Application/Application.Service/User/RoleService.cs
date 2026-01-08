using Application.Interface;
using Application.Model.Shared;
using Application.Model.User.Role;
using Common;
using DistributedLock;
using IdentifierGenerator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Repository.Database;
using Repository.Enum;
using SourceGenerator.Runtime.Attributes;

namespace Application.Service.User;
[RegisterService(Lifetime = ServiceLifetime.Scoped)]
public class RoleService(DatabaseContext db, IdService idService, IUserContext userContext, IDistributedLock distLock)
{

    private long UserId => userContext.UserId;


    /// 获取角色列表
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    public async Task<DtoPageList<DtoRole>> GetRoleListAsync(DtoPageRequest request)
    {
        DtoPageList<DtoRole> result = new();

        var query = db.Role.AsQueryable();

        result.Total = await query.CountAsync();

        if (result.Total != 0)
        {
            result.List = await query.OrderByDescending(t => t.Id).Select(t => new DtoRole
            {
                Id = t.Id,
                Code = t.Code,
                Name = t.Name,
                Remarks = t.Remarks,
                CreateTime = t.CreateTime
            }).Skip(request.Skip()).Take(request.PageSize).ToListAsync();
        }

        return result;
    }


    /// <summary>
    /// 通过ID获取角色信息
    /// </summary>
    /// <param name="roleId">角色ID</param>
    /// <returns></returns>
    public Task<DtoRole?> GetRoleAsync(long roleId)
    {
        var role = db.Role.Where(t => t.Id == roleId).Select(t => new DtoRole
        {
            Id = t.Id,
            Code = t.Code,
            Name = t.Name,
            Remarks = t.Remarks,
            CreateTime = t.CreateTime
        }).FirstOrDefaultAsync();

        return role;
    }


    /// <summary>
    /// 创建角色
    /// </summary>
    /// <param name="role"></param>
    /// <returns></returns>
    public async Task<long> CreateRoleAsync(DtoEditRole role)
    {
        using (var lockHandle = await distLock.TryLockAsync("roleCode" + role.Code))
        {
            if (lockHandle != null)
            {
                var isHaveRoleCode = await db.Role.Where(t => t.Code == role.Code).AnyAsync();

                if (!isHaveRoleCode)
                {
                    var dbRole = new Role
                    {
                        Id = idService.GetId(),
                        Code = role.Code,
                        Name = role.Name,
                        Remarks = role.Remarks
                    };

                    db.Role.Add(dbRole);

                    await db.SaveChangesAsync();

                    return dbRole.Id;
                }
            }
        }

        throw new CustomException("角色编码已被占用");
    }


    /// <summary>
    /// 编辑角色
    /// </summary>
    /// <param name="roleId"></param>
    /// <param name="role"></param>
    /// <returns></returns>
    public async Task<bool> UpdateRoleAsync(long roleId, DtoEditRole role)
    {
        using (var lockHandle = await distLock.TryLockAsync("roleCode" + role.Code))
        {
            if (lockHandle != null)
            {
                var isHaveRoleCode = await db.Role.Where(t => t.Id != roleId && t.Code == role.Code).AnyAsync();

                if (!isHaveRoleCode)
                {
                    var dbRole = await db.Role.Where(t => t.Id == roleId).FirstOrDefaultAsync();

                    if (dbRole != null)
                    {
                        dbRole.Code = role.Code;
                        dbRole.Name = role.Name;
                        dbRole.Remarks = role.Remarks;

                        await db.SaveChangesAsync();

                        return true;
                    }
                    else
                    {
                        throw new CustomException("角色不存在无法编辑");
                    }
                }
            }
        }

        throw new CustomException("角色编码已被占用");
    }


    /// <summary>
    /// 删除角色
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public async Task<bool> DeleteRoleAsync(long id)
    {
        var isHaveUser = await db.UserRole.Where(t => t.RoleId == id).FirstOrDefaultAsync();

        if (isHaveUser != null)
        {
            throw new CustomException("当前角色下存在人员信息，无法删除！");
        }

        var role = await db.Role.Where(t => t.Id == id).FirstOrDefaultAsync();

        if (role != null)
        {
            role.IsDelete = true;
            await db.SaveChangesAsync();
        }

        return true;
    }


    /// <summary>
    /// 获取某个角色的功能权限
    /// </summary>
    /// <param name="roleId">角色ID</param>
    /// <returns></returns>
    public async Task<List<DtoRoleFunction>> GetRoleFunctionAsync(long roleId)
    {
        var functionList = await db.Function.Where(t => t.ParentId == null && t.Type == EnumFunctionType.Module).Select(t => new DtoRoleFunction
        {
            Id = t.Id,
            Name = t.Name.Replace(t.Parent!.Name + "-", ""),
            Sign = t.Sign,
            IsCheck = db.FunctionAuthorize.Where(r => r.FunctionId == t.Id && r.RoleId == roleId).FirstOrDefault() != null,
            FunctionList = db.Function.Where(f => f.ParentId == t.Id && f.Type == EnumFunctionType.Function).Select(f => new DtoRoleFunction
            {
                Id = f.Id,
                Name = f.Name.Replace(f.Parent!.Name + "-", ""),
                Sign = f.Sign,
                IsCheck = db.FunctionAuthorize.Where(r => r.FunctionId == f.Id && r.RoleId == roleId).FirstOrDefault() != null,
            }).ToList()
        }).ToListAsync();

        foreach (var function in functionList)
        {
            function.ChildList = await GetRoleFunctionChildListAsync(roleId, function.Id);
        }

        return functionList;
    }


    /// <summary>
    /// 设置角色的功能
    /// </summary>
    /// <param name="setRoleFunction"></param>
    /// <returns></returns>
    public async Task<bool> SetRoleFunctionAsync(DtoSetRoleFunction setRoleFunction)
    {

        var functionAuthorize = await db.FunctionAuthorize.Where(t => t.RoleId == setRoleFunction.RoleId && t.FunctionId == setRoleFunction.FunctionId).FirstOrDefaultAsync() ?? new FunctionAuthorize();

        functionAuthorize.FunctionId = setRoleFunction.FunctionId;
        functionAuthorize.RoleId = setRoleFunction.RoleId;

        if (setRoleFunction.IsCheck)
        {
            if (functionAuthorize.Id == default)
            {
                functionAuthorize.Id = idService.GetId();
                functionAuthorize.CreateUserId = UserId;

                db.FunctionAuthorize.Add(functionAuthorize);
            }
        }
        else
        {
            if (functionAuthorize.Id != default)
            {
                functionAuthorize.IsDelete = true;
                functionAuthorize.DeleteUserId = UserId;
            }
        }

        await db.SaveChangesAsync();

        return true;
    }


    /// <summary>
    /// 获取角色键值对
    /// </summary>
    /// <returns></returns>
    public Task<List<DtoKeyValue>> GetRoleKVAsync()
    {
        var list = db.Role.Select(t => new DtoKeyValue
        {
            Key = t.Id,
            Value = t.Name
        }).ToListAsync();

        return list;
    }


    /// <summary>
    /// 获取某个角色某个功能下的子集功能
    /// </summary>
    /// <param name="roleId">角色ID</param>
    /// <param name="parentId">功能父级ID</param>
    /// <returns></returns>
    public async Task<List<DtoRoleFunction>> GetRoleFunctionChildListAsync(long roleId, long parentId)
    {

        var functionList = await db.Function.Where(t => t.ParentId == parentId && t.Type == EnumFunctionType.Module).Select(t => new DtoRoleFunction
        {
            Id = t.Id,
            Name = t.Name.Replace(t.Parent!.Name + "-", ""),
            Sign = t.Sign,
            IsCheck = db.FunctionAuthorize.Where(r => r.FunctionId == t.Id && r.RoleId == roleId).FirstOrDefault() != null,
            FunctionList = db.Function.Where(f => f.ParentId == t.Id && f.Type == EnumFunctionType.Function).Select(f => new DtoRoleFunction
            {
                Id = f.Id,
                Name = f.Name.Replace(f.Parent!.Name + "-", ""),
                Sign = f.Sign,
                IsCheck = db.FunctionAuthorize.Where(r => r.FunctionId == f.Id && r.RoleId == roleId).FirstOrDefault() != null,
            }).ToList()
        }).ToListAsync();

        foreach (var function in functionList)
        {
            function.ChildList = await GetRoleFunctionChildListAsync(roleId, function.Id);
        }

        return functionList;

    }

}
