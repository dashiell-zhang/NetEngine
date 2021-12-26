using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace AdminApi.Libraries.IO
{
    public class Path
    {


        /// <summary>
        /// 获取 wwwroot 路径
        /// </summary>
        /// <returns></returns>
        public static string WebRootPath()
        {
            return Program.ServiceProvider.GetService<IWebHostEnvironment>().WebRootPath.Replace("\\", "/");
        }



        /// <summary>
        /// 获取 项目运行 路径
        /// </summary>
        /// <returns></returns>
        public static string ContentRootPath()
        {
            return Program.ServiceProvider.GetService<IWebHostEnvironment>().ContentRootPath.Replace("\\", "/");
        }

    }
}
