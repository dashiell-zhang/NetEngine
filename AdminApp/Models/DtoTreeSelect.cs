using AntDesign;

namespace AdminApp.Models
{


    public class DtoTreeSelect : ITreeData<DtoTreeSelect>
    {
        public string Key { get; set; }
        public DtoTreeSelect Value => this;
        public string Title { get; set; }
        public IEnumerable<DtoTreeSelect>? Children { get; set; }


        public bool IsDisabled { get; set; }
    }

}
