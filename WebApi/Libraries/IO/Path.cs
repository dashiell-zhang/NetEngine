using WebApi.Libraries.Start;

namespace WebApi.Libraries.IO
{
    public class Path
    {
       

        /// <summary>
        /// 获取 wwwroot 路径
        /// </summary>
        /// <returns></returns>
        public static string WebRootPath()
        {
            return StartHostingEnvironment.webHostEnvironment.WebRootPath;
        }



        /// <summary>
        /// 获取 项目运行 路径
        /// </summary>
        /// <returns></returns>
        public static string ContentRootPath()
        {
            return StartHostingEnvironment.webHostEnvironment.ContentRootPath;
        }
            
    }
}
