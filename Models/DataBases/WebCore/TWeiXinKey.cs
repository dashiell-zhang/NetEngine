using Models.DataBases.Bases;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Models.DataBases.WebCore
{


    /// <summary>
    /// 微信商户平台账户配置表
    /// </summary>
    [Table("t_weixinkey")]
    public class TWeiXinKey : CD
    {


        /// <summary>
        /// WxAppId
        /// </summary>
        public string WxAppId { get; set; }


        /// <summary>
        /// WxAppSecret
        /// </summary>
        public string WxAppSecret { get; set; }


        /// <summary>
        /// MchId
        /// </summary>
        public string MchId { get; set; }


        /// <summary>
        /// MchKey
        /// </summary>
        public string MchKey { get; set; }

        /// <summary>
        /// 排序
        /// </summary>
        public int Sort { get; set; }


        /// <summary>
        /// 备注
        /// </summary>
        public string Remark { get; set; }
    }
}
