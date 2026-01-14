using AntDesign;

namespace Admin.App.Models;


public class TreeSelectDto : ITreeData<TreeSelectDto>
{
    public string Key { get; set; }
    public TreeSelectDto Value => this;
    public string Title { get; set; }
    public IEnumerable<TreeSelectDto>? Children { get; set; }


    public bool IsDisabled { get; set; }
}

