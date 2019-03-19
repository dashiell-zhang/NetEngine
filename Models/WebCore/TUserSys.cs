using System;
using System.Collections.Generic;

namespace Models.WebCore
{
    public partial class TUserSys
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Password { get; set; }
        public string Nickname { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        public DateTime Createtime { get; set; }
        public DateTime? Updatetime { get; set; }
    }
}
