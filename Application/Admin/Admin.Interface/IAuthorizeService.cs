using Admin.Model.Authorize;

namespace Admin.Interface
{
    public interface IAuthorizeService
    {

        /// <summary>
        /// 获取Token认证信息
        /// </summary>
        /// <param name="login">登录信息集合</param>
        /// <returns></returns>
        public string? GetToken(DtoLogin login);



        /// <summary>
        /// 获取授权功能列表
        /// </summary>
        /// <returns></returns>
        public List<string> GetFunctionList();



        /// <summary>
        /// 获取Token通过UserId
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public string GetTokenByUserId(long userId);


    }
}
