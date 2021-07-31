using Repository.Bases;
using System;

namespace Repository.Database
{
    public class TUserBindExternal : CD
    {


        /// <summary>
        /// 用户信息
        /// </summary>
        public Guid UserId { get; set; }
        public TUser User { get; set; }



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
