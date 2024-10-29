namespace Shared.Model.Authorize
{
    public class DtoGetTokenBySMS
    {

        /// <summary>
        /// 手机号
        /// </summary>
        public string Phone { get; set; }



        /// <summary>
        /// 验证码
        /// </summary>
        public string VerifyCode { get; set; }
    }
}
