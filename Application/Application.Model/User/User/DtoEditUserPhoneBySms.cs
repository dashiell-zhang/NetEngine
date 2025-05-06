namespace Application.Model.User.User
{
    /// <summary>
    /// 通过短信验证码修改账户手机号入参
    /// </summary>
    public class DtoEditUserPhoneBySms
    {

        /// <summary>
        /// 新手机号
        /// </summary>
        public string NewPhone { get; set; }


        /// <summary>
        /// 短信验证码
        /// </summary>
        public string SmsCode { get; set; }

    }
}
