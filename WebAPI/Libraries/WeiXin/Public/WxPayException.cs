namespace WebAPI.Libraries.WeiXin.Public
{
    public class WxPayException : Exception
    {
        public WxPayException(string msg) : base(msg)
        {

        }
    }
}
