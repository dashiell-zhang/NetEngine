﻿using Microsoft.Extensions.Configuration;

namespace Cms.Libraries.Start
{
    public class StartConfiguration
    {
        public static IConfiguration configuration;
        public static void Add(IConfiguration in_configuration)
        {
            configuration = in_configuration;
        }
    }
}
