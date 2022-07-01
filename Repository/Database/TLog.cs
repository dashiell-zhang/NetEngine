using Repository.Bases;

namespace Repository.Database
{

    /// <summary>
    /// 日志表
    /// </summary>
    public class TLog : CD
    {


        /// <summary>
        /// 项目
        /// </summary>
        public string Project { get; set; }



        /// <summary>
        /// IP
        /// </summary>
        public string IP { get; set; }



        /// <summary>
        /// 日志等级
        /// </summary>
        public string Level { get; set; }



        /// <summary>
        /// 类别
        /// </summary>
        public string Category { get; set; }



        /// <summary>
        /// 内容
        /// </summary>
        public string Content { get; set; }


    }
}
