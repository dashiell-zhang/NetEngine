using System;
using System.Collections.Generic;

namespace Models.WebCore
{
    public partial class TCategory
    {
        public TCategory()
        {
            TArticle = new HashSet<TArticle>();
        }

        public int Id { get; set; }
        public int Channelid { get; set; }
        public string Name { get; set; }
        public int Sort { get; set; }
        public int Parentid { get; set; }
        public string Remark { get; set; }
        public string Urlrole { get; set; }
        public string Seot { get; set; }
        public string Seok { get; set; }
        public string Seod { get; set; }
        public DateTime Createtime { get; set; }
        public DateTime? Updatetime { get; set; }

        public virtual TChannel Channel { get; set; }
        public virtual ICollection<TArticle> TArticle { get; set; }
    }
}
