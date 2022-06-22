using System.Reflection;

namespace Logger.LocalFile.Models
{
    public class LoggerSetting
    {


        /// <summary>
        /// App标记
        /// </summary>
        public string AppSign { get; set; } = Assembly.GetEntryAssembly()?.GetName().Name!;

    }
}
