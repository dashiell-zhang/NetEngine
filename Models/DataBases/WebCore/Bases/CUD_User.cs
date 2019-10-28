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
    public class CUD_User:CD_User
    {

        /// <summary>
        /// 编辑人ID
        /// </summary>
        [MaxLength(36)]
        public string UpdateUserId { get; set; }
        public TUser UpdateUser { get; set; }

    }
}
