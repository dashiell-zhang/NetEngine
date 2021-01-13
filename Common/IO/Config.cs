using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;

namespace Common.IO
{

    /// <summary>
    /// 配置文件操作类
    /// </summary>
    public class Config
    {

        /// <summary>
        /// 读取项目配置文件(appsettings.json)
        /// </summary>
        /// <returns></returns>
        public static IConfigurationRoot Get()
        {
            var ev = Environment.GetEnvironmentVariable("NETCORE_ENVIRONMENT");
            if (string.IsNullOrEmpty(ev))
            {
                ev = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            }
            IConfigurationBuilder builder = new ConfigurationBuilder().AddJsonFile("appsettings.json");
            if (!string.IsNullOrEmpty(ev))
            {
                builder = new ConfigurationBuilder().AddJsonFile("appsettings." + ev + ".json");
            }


            var testIpList = new List<string>();

            if (testIpList.Count != 0)
            {
                var localIpList = SystemHelper.GetAllIpAddress();

                foreach (var item in localIpList)
                {
                    if (testIpList.Contains(item))
                    {
                        builder = new ConfigurationBuilder().AddJsonFile("appsettings.Test.json");
                        break;
                    }
                }
            }


            IConfigurationRoot configuration = builder.Build();
            return configuration;
        }


    }
}
