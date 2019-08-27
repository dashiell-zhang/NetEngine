using Models.DataBases.Bases;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Models.DataBases.WebCore.Bases
{

    /// <summary>
    /// 创建，编辑，删除，并关联了用户
    /// </summary>
    public class CUD_User:CUD
    {



        /// <summary>
        /// 创建人ID
        /// </summary>
        [MaxLength(64)]
        public string CreateUserId { get; set; }
        public TUser CreateUser { get; set; }



        /// <summary>
        /// 编辑人ID
        /// </summary>
        [MaxLength(64)]
        public string UpdateUserId { get; set; }
        public TUser UpdateUser { get; set; }



        /// <summary>
        /// 删除人ID
        /// </summary>
        [MaxLength(64)]
        public string DeleteUserId { get; set; }
        public TUser DeleteUser { get; set; }
    }
}
