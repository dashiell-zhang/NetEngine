using Microsoft.Extensions.Configuration.CommandLine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Common
{

    /// <summary>
    /// 环境操作Helper方法
    /// </summary>
    public class EnvironmentHelper
    {



        /// <summary>
        /// 初始化TestServer
        /// </summary>
        public static void InitTestServer()
        {
            var testIpList = new List<string>();

            testIpList.Add("x.x.x.x");

            if (testIpList.Count() != 0)
            {
                var localIpList = SystemHelper.GetAllIpAddress();

                foreach (var item in localIpList)
                {
                    if (testIpList.Contains(item))
                    {
                        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Test");
                        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Test");
                        break;
                    }
                }
            }
        }




        /// <summary>
        /// 改变工作目录
        /// </summary>
        /// <param name="args"></param>
        public static void ChangeDirectory(string[] args)
        {
            var cmdConf = new CommandLineConfigurationProvider(args);
            cmdConf.Load();

            if (cmdConf.TryGet("cd", out string cdStr) && bool.TryParse(cdStr, out bool cd) && cd)
            {
                Directory.SetCurrentDirectory(AppContext.BaseDirectory);
            }
        }
    }
}
