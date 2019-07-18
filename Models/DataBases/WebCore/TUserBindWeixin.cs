using Models.DataBases.Bases;
using System;
using System.Collections.Generic;
using System.Text;

namespace Models.DataBases.WebCore
{
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
