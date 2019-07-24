using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Models.DataBases.WebCore
{


    [Table("t_guidtoint")]
    public class TGuidToInt
    {

        /// <summary>
        /// Id
        /// </summary>
        public int Id { get; set; }



        /// <summary>
        /// Guid 字符串
        /// </summary>
        public string Guid { get; set; }
    }


}
