namespace WebAPI.Libraries.WeiXin.App.Models
{

    /// <summary>
    /// 获取用户信息接口返回数据类型
    /// </summary>
    public class DtoGetUserInfo
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
