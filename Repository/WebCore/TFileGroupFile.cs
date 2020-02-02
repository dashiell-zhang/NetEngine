using Repository.Bases;

namespace Repository.WebCore
{
    /// <summary>
    /// 分片上传时的切片文件记录表
    /// </summary>
    public class TFileGroupFile : CD
    {


        /// <summary>
        /// 文件ID
        /// </summary>
        public string FileId { get; set; }
        public TFile File { get; set; }


        /// <summary>
        /// 文件索引值
        /// </summary>
        public int Index { get; set; }


        /// <summary>
        /// 文件保存路径
        /// </summary>
        public string Path { get; set; }

    }
}
