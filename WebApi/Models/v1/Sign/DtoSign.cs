namespace WebApi.Models.v1.Sign
{

    /// <summary>
    /// 标记喜欢方法入参
    /// </summary>
    public class DtoSign
    {


        public DtoSign(string table,  string sign)
        {
            Table = table;
            Sign = sign;
        }


        /// <summary>
        /// 表名
        /// </summary>
        public string Table { get; set; }


        /// <summary>
        /// 记录ID
        /// </summary>
        public long TableId { get; set; }


        /// <summary>
        /// 标记
        /// </summary>
        public string Sign { get; set; }
    }
}
