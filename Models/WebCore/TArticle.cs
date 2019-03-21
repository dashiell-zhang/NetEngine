using System;
using System.Collections.Generic;

namespace Models.WebCore
{
    public partial class TArticle
    {
        public int Id { get; set; }
        public int Categoryid { get; set; }
        public string Title { get; set; }
        public string Cover { get; set; }
        public string Content { get; set; }
        public int Sort { get; set; }
        public int? Clickcount { get; set; }
        public string Abstract { get; set; }
        public bool? Openstate { get; set; }
        public bool? Recommendstate { get; set; }
        public bool? Commentstate { get; set; }
        public string Seot { get; set; }
        public string Seok { get; set; }
        public string Seod { get; set; }
        public DateTime Createtime { get; set; }
        public DateTime? Updatetime { get; set; }

        public virtual TCategory Category { get; set; }
    }
}
