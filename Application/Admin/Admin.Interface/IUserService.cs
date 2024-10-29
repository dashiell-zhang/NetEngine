using Admin.Model.User;
using Shared.Model;

namespace Admin.Interface
{
    public interface IUserService
    {

        /// <summary>
        /// 获取用户列表
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public DtoPageList<DtoUser> GetUserList(DtoPageRequest request);



        /// <summary>
        /// 通过 UserId 获取用户信息 
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <returns></returns>
        public DtoUser? GetUser(long? userId);



        /// <summary>
        /// 创建用户
        /// </summary>
        /// <param name="createUser"></param>
        /// <returns></returns>
        public long? CreateUser(DtoEditUser createUser);



        /// <summary>
        /// 更新用户信息
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="updateUser"></param>
        /// <returns></returns>
        public bool UpdateUser(long userId, DtoEditUser updateUser);


        /// <summary>
        /// 删除用户
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public bool DeleteUser(long id);



        /// <summary>
        /// 获取某个用户的功能权限
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <returns></returns>
        public List<DtoUserFunction> GetUserFunction(long userId);



        /// <summary>
        /// 设置用户的功能
        /// </summary>
        /// <param name="setUserFunction"></param>
        /// <returns></returns>
        public bool SetUserFunction(DtoSetUserFunction setUserFunction);



        /// <summary>
        /// 获取用户角色列表
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public List<DtoUserRole> GetUserRoleList(long userId);



        /// <summary>
        /// 设置用户角色
        /// </summary>
        /// <param name="setUserRole"></param>
        /// <returns></returns>
        public bool SetUserRole(DtoSetUserRole setUserRole);



        /// <summary>
        /// 获取某个用户某个功能下的子集功能
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <param name="parentId">功能父级ID</param>
        /// <param name="roleIds">用户角色ID集合</param>
        /// <returns></returns>
        public List<DtoUserFunction> GetUserFunctionChildList(long userId, long parentId, List<long> roleIds);
    }
}
