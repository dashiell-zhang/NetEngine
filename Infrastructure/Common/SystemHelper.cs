using System.Diagnostics;

namespace Common
{
    public class SystemHelper
    {


        /// <summary>
        /// 获取本机全部IP
        /// </summary>
        /// <returns></returns>
        public static List<string> GetAllIpAddress()
        {
            var allIp = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName()).AddressList.Select(t => t.ToString()).ToList();

            return allIp;
        }



        /// <summary>
        /// 获取本机 IPV4 地址
        /// </summary>
        /// <returns></returns>
        public static string? GetIpv4Address()
        {
            var ipv4 = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName()).AddressList.FirstOrDefault(address => address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)?.ToString();

            return ipv4;
        }



        /// <summary>
        /// 获取本机 IPV6 地址
        /// </summary>
        /// <returns></returns>
        public static string? GetIpv6Address()
        {
            var ipv6 = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName()).AddressList.FirstOrDefault(address => address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)?.ToString();

            return ipv6;
        }



        /// <summary>
        /// Linux 运行 shell 脚本
        /// </summary>
        /// <param name="shell"></param>
        /// <returns></returns>
        public static string LinuxShell(string shell)
        {
            string output = "";

            //创建一个ProcessStartInfo对象 使用系统shell 指定命令和参数 设置标准输出
            ProcessStartInfo psi = new("/bin/bash", "-c \"" + shell + "\"") { RedirectStandardOutput = true };

            using (var proc = Process.Start(psi))
            {
                if (proc != null)
                {
                    output = proc.StandardOutput.ReadToEnd();

                    if (!proc.HasExited)
                    {
                        proc.Kill();
                    }
                }
            }

            return output;
        }




        /// <summary>
        /// Windows 运行 shell 脚本
        /// </summary>
        /// <param name="shell"></param>
        /// <returns></returns>
        public static string WindowsShell(string shell)
        {
            string output = "";

            //创建一个ProcessStartInfo对象 使用系统shell 指定命令和参数 设置标准输出
            ProcessStartInfo psi = new("powershell", "-c \"" + shell + "\"") { RedirectStandardOutput = true };

            using (var proc = Process.Start(psi))
            {
                if (proc != null)
                {
                    output = proc.StandardOutput.ReadToEnd();

                    if (!proc.HasExited)
                    {
                        proc.Kill();
                    }
                }
            }

            return output;
        }




        /// <summary>
        /// Word转PDF (运行机器需要安装 office)
        /// </summary>
        /// <param name="wordPath">word源文件路径</param>
        /// <param name="pdfPath">pdf保存路径</param>
        /// <returns></returns>
        /// <remarks>文件地址需要用 \\ 切分，不可用 / </remarks>
        public static bool WordToPDF(string wordPath, string pdfPath)
        {
            string shell = "$File='" + wordPath + "'\n$OutFile = '" + pdfPath + "'\n$Word = New-Object –ComObject Word.Application\n$Doc =$Word.Documents.Open($File)\n$Doc.ExportAsFixedFormat($OutFile, 17)\n$Doc.Close()\n";

            WindowsShell(shell);

            return File.Exists(pdfPath);
        }



        /// <summary>
        /// Excel转PDF (运行机器需要安装 office)
        /// </summary>
        /// <param name="excelPath">excel源文件路径</param>
        /// <param name="pdfPath">pdf保存路径</param>
        /// <returns></returns>
        /// <remarks>文件地址需要用 \\ 切分，不可用 / </remarks>
        public static bool ExcelToPDF(string excelPath, string pdfPath)
        {
            string shell = "$File = '" + excelPath + "'\n$OutFile = '" + pdfPath + "'\n$Excel = New-Object –ComObject Excel.Application\n$Workbook =$Excel.Workbooks.Open($File)\n$Workbook.ExportAsFixedFormat(0,$OutFile)\n$Workbook.Close()\n";

            WindowsShell(shell);

            return File.Exists(pdfPath);
        }



        /// <summary>
        /// PPT转PDF 运行机器需要安装 office)
        /// </summary>
        /// <param name="pptPath">ppt源文件路径</param>
        /// <param name="pdfPath">pdf保存路径</param>
        /// <returns></returns>
        /// <remarks>文件地址需要用 \\ 切分，不可用 / </remarks>
        public static bool PPTToPDF(string pptPath, string pdfPath)
        {
            string shell = "$File = '" + pptPath + "'\n$OutFile = '" + pdfPath + "'\n$PowerPoint = New-Object –ComObject PowerPoint.Application\n$Presentation =$PowerPoint.Presentations.Open($File,$True,$False,$False)\n$Presentation.SaveAs($OutFile, 32)\n$Presentation.Close()\n";

            WindowsShell(shell);

            return File.Exists(pdfPath);
        }

    }
}
