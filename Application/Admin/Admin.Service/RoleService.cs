using Admin.Interface;
using Admin.Model.Role;
using Common;
using IdentifierGenerator;
using Microsoft.Extensions.DependencyInjection;
using Repository.Database;
using Shared.Interface;
using Shared.Model;

namespace Admin.Service
{
    [Service(Lifetime = ServiceLifetime.Scoped)]
    public class RoleService(DatabaseContext db, IdService idService, IUserContext userContext) : IRoleService
    {


        private long userId => userContext.UserId;



        public DtoPageList<DtoRole> GetRoleList(DtoPageRequest request)
        {
            var retList = new DtoPageList<DtoRole>();

            var query = db.TRole.AsQueryable();

            retList.Total = query.Count();

            retList.List = query.OrderBy(t => t.CreateTime).Select(t => new DtoRole
            {
                Id = t.Id,
                CreateTime = t.CreateTime,
                Name = t.Name,
                Remarks = t.Remarks
            }).Skip(request.Skip()).Take(request.PageSize).ToList();

            return retList;
        }




        public DtoRole? GetRole(long roleId)
        {

            var role = db.TRole.Where(t => t.Id == roleId).Select(t => new DtoRole
            {
                Id = t.Id,
                CreateTime = t.CreateTime,
                Name = t.Name,
                Remarks = t.Remarks
            }).FirstOrDefault();

            return role;
        }



        public long CreateRole(DtoEditRole role)
        {
            var dbRole = new TRole
            {
                Id = idService.GetId(),
                Name = role.Name,
                Remarks = role.Remarks
            };

            db.TRole.Add(dbRole);

            db.SaveChanges();

            return dbRole.Id;
        }



        public bool UpdateRole(long roleId, DtoEditRole role)
        {
            var dbRole = db.TRole.Where(t => t.Id == roleId).FirstOrDefault();

            if (dbRole != null)
            {
                dbRole.Name = role.Name;
                dbRole.Remarks = role.Remarks;

                db.SaveChanges();

                return true;
            }
            else
            {
                return false;
            }
        }



        public bool DeleteRole(long id)
        {

            var role = db.TRole.Where(t => t.Id == id).FirstOrDefault();

            var isHaveUser = db.TUserRole.Where(t => t.RoleId == id).FirstOrDefault();

            if (isHaveUser != null)
            {
                throw new CustomException("当前角色下存在人员信息，无法删除！");
            }

            if (role != null)
            {
                role.IsDelete = true;

                db.SaveChanges();

                return true;
            }
            else
            {
                throw new CustomException("角色不存在或已被删除！");
            }
        }




        public List<DtoRoleFunction> GetRoleFunction(long roleId)
        {
            var functionList = db.TFunction.Where(t => t.ParentId == null && t.Type == TFunction.EnumType.模块).Select(t => new DtoRoleFunction
            {
                Id = t.Id,
                Name = t.Name.Replace(t.Parent!.Name + "-", ""),
                Sign = t.Sign,
                IsCheck = db.TFunctionAuthorize.Where(r => r.FunctionId == t.Id && r.RoleId == roleId).FirstOrDefault() != null,
                FunctionList = db.TFunction.Where(f => f.ParentId == t.Id && f.Type == TFunction.EnumType.功能).Select(f => new DtoRoleFunction
                {
                    Id = f.Id,
                    Name = f.Name.Replace(f.Parent!.Name + "-", ""),
                    Sign = f.Sign,
                    IsCheck = db.TFunctionAuthorize.Where(r => r.FunctionId == f.Id && r.RoleId == roleId).FirstOrDefault() != null,
                }).ToList()
            }).ToList();

            foreach (var function in functionList)
            {
                function.ChildList = GetRoleFunctionChildList(roleId, function.Id);
            }

            return functionList;
        }




        public bool SetRoleFunction(DtoSetRoleFunction setRoleFunction)
        {

            var functionAuthorize = db.TFunctionAuthorize.Where(t => t.RoleId == setRoleFunction.RoleId && t.FunctionId == setRoleFunction.FunctionId).FirstOrDefault() ?? new TFunctionAuthorize();

            functionAuthorize.FunctionId = setRoleFunction.FunctionId;
            functionAuthorize.RoleId = setRoleFunction.RoleId;

            if (setRoleFunction.IsCheck)
            {
                if (functionAuthorize.Id == default)
                {
                    functionAuthorize.Id = idService.GetId();
                    functionAuthorize.CreateUserId = userId;

                    db.TFunctionAuthorize.Add(functionAuthorize);
                }
            }
            else
            {
                if (functionAuthorize.Id != default)
                {
                    functionAuthorize.IsDelete = true;
                    functionAuthorize.DeleteUserId = userId;
                }
            }

            db.SaveChanges();

            return true;
        }



        public List<DtoKeyValue> GetRoleKV()
        {
            var list = db.TRole.Select(t => new DtoKeyValue
            {
                Key = t.Id,
                Value = t.Name
            }).ToList();

            return list;
        }



        public List<DtoRoleFunction> GetRoleFunctionChildList(long roleId, long parentId)
        {

            var functionList = db.TFunction.Where(t => t.ParentId == parentId && t.Type == TFunction.EnumType.模块).Select(t => new DtoRoleFunction
            {
                Id = t.Id,
                Name = t.Name.Replace(t.Parent!.Name + "-", ""),
                Sign = t.Sign,
                IsCheck = db.TFunctionAuthorize.Where(r => r.FunctionId == t.Id && r.RoleId == roleId).FirstOrDefault() != null,
                FunctionList = db.TFunction.Where(f => f.ParentId == t.Id && f.Type == TFunction.EnumType.功能).Select(f => new DtoRoleFunction
                {
                    Id = f.Id,
                    Name = f.Name.Replace(f.Parent!.Name + "-", ""),
                    Sign = f.Sign,
                    IsCheck = db.TFunctionAuthorize.Where(r => r.FunctionId == f.Id && r.RoleId == roleId).FirstOrDefault() != null,
                }).ToList()
            }).ToList();

            foreach (var function in functionList)
            {
                function.ChildList = GetRoleFunctionChildList(roleId, function.Id);
            }

            return functionList;

        }

    }
}
