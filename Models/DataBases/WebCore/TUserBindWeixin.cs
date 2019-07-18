using Models.DataBases.Bases;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Models.DataBases.WebCore
{


    /// <summary>
    /// 用户和微信绑定关系表
    /// </summary>
    [Table("t_user_bind_weixin")]
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
