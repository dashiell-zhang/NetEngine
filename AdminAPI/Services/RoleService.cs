using AdminShared.Models.Role;
using Common;
using Repository.Database;

namespace AdminAPI.Services
{

    [Service(Lifetime = ServiceLifetime.Scoped)]
    public class RoleService
    {


        private readonly DatabaseContext db;


        public RoleService(DatabaseContext db)
        {
            this.db = db;
        }



        /// <summary>
        /// 获取某个角色某个功能下的子集功能
        /// </summary>
        /// <param name="roleId">角色ID</param>
        /// <param name="parentId">功能父级ID</param>
        /// <returns></returns>
        public List<DtoRoleFunction> GetRoleFunctionChildList(long roleId, long parentId)
        {

            var functionList = db.TFunction.Where(t => t.ParentId == parentId && t.Type == TFunction.EnumType.模块).Select(t => new DtoRoleFunction
            {
                Id = t.Id,
                Name = t.Name.Replace(t.Parent!.Name + "-", ""),
                Sign = t.Sign,
                IsCheck = db.TFunctionAuthorize.Where(r =>  r.FunctionId == t.Id && r.RoleId == roleId).FirstOrDefault() != null,
                FunctionList = db.TFunction.Where(f =>  f.ParentId == t.Id && f.Type == TFunction.EnumType.功能).Select(f => new DtoRoleFunction
                {
                    Id = f.Id,
                    Name = f.Name.Replace(f.Parent!.Name + "-", ""),
                    Sign = f.Sign,
                    IsCheck = db.TFunctionAuthorize.Where(r =>  r.FunctionId == f.Id && r.RoleId == roleId).FirstOrDefault() != null,
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
