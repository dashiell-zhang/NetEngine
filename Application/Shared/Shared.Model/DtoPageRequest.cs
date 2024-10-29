using System.ComponentModel.DataAnnotations;

namespace Shared.Model
{

    /// <summary>
    /// 分页请求基本入参
    /// </summary>
    public class DtoPageRequest
    {


        /// <summary>
        /// 页码
        /// </summary>
        public int PageNum { get; set; } = 1;



        /// <summary>
        /// 单页数量
        /// </summary>
        [Range(1, 100)]
        public int PageSize { get; set; } = 20;



        /// <summary>
        /// 获取跳过数量
        /// </summary>
        /// <returns></returns>
        public int Skip() => (PageNum - 1) * PageSize;

    }
}
