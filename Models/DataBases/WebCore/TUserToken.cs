using Models.DataBases.Bases;

namespace Models.DataBases.WebCore
{

    /// <summary>
    /// 用户Token记录表
    /// </summary>
    public class TUserToken : CD
    {

        /// <summary>
        /// 用户ID
        /// </summary>
        public string UserId { get; set; }
        public TUser User { get; set; }



        /// <summary>
        /// Token
        /// </summary>
        public string Token { get; set; }


        /// <summary>
        /// 上一次有效的 tokenid
        /// </summary>
        public string LastId { get; set; }

    }
}
