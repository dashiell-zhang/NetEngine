namespace WebAPI.Models.Authorize
{

    /// <summary>
    /// 获取微信APP用户信息
    /// </summary>
    public class DtoGetWeiXinAPPUserInfo
    {
        public string? OpenId { get; set; }

        public string? NickName { get; set; }

        public int Sex { get; set; }

        public string? Province { get; set; }

        public string? City { get; set; }

        public string? Country { get; set; }

        public string? HeadimgUrl { get; set; }

        public string[]? Privilege { get; set; }

        public string? UnionId { get; set; }
    }
}
