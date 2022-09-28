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

            var functionList = db.TFunction.Where(t => t.IsDelete == false && t.ParentId == parentId).Select(t => new DtoRoleFunction
            {
                Id = t.Id,
                Name = t.Name.Replace(t.Parent!.Name + "-", ""),
                Type = t.Type.ToString(),
                Sign = t.Sign,
                IsCheck = db.TFunctionAuthorize.Where(r => r.IsDelete == false && r.FunctionId == t.Id && r.RoleId == roleId).FirstOrDefault() != null,
            }).ToList();

            foreach (var function in functionList)
            {
                function.ChildList = GetRoleFunctionChildList(roleId, function.Id);
            }

            return functionList;

        }

    }
}
