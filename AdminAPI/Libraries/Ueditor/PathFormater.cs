using System.Text.RegularExpressions;


namespace AdminAPI.Libraries.Ueditor
{

    /// <summary>
    /// PathFormater 的摘要说明
    /// </summary>
#pragma warning disable SYSLIB1045 // Convert to 'GeneratedRegexAttribute'.
    public static class PathFormatter
    {
        public static string Format(string originFileName, string pathFormat)
        {
            if (string.IsNullOrWhiteSpace(pathFormat))
            {
                pathFormat = "{filename}{rand:6}";
            }

            Regex invalidPattern = new(@"[\\\/\:\*\?\042\<\>\|]");
            originFileName = invalidPattern.Replace(originFileName, "");

            string extension = Path.GetExtension(originFileName);
            string filename = Path.GetFileNameWithoutExtension(originFileName);

            pathFormat = pathFormat.Replace("{filename}", filename);
            pathFormat = new Regex(@"\{rand(\:?)(\d+)\}", RegexOptions.Compiled).Replace(pathFormat, new MatchEvaluator(delegate (Match match)
            {
                var digit = 6;
                if (match.Groups.Count > 2)
                {
                    digit = Convert.ToInt32(match.Groups[2].Value);
                }
                Random rand = new();
                return rand.Next((int)Math.Pow(10, digit), (int)Math.Pow(10, digit + 1)).ToString();
            }));

            pathFormat = pathFormat.Replace("{time}", DateTime.UtcNow.Ticks.ToString());
            pathFormat = pathFormat.Replace("{yyyy}", DateTime.UtcNow.Year.ToString());
            pathFormat = pathFormat.Replace("{yy}", (DateTime.UtcNow.Year % 100).ToString("D2"));
            pathFormat = pathFormat.Replace("{mm}", DateTime.UtcNow.Month.ToString("D2"));
            pathFormat = pathFormat.Replace("{dd}", DateTime.UtcNow.Day.ToString("D2"));
            pathFormat = pathFormat.Replace("{hh}", DateTime.UtcNow.Hour.ToString("D2"));
            pathFormat = pathFormat.Replace("{ii}", DateTime.UtcNow.Minute.ToString("D2"));
            pathFormat = pathFormat.Replace("{ss}", DateTime.UtcNow.Second.ToString("D2"));

            return pathFormat + extension;
        }
    }

}