using Models.DataBases.Bases;
using System;
using System.Collections.Generic;
using System.Text;

namespace Models.DataBases.WebCore
{
    public class TWeiXinKey : CD
    {
        public string WxAppId { get; set; }
        public string WxAppSecret { get; set; }
        public string MchId { get; set; }
        public string MchKey { get; set; }
        public int Sort { get; set; }
        public string Remark { get; set; }
    }
}
