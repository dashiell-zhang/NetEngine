using Repository.Bases;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repository.Database
{
    public class TUserBindExternal : CD
    {



        /// <summary>
        /// 外部标记
        /// </summary>
        public Guid UserId { get; set; }
        public TUser User { get; set; }




        /// <summary>
        /// 外部标记
        /// </summary>
        public string Sign { get; set; }



        /// <summary>
        /// 外部ID
        /// </summary>
        public string OpenId { get; set; }



    }
}
