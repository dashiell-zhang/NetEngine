namespace Shared.Model.AppSetting
{


    /// <summary>
    /// JWT 配置信息
    /// </summary>
    public class JWTSetting
    {


        /// <summary>
        /// token是谁颁发的
        /// </summary>
        public string Issuer { get; set; }



        /// <summary>
        /// token可以给哪些客户端使用
        /// </summary>
        public string Audience { get; set; }



        /// <summary>
        /// 私钥
        /// </summary>
        public string PrivateKey { get; set; }



        /// <summary>
        /// 公钥
        /// </summary>
        public string PublicKey { get; set; }



        /// <summary>
        /// 失效时长
        /// </summary>
        public TimeSpan Expiry { get; set; }


    }
}
