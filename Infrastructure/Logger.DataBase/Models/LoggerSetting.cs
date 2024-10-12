using System.Reflection;

namespace Logger.DataBase.Models
{
    public class LoggerSetting
    {

        /// <summary>
        /// 项目
        /// </summary>
        public string Project { get; set; } = Assembly.GetEntryAssembly()?.GetName().Name!;



        /// <summary>
        /// 保存天数
        /// </summary>
        /// <remarks>永久保存：-1</remarks>
        public int SaveDays { get; set; } = 14;

    }
}
