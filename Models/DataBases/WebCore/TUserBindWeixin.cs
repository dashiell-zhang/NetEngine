using Models.DataBases.Bases;

namespace Models.DataBases.WebCore
{


    /// <summary>
    /// 用户和微信绑定关系表
    /// </summary>
    public class TUserBindWeixin :CD
    {

        /// <summary>
        /// 用户ID
        /// </summary>
        public string UserId { get; set; }
        public TUser User { get; set; }


        /// <summary>
        /// 关联微信账户
        /// </summary>
        public string WeiXinKeyId { get; set; }
        public TWeiXinKey WeiXinKey { get; set; }



        /// <summary>
        /// 微信OpenId
        /// </summary>
        public string WeiXinOpenId { get; set; }
    }
}
