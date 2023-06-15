using Repository.Bases;

namespace Repository.Database
{
    public class TUserBindExternal : CD
    {



        /// <summary>
        /// 用户信息
        /// </summary>
        public long UserId { get; set; }
        public virtual TUser User { get; set; }



        /// <summary>
        /// APP名称
        /// </summary>
        public string APPName { get; set; }




        /// <summary>
        /// APPId
        /// </summary>
        public string APPId { get; set; }



        /// <summary>
        /// 用户绑定ID
        /// </summary>
        public string OpenId { get; set; }



    }
}
