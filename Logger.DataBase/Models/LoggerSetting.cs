using System.Reflection;

namespace Logger.DataBase.Models
{
    public class LoggerSetting
    {

        /// <summary>
        /// 项目
        /// </summary>
        public string Project { get; set; } = Assembly.GetEntryAssembly()?.GetName().Name!;

    }
}
