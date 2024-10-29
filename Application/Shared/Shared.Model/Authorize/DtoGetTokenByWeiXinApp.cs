namespace Shared.Model.Authorize
{
    public class DtoGetTokenByWeiXinApp
    {

        public string AppId { get; set; }



        /// <summary>
        /// 登录时获取的 code，可通过wx.login获取
        /// </summary>
        public string Code { get; set; }

    }
}
