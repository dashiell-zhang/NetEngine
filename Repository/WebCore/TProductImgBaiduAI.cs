using Repository.DataBases.Bases;

namespace Repository.WebCore
{

    /// <summary>
    /// 产品图片对应百度AI信息表
    /// </summary>
    public class TProductImgBaiduAi : CUD
    {


        /// <summary>
        /// 产品图片ID
        /// </summary>
        public string ProductImgId { get; set; }
        public TProductImg ProductImg { get; set; }


        /// <summary>
        /// 图片库唯一标识符
        /// </summary>
        public string Unique { get; set; }


        /// <summary>
        /// 接口返回值
        /// </summary>
        public string Result { get; set; }

    }
}
