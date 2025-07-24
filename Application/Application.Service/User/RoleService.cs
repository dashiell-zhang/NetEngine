using Application.Interface.Authorize;
using Application.Interface.User;
using Application.Model.Shared;
using Application.Model.User.Role;
using Common;
using DistributedLock;
using IdentifierGenerator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Repository.Database;
using Repository.Enum;

namespace Application.Service.User
{
    [Service(Lifetime = ServiceLifetime.Scoped)]
    public class RoleService(DatabaseContext db, IdService idService, IUserContext userContext, IDistributedLock distLock) : IRoleService
    {

        private long UserId => userContext.UserId;


        public async Task<DtoPageList<DtoRole>> GetRoleListAsync(DtoPageRequest request)
        {
            DtoPageList<DtoRole> result = new();

            var query = db.TRole.AsQueryable();

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


        public Task<DtoRole?> GetRoleAsync(long roleId)
        {
            var role = db.TRole.Where(t => t.Id == roleId).Select(t => new DtoRole
            {
                Id = t.Id,
                Code = t.Code,
                Name = t.Name,
                Remarks = t.Remarks,
                CreateTime = t.CreateTime
            }).FirstOrDefaultAsync();

            return role;
        }


        public async Task<long> CreateRoleAsync(DtoEditRole role)
        {
            using (var lockHandle = await distLock.TryLockAsync("roleCode" + role.Code))
            {
                if (lockHandle != null)
                {
                    var isHaveRoleCode = await db.TRole.Where(t => t.Code == role.Code).AnyAsync();

                    if (!isHaveRoleCode)
                    {
                        var dbRole = new TRole
                        {
                            Id = idService.GetId(),
                            Code = role.Code,
                            Name = role.Name,
                            Remarks = role.Remarks
                        };

                        db.TRole.Add(dbRole);

                        await db.SaveChangesAsync();

                        return dbRole.Id;
                    }
                }
            }

            throw new CustomException("角色编码已被占用");
        }


        public async Task<bool> UpdateRoleAsync(long roleId, DtoEditRole role)
        {
            using (var lockHandle = await distLock.TryLockAsync("roleCode" + role.Code))
            {
                if (lockHandle != null)
                {
                    var isHaveRoleCode = await db.TRole.Where(t => t.Id != roleId && t.Code == role.Code).AnyAsync();

                    if (!isHaveRoleCode)
                    {
                        var dbRole = await db.TRole.Where(t => t.Id == roleId).FirstOrDefaultAsync();

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


        public async Task<bool> DeleteRoleAsync(long id)
        {
            var isHaveUser = await db.TUserRole.Where(t => t.RoleId == id).FirstOrDefaultAsync();

            if (isHaveUser != null)
            {
                throw new CustomException("当前角色下存在人员信息，无法删除！");
            }

            var role = await db.TRole.Where(t => t.Id == id).FirstOrDefaultAsync();

            if (role != null)
            {
                role.IsDelete = true;
                await db.SaveChangesAsync();
            }

            return true;
        }


        public async Task<List<DtoRoleFunction>> GetRoleFunctionAsync(long roleId)
        {
            var functionList = await db.TFunction.Where(t => t.ParentId == null && t.Type == EnumFunctionType.Module).Select(t => new DtoRoleFunction
            {
                Id = t.Id,
                Name = t.Name.Replace(t.Parent!.Name + "-", ""),
                Sign = t.Sign,
                IsCheck = db.TFunctionAuthorize.Where(r => r.FunctionId == t.Id && r.RoleId == roleId).FirstOrDefault() != null,
                FunctionList = db.TFunction.Where(f => f.ParentId == t.Id && f.Type == EnumFunctionType.Function).Select(f => new DtoRoleFunction
                {
                    Id = f.Id,
                    Name = f.Name.Replace(f.Parent!.Name + "-", ""),
                    Sign = f.Sign,
                    IsCheck = db.TFunctionAuthorize.Where(r => r.FunctionId == f.Id && r.RoleId == roleId).FirstOrDefault() != null,
                }).ToList()
            }).ToListAsync();

            foreach (var function in functionList)
            {
                function.ChildList = await GetRoleFunctionChildListAsync(roleId, function.Id);
            }

            return functionList;
        }


        public async Task<bool> SetRoleFunctionAsync(DtoSetRoleFunction setRoleFunction)
        {

            var functionAuthorize = await db.TFunctionAuthorize.Where(t => t.RoleId == setRoleFunction.RoleId && t.FunctionId == setRoleFunction.FunctionId).FirstOrDefaultAsync() ?? new TFunctionAuthorize();

            functionAuthorize.FunctionId = setRoleFunction.FunctionId;
            functionAuthorize.RoleId = setRoleFunction.RoleId;

            if (setRoleFunction.IsCheck)
            {
                if (functionAuthorize.Id == default)
                {
                    functionAuthorize.Id = idService.GetId();
                    functionAuthorize.CreateUserId = UserId;

                    db.TFunctionAuthorize.Add(functionAuthorize);
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


        public Task<List<DtoKeyValue>> GetRoleKVAsync()
        {
            var list = db.TRole.Select(t => new DtoKeyValue
            {
                Key = t.Id,
                Value = t.Name
            }).ToListAsync();

            return list;
        }


        public async Task<List<DtoRoleFunction>> GetRoleFunctionChildListAsync(long roleId, long parentId)
        {

            var functionList = await db.TFunction.Where(t => t.ParentId == parentId && t.Type == EnumFunctionType.Module).Select(t => new DtoRoleFunction
            {
                Id = t.Id,
                Name = t.Name.Replace(t.Parent!.Name + "-", ""),
                Sign = t.Sign,
                IsCheck = db.TFunctionAuthorize.Where(r => r.FunctionId == t.Id && r.RoleId == roleId).FirstOrDefault() != null,
                FunctionList = db.TFunction.Where(f => f.ParentId == t.Id && f.Type == EnumFunctionType.Function).Select(f => new DtoRoleFunction
                {
                    Id = f.Id,
                    Name = f.Name.Replace(f.Parent!.Name + "-", ""),
                    Sign = f.Sign,
                    IsCheck = db.TFunctionAuthorize.Where(r => r.FunctionId == f.Id && r.RoleId == roleId).FirstOrDefault() != null,
                }).ToList()
            }).ToListAsync();

            foreach (var function in functionList)
            {
                function.ChildList = await GetRoleFunctionChildListAsync(roleId, function.Id);
            }

            return functionList;

        }

    }
}
