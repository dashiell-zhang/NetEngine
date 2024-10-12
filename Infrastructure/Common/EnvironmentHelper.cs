using Microsoft.Extensions.Configuration.CommandLine;

namespace Common
{

    /// <summary>
    /// 环境操作Helper方法
    /// </summary>
    public class EnvironmentHelper
    {



        /// <summary>
        /// 改变工作目录
        /// </summary>
        /// <param name="args"></param>
        public static void ChangeDirectory(string[] args)
        {
            CommandLineConfigurationProvider cmdConf = new(args);
            cmdConf.Load();

            if (cmdConf.TryGet("cd", out string? cdStr) && bool.TryParse(cdStr, out bool cd) && cd)
            {
                Directory.SetCurrentDirectory(AppContext.BaseDirectory);
            }
        }
    }
}
