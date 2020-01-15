using Microsoft.Extensions.Configuration;

namespace Common.IO
{

    /// <summary>
    /// 配置文件操作类
    /// </summary>
    public class Config
    {

        /// <summary>
        /// 读取项目默认配置文件(appsettings.json)
        /// </summary>
        /// <returns></returns>
        public static IConfigurationRoot Get()
        {
            var builder = new ConfigurationBuilder()
              .AddJsonFile("appsettings.json");
            var configuration = builder.Build();
            return configuration;
        }

    }
}
