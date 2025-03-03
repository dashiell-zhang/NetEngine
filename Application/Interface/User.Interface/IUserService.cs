using Shared.Model;
using User.Model.User;

namespace User.Interface
{
    public interface IUserService
    {

        /// <summary>
        /// 通过 UserId 获取用户信息 
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <returns></returns>
        Task<DtoUser?> GetUserAsync(long? userId);


        /// <summary>
        /// 通过短信验证码修改账户手机号
        /// </summary>
        /// <param name="keyValue">key 为新手机号，value 为短信验证码</param>
        /// <returns></returns>
        Task<bool> EditUserPhoneBySmsAsync(DtoEditUserPhoneBySms request);


        /// <summary>
        /// 获取用户列表
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        Task<DtoPageList<DtoUser>> GetUserListAsync(DtoPageRequest request);


        /// <summary>
        /// 创建用户
        /// </summary>
        /// <param name="createUser"></param>
        /// <returns></returns>
        Task<long?> CreateUserAsync(DtoEditUser createUser);


        /// <summary>
        /// 更新用户信息
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="updateUser"></param>
        /// <returns></returns>
        Task<bool> UpdateUserAsync(long userId, DtoEditUser updateUser);


        /// <summary>
        /// 删除用户
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        Task<bool> DeleteUserAsync(long id);


        /// <summary>
        /// 获取某个用户的功能权限
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <returns></returns>
        Task<List<DtoUserFunction>> GetUserFunctionAsync(long userId);


        /// <summary>
        /// 设置用户的功能
        /// </summary>
        /// <param name="setUserFunction"></param>
        /// <returns></returns>
        Task<bool> SetUserFunctionAsync(DtoSetUserFunction setUserFunction);


        /// <summary>
        /// 获取用户角色列表
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        Task<List<DtoUserRole>> GetUserRoleListAsync(long userId);


        /// <summary>
        /// 设置用户角色
        /// </summary>
        /// <param name="setUserRole"></param>
        /// <returns></returns>
        Task<bool> SetUserRoleAsync(DtoSetUserRole setUserRole);


        /// <summary>
        /// 获取某个用户某个功能下的子集功能
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <param name="parentId">功能父级ID</param>
        /// <param name="roleIds">用户角色ID集合</param>
        /// <returns></returns>
        Task<List<DtoUserFunction>> GetUserFunctionChildListAsync(long userId, long parentId, List<long> roleIds);

    }
}
