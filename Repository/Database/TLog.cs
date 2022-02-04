using Microsoft.EntityFrameworkCore;
using Repository.Bases;

namespace Repository.Database
{

    /// <summary>
    /// 日志表
    /// </summary>
    [Index(nameof(Sign))]
    public class TLog : CD
    {


        public TLog(string sign, string type, string content)
        {
            Sign = sign;
            Type = type;
            Content = content;
        }



        /// <summary>
        /// 标记
        /// </summary>
        public string Sign { get; set; }



        /// <summary>
        /// 类型
        /// </summary>
        public string Type { get; set; }



        /// <summary>
        /// 内容
        /// </summary>
        public string Content { get; set; }

    }
}
