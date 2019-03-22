namespace Methods.Pay.WeiXin
{
    public class JsApiConfig
    {
        #region 字段
        private string partner = string.Empty;
        private string key = string.Empty;
        private string appid = string.Empty;
        private string appsecret = string.Empty;
        private string redirect_url = string.Empty;
        private string notify_url = string.Empty;
        #endregion



        public JsApiConfig()
        {
            //using (cckdwebContext db = new cckdwebContext())
            //{
            //    int cityid = HttpContext.Current().Session.GetInt32("cityid").Value;
            //    var payconf = db.TPayConf.Where(t => t.Type == "微信" & t.Cityid == cityid).FirstOrDefault();

            //    partner = payconf.Key1; //商户号（必须配置）
            //    key = payconf.Key2; //商户支付密钥，参考开户邮件设置（必须配置）
            //    appid = payconf.Key3; //绑定支付的APPID（必须配置）
            //    appsecret = payconf.Key4; //公众帐号secert（仅JSAPI支付的时候需要配置）
            //}
        }

        #region 属性
        /// <summary>
        /// 商户号（必须配置）
        /// </summary>
        public string Partner
        {
            get { return partner; }
            set { partner = value; }
        }

        /// <summary>
        /// 获取或设交易安全校验码
        /// </summary>
        public string Key
        {
            get { return key; }
            set { key = value; }
        }

        /// <summary>
        /// 绑定支付的APPID（必须配置）
        /// </summary>
        public string AppId
        {
            get { return appid; }
            set { appid = value; }
        }

        /// <summary>
        /// 公众帐号secert（仅JSAPI支付的时候需要配置）
        /// </summary>
        public string AppSecret
        {
            get { return appsecret; }
            set { appsecret = value; }
        }


        #endregion
    }
}
