using Client.Interface.Models.User;
using Shared.Model;

namespace Client.Interface
{
    public interface IUserService
    {

        /// <summary>
        /// 通过 UserId 获取用户信息 
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <returns></returns>
        public DtoUser? GetUser(long? userId);


        /// <summary>
        /// 通过短信验证码修改账户手机号
        /// </summary>
        /// <param name="keyValue">key 为新手机号，value 为短信验证码</param>
        /// <returns></returns>
        public bool EditUserPhoneBySms(DtoKeyValue keyValue);

    }
}
