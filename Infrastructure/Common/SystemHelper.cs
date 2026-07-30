#if !BROWSER
using System.Diagnostics;

namespace Common;

/// <summary>
/// 提供网络地址、Shell执行和Office文档转换能力
/// </summary>
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
    /// <param name="timeoutSeconds">超时秒数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public static string LinuxShell(string shell, int timeoutSeconds = 60, CancellationToken cancellationToken = default)
    {
        string strictShell = "shopt -s inherit_errexit\n" + shell;
        var result = ExecuteShell("/bin/bash", ["-e", "-o", "pipefail", "-c", strictShell], timeoutSeconds, cancellationToken);
        EnsureShellSuccess(result);
        return result.Output;
    }


    /// <summary>
    /// Windows 运行 shell 脚本
    /// </summary>
    /// <param name="shell"></param>
    /// <param name="timeoutSeconds">超时秒数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    /// <remarks>需要PowerShell 7.3及以上版本</remarks>
    public static string WindowsShell(string shell, int timeoutSeconds = 60, CancellationToken cancellationToken = default)
    {
        return ExecuteWindowsShell(shell, timeoutSeconds, cancellationToken);
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
        string tempPdfPath = GetTemporaryPdfPath(pdfPath);
        string filePath = EscapePowerShellSingleQuotedString(wordPath);
        string outputPath = EscapePowerShellSingleQuotedString(tempPdfPath);
        string shell = $$"""
            $File = '{{filePath}}'
            $OutFile = '{{outputPath}}'
            $Word = $null
            $Documents = $null
            $Doc = $null
            try {
                $Word = New-Object -ComObject Word.Application
                $Documents = $Word.Documents
                $Doc = $Documents.Open($File)
                $Doc.ExportAsFixedFormat($OutFile, 17)
            }
            finally {
                if ($Doc -ne $null) {
                    try { $Doc.Close($false) | Out-Null } catch {}
                    try { [System.Runtime.InteropServices.Marshal]::ReleaseComObject($Doc) | Out-Null } catch {}
                }
                if ($Documents -ne $null) {
                    try { [System.Runtime.InteropServices.Marshal]::ReleaseComObject($Documents) | Out-Null } catch {}
                }
                if ($Word -ne $null) {
                    try { $Word.Quit() | Out-Null } catch {}
                    try { [System.Runtime.InteropServices.Marshal]::ReleaseComObject($Word) | Out-Null } catch {}
                }
            }
            """;

        return ConvertOfficeToPdf(shell, tempPdfPath, pdfPath);
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
        string tempPdfPath = GetTemporaryPdfPath(pdfPath);
        string filePath = EscapePowerShellSingleQuotedString(excelPath);
        string outputPath = EscapePowerShellSingleQuotedString(tempPdfPath);
        string shell = $$"""
            $File = '{{filePath}}'
            $OutFile = '{{outputPath}}'
            $Excel = $null
            $Workbooks = $null
            $Workbook = $null
            try {
                $Excel = New-Object -ComObject Excel.Application
                $Workbooks = $Excel.Workbooks
                $Workbook = $Workbooks.Open($File)
                $Workbook.ExportAsFixedFormat(0, $OutFile)
            }
            finally {
                if ($Workbook -ne $null) {
                    try { $Workbook.Close($false) | Out-Null } catch {}
                    try { [System.Runtime.InteropServices.Marshal]::ReleaseComObject($Workbook) | Out-Null } catch {}
                }
                if ($Workbooks -ne $null) {
                    try { [System.Runtime.InteropServices.Marshal]::ReleaseComObject($Workbooks) | Out-Null } catch {}
                }
                if ($Excel -ne $null) {
                    try { $Excel.Quit() | Out-Null } catch {}
                    try { [System.Runtime.InteropServices.Marshal]::ReleaseComObject($Excel) | Out-Null } catch {}
                }
            }
            """;

        return ConvertOfficeToPdf(shell, tempPdfPath, pdfPath);
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
        string tempPdfPath = GetTemporaryPdfPath(pdfPath);
        string filePath = EscapePowerShellSingleQuotedString(pptPath);
        string outputPath = EscapePowerShellSingleQuotedString(tempPdfPath);
        string shell = $$"""
            $File = '{{filePath}}'
            $OutFile = '{{outputPath}}'
            $PowerPoint = $null
            $Presentations = $null
            $Presentation = $null
            try {
                $PowerPoint = New-Object -ComObject PowerPoint.Application
                $Presentations = $PowerPoint.Presentations
                $Presentation = $Presentations.Open($File, $True, $False, $False)
                $Presentation.SaveAs($OutFile, 32)
            }
            finally {
                if ($Presentation -ne $null) {
                    try { $Presentation.Close() | Out-Null } catch {}
                    try { [System.Runtime.InteropServices.Marshal]::ReleaseComObject($Presentation) | Out-Null } catch {}
                }
                if ($Presentations -ne $null) {
                    try { [System.Runtime.InteropServices.Marshal]::ReleaseComObject($Presentations) | Out-Null } catch {}
                }
                if ($PowerPoint -ne $null) {
                    try { $PowerPoint.Quit() | Out-Null } catch {}
                    try { [System.Runtime.InteropServices.Marshal]::ReleaseComObject($PowerPoint) | Out-Null } catch {}
                }
            }
            """;

        return ConvertOfficeToPdf(shell, tempPdfPath, pdfPath);
    }


    /// <summary>
    /// 严格执行Windows PowerShell脚本
    /// </summary>
    /// <param name="shell">PowerShell脚本</param>
    /// <param name="timeoutSeconds">超时秒数，为空时不限制执行时间</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>脚本标准输出</returns>
    private static string ExecuteWindowsShell(string shell, int? timeoutSeconds, CancellationToken cancellationToken)
    {
        string tempScriptPath = Path.Combine(Path.GetTempPath(), "NetEngine." + Guid.NewGuid().ToString("N") + ".ps1");

        try
        {
            using (FileStream tempScriptStream = new(tempScriptPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (StreamWriter writer = new(tempScriptStream, new System.Text.UTF8Encoding(false)))
            {
                writer.Write(shell);
            }

            using FileStream tempScriptLock = new(tempScriptPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            string encodedScriptPath = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(tempScriptPath));
            string strictShell = $$"""
                [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
                $OutputEncoding = [Console]::OutputEncoding
                $ErrorActionPreference = 'Stop'
                if ($PSVersionTable.PSVersion -lt [Version]'7.3') { throw '严格模式需要PowerShell 7.3及以上版本' }
                $PSNativeCommandUseErrorActionPreference = $true
                & ([System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('{{encodedScriptPath}}')))
                """;
            var result = ExecuteShell("pwsh", ["-NoLogo", "-NoProfile", "-NonInteractive", "-STA", "-Command", strictShell], timeoutSeconds, cancellationToken);
            EnsureShellSuccess(result);
            return result.Output;
        }
        finally
        {
            IOHelper.DeleteFile(tempScriptPath);
        }
    }


    /// <summary>
    /// 执行Shell进程并收集结果
    /// </summary>
    /// <param name="fileName">进程名称</param>
    /// <param name="arguments">进程参数</param>
    /// <param name="timeoutSeconds">超时秒数，为空时不限制执行时间</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>Shell执行结果</returns>
    private static (string Output, string Error, int ExitCode) ExecuteShell(string fileName, IEnumerable<string> arguments, int? timeoutSeconds, CancellationToken cancellationToken)
    {
        ValidateShellExecutionOptions(timeoutSeconds, cancellationToken);

        ProcessStartInfo psi = new(fileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (string argument in arguments)
        {
            psi.ArgumentList.Add(argument);
        }

        using var process = Process.Start(psi) ?? throw new InvalidOperationException($"无法启动进程：{fileName}");
        using MemoryStream outputStream = new();
        using MemoryStream errorStream = new();
        Task outputTask = process.StandardOutput.BaseStream.CopyToAsync(outputStream);
        Task errorTask = process.StandardError.BaseStream.CopyToAsync(errorStream);

        TimeSpan? timeout = timeoutSeconds.HasValue ? TimeSpan.FromSeconds(timeoutSeconds.Value) : null;
        Stopwatch stopwatch = Stopwatch.StartNew();

        while (!process.WaitForExit(100))
        {
            if (cancellationToken.IsCancellationRequested || timeout.HasValue && stopwatch.Elapsed >= timeout.Value)
            {
                if (!process.HasExited)
                {
                    try
                    {
                        process.Kill(entireProcessTree: true);
                    }
                    catch (InvalidOperationException) when (process.HasExited)
                    {
                    }
                }

                process.WaitForExit();
                Task.WhenAll(outputTask, errorTask).GetAwaiter().GetResult();
                cancellationToken.ThrowIfCancellationRequested();
                throw new TimeoutException($"Shell执行超过{timeoutSeconds!.Value}秒");
            }
        }

        Task.WhenAll(outputTask, errorTask).GetAwaiter().GetResult();
        return (System.Text.Encoding.UTF8.GetString(outputStream.ToArray()), System.Text.Encoding.UTF8.GetString(errorStream.ToArray()), process.ExitCode);
    }


    /// <summary>
    /// 验证Shell执行参数
    /// </summary>
    /// <param name="timeoutSeconds">超时秒数，为空时不限制执行时间</param>
    /// <param name="cancellationToken">取消令牌</param>
    private static void ValidateShellExecutionOptions(int? timeoutSeconds, CancellationToken cancellationToken)
    {
        if (timeoutSeconds.HasValue && timeoutSeconds.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(timeoutSeconds), "超时秒数必须大于0");
        }

        cancellationToken.ThrowIfCancellationRequested();
    }


    /// <summary>
    /// 验证Shell是否执行成功
    /// </summary>
    /// <param name="result">Shell执行结果</param>
    private static void EnsureShellSuccess((string Output, string Error, int ExitCode) result)
    {
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"Shell执行失败，退出码：{result.ExitCode}，错误：{result.Error}");
        }
    }


    /// <summary>
    /// 获取PDF临时文件路径
    /// </summary>
    /// <param name="pdfPath">最终PDF路径</param>
    /// <returns>临时PDF路径</returns>
    private static string GetTemporaryPdfPath(string pdfPath)
    {
        string fullPath = Path.GetFullPath(pdfPath);
        string? directory = Path.GetDirectoryName(fullPath);

        if (directory == null)
        {
            throw new ArgumentException("PDF目标路径无效", nameof(pdfPath));
        }

        Directory.CreateDirectory(directory);
        return Path.Combine(directory, Path.GetFileNameWithoutExtension(fullPath) + "." + Guid.NewGuid().ToString("N") + ".pdf");
    }


    /// <summary>
    /// 执行Office转换并安全替换目标PDF
    /// </summary>
    /// <param name="shell">转换脚本</param>
    /// <param name="tempPdfPath">临时PDF路径</param>
    /// <param name="pdfPath">最终PDF路径</param>
    /// <returns>是否转换成功</returns>
    private static bool ConvertOfficeToPdf(string shell, string tempPdfPath, string pdfPath)
    {
        try
        {
            ExecuteWindowsShell(shell, null, default);

            if (!File.Exists(tempPdfPath) || new FileInfo(tempPdfPath).Length == 0)
            {
                return false;
            }

            File.Move(tempPdfPath, Path.GetFullPath(pdfPath), true);
            return true;
        }
        finally
        {
            IOHelper.DeleteFile(tempPdfPath);
        }
    }


    /// <summary>
    /// 转义 PowerShell 单引号字符串内容
    /// </summary>
    private static string EscapePowerShellSingleQuotedString(string value)
    {
        return value.Replace("'", "''");
    }

}
#endif
