using System;
using System.Collections.Generic;

namespace Models.WebCore
{
    public partial class TFile
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Path { get; set; }
        public string Createuserid { get; set; }
        public DateTime Createtime { get; set; }
    }
}
