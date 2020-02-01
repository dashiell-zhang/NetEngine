using Repository.WebCore.Bases;

namespace Repository.WebCore
{


    /// <summary>
    /// 文件表
    /// </summary>
    public class TFile :CD_User
    {


        /// <summary>
        /// 文件名称
        /// </summary>
        public string Name { get; set; }



        /// <summary>
        /// 保存路径
        /// </summary>
        public string Path { get; set; }


        /// <summary>
        /// 文件类型
        /// </summary>
        public string Type { get; set; } 

    }
}
