using AdminShared.Models.User;
using Common;
using Repository.Database;

namespace AdminAPI.Services
{

    [Service(Lifetime = ServiceLifetime.Scoped)]
    public class UserService
    {


        private readonly DatabaseContext db;


        public UserService(DatabaseContext db)
        {
            this.db = db;
        }



        /// <summary>
        /// 获取某个用户某个功能下的子集功能
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <param name="parentId">功能父级ID</param>
        /// <param name="roleIds">用户角色ID集合</param>
        /// <returns></returns>
        public List<DtoUserFunction> GetUserFunctionChildList(long userId, long parentId, List<long> roleIds)
        {

            var functionList = db.TFunction.Where(t => t.IsDelete == false && t.ParentId == parentId && t.Type == TFunction.EnumType.模块).Select(t => new DtoUserFunction
            {
                Id = t.Id,
                Name = t.Name.Replace(t.Parent!.Name + "-", ""),
                Sign = t.Sign,
                IsCheck = db.TFunctionAuthorize.Where(r => r.IsDelete == false && r.FunctionId == t.Id && (roleIds.Contains(r.RoleId!.Value) || r.UserId == userId)).FirstOrDefault() != null,
                FunctionList = db.TFunction.Where(f => f.IsDelete == false && f.ParentId == t.Id && f.Type == TFunction.EnumType.功能).Select(f => new DtoUserFunction
                {
                    Id = f.Id,
                    Name = f.Name.Replace(f.Parent!.Name + "-", ""),
                    Sign = f.Sign,
                    IsCheck = db.TFunctionAuthorize.Where(r => r.IsDelete == false && r.FunctionId == f.Id && (roleIds.Contains(r.RoleId!.Value) || r.UserId == userId)).FirstOrDefault() != null,
                }).ToList()
            }).ToList();

            foreach (var function in functionList)
            {
                function.ChildList = GetUserFunctionChildList(userId, function.Id, roleIds);
            }

            return functionList;
        }

    }
}
