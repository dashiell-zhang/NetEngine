namespace WebAPI.Libraries.WeiXin.H5.Models
{
    public class DtoWeiXinJsSdkSign
    {
        public string? AppId { get; set; }

        public long TimeStamp { get; set; }

        public string? NonceStr { get; set; }

        public string? Signature { get; set; }

    }
}
