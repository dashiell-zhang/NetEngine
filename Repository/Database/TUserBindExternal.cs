using Repository.Bases;

namespace Repository.Database
{
    public class TUserBindExternal : CD
    {


        public TUserBindExternal(string appName, string appId, string openId)
        {
            AppName = appName;
            AppId = appId;
            OpenId = openId;
        }


        /// <summary>
        /// 用户信息
        /// </summary>
        public long UserId { get; set; }
        public TUser User { get; set; } = null!;



        /// <summary>
        /// App名称
        /// </summary>
        public string AppName { get; set; }




        /// <summary>
        /// AppId
        /// </summary>
        public string AppId { get; set; }



        /// <summary>
        /// 用户绑定ID
        /// </summary>
        public string OpenId { get; set; }



    }
}
