using Common;

namespace WebAPI.Libraries.WeiXin.Public
{
    public class JsApiPay
    {

        private readonly string appid;


        private readonly string mchid;

        private readonly string mchkey;


        public JsApiPay(string appid, string mchid, string mchkey)
        {
            this.appid = appid;
            this.mchid = mchid;
            this.mchkey = mchkey;
        }




        /// <summary>
        /// 查询订单
        /// </summary>
        /// <param name="inputObj">提交给查询订单API的参数</param>
        /// <param name="httpClientFactory"></param>
        /// <returns>成功时返回订单查询结果，其他抛异常</returns>
        /// <exception cref="WxPayException"></exception>
        public WxPayData OrderQuery(WxPayData inputObj, IHttpClientFactory httpClientFactory)
        {
            string sendUrl = "https://api.mch.weixin.qq.com/pay/orderquery";
            //检测必填参数
            if (!inputObj.IsSet("out_trade_no") && !inputObj.IsSet("transaction_id"))
            {
                throw new WxPayException("订单查询接口中，out_trade_no、transaction_id至少填一个！");
            }
            inputObj.SetValue("appid", appid);//公众账号ID
            inputObj.SetValue("mch_id", mchid);//商户号
            inputObj.SetValue("nonce_str", Guid.NewGuid().ToString().Replace("-", ""));//随机字符串
            inputObj.SetValue("sign", inputObj.MakeSign(mchkey));//签名
            string xml = inputObj.ToXml();
            string response = httpClientFactory.Post(sendUrl, xml, "xml");//调用HTTP通信接口提交数据
            //将xml格式的数据转化为对象以返回
            WxPayData result = new();
            result.FromXml(response, mchkey);
            return result;
        }

    }
}
