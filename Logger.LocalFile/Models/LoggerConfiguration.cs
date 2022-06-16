using Microsoft.Extensions.Logging;
using System.Reflection;

namespace Logger.LocalFile.Models
{
    public class LoggerConfiguration
    {


        /// <summary>
        /// App标记
        /// </summary>
        public string AppSign { get; set; } = Assembly.GetEntryAssembly()?.GetName().Name!;


        /// <summary>
        /// 最低记录等级
        /// </summary>
        public LogLevel MinLogLevel { get; set; } = LogLevel.Information;


        /// <summary>
        /// 日志文件夹路径
        /// </summary>
        public string LogFolderPath { get; set; }
    }
}
