using Repository.Bases;
using System.ComponentModel.DataAnnotations;

namespace Repository.Database
{

    /// <summary>
    /// 锁
    /// </summary>
    public class TLock : CD
    {


        /// <summary>
        /// 锁键
        /// </summary>
        [MaxLength(32)]
        public new string Id { get; set; }



        /// <summary>
        /// 锁生存期
        /// </summary>
        public double TTL { get; set; }


    }
}
