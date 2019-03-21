using System;
using System.Collections.Generic;

namespace Models.WebCore
{
    public partial class TChannel
    {
        public TChannel()
        {
            TCategory = new HashSet<TCategory>();
        }

        public int Id { get; set; }
        public string Name { get; set; }
        public int Sort { get; set; }
        public string Remark { get; set; }
        public string Urlrole { get; set; }
        public string Seot { get; set; }
        public string Seok { get; set; }
        public string Seod { get; set; }
        public DateTime Createtime { get; set; }
        public DateTime? Updatetime { get; set; }

        public virtual ICollection<TCategory> TCategory { get; set; }
    }
}
