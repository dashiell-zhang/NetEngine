using Microsoft.AspNetCore.Hosting;

namespace TaskAdmin.Libraries.Start
{
    public class StartHostingEnvironment
    {
        public static IWebHostEnvironment webHostEnvironment;

        public static void Add(IWebHostEnvironment in_webHostEnvironment)
        {
            webHostEnvironment = in_webHostEnvironment;
        }

    }
}
