using Repository.Bases;

namespace Repository.Database
{

    /// <summary>
    /// 用户Token记录表
    /// </summary>
    public class TUserToken : CD
    {


        /// <summary>
        /// 用户ID
        /// </summary>
        public long UserId { get; set; }
        public virtual TUser User { get; set; }



        /// <summary>
        /// 上一次有效的 tokenid
        /// </summary>
        public long? LastId { get; set; }

    }
}
