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

            var functionList = db.TFunction.Where(t => t.IsDelete == false && t.ParentId == parentId).Select(t => new DtoUserFunction
            {
                Id = t.Id,
                Name = t.Name.Replace(t.Parent!.Name + "-", ""),
                Type = t.Type.ToString(),
                Sign = t.Sign,
                IsCheck = db.TFunctionAuthorize.Where(r => r.IsDelete == false && r.FunctionId == t.Id && (roleIds.Contains(r.RoleId!.Value) || r.UserId == userId)).FirstOrDefault() != null,
            }).ToList();

            foreach (var function in functionList)
            {
                function.ChildList = GetUserFunctionChildList(userId, function.Id, roleIds);
            }

            return functionList;
        }

    }
}
