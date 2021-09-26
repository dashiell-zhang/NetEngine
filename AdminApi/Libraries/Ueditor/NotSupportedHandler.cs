using Microsoft.AspNetCore.Http;


namespace AdminApi.Libraries.Ueditor
{

    /// <summary>
    /// NotSupportedHandler 的摘要说明
    /// </summary>
    public class NotSupportedHandler : Handler
    {
        public NotSupportedHandler(HttpContext context)
            : base()
        {
        }

        public override string Process()
        {
            return WriteJson(new
            {
                state = "action 参数为空或者 action 不被支持。"
            });
        }
    }
}