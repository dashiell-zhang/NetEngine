using Microsoft.AspNetCore.Hosting;
using System;
using System.Collections.Generic;
using System.Text;

namespace Methods.Start
{
    public class StartHostingEnvironment
    {
        public static IHostingEnvironment hostingEnvironment;

        public static void Add(IHostingEnvironment in_hostingEnvironment)
        {
            hostingEnvironment = in_hostingEnvironment;
        }

    }
}
