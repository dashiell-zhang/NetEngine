using Microsoft.EntityFrameworkCore;
using Repository.Bases;
using System.ComponentModel.DataAnnotations.Schema;

namespace Repository.Database;
[Index(nameof(Town))]
public class RegionTown : CD
{


    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public new int Id { get; set; }



    /// <summary>
    /// 街道名称
    /// </summary>
    public string Town { get; set; }



    /// <summary>
    /// 所属区域ID
    /// </summary>
    public int AreaId { get; set; }
    public virtual RegionArea Area { get; set; }


}
