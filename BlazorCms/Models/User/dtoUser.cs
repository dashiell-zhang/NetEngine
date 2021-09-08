using BlazorCms.Libraries.JsonConverter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace BlazorCms.Models.User
{


    public class dtoUser
    {
        /// <summary>
        /// 用户名
        /// </summary>
        public string Name { get; set; }


        /// <summary>
        /// 昵称
        /// </summary>
        public string NickName { get; set; }


        /// <summary>
        /// 手机号
        /// </summary>
        public string Phone { get; set; }


        /// <summary>
        /// 邮箱
        /// </summary>
        public string Email { get; set; }


        /// <summary>
        /// 角色
        /// </summary>
        public string Roles { get; set; }


        /// <summary>
        /// 创建时间
        /// </summary>
        //[JsonConverter(typeof(DateTimeConverter))]
        public DateTime CreateTime { get; set; }

    }
}
