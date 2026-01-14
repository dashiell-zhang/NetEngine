namespace Application.Model.Authorize;
public class GetTokenByWeiXinAppDto
{

    public string AppId { get; set; }



    /// <summary>
    /// 登录时获取的 code，可通过wx.login获取
    /// </summary>
    public string Code { get; set; }

}
