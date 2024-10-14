using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace TaskService.Core.ScheduleTask
{
#pragma warning disable SYSLIB1045 // Convert to 'GeneratedRegexAttribute'.
    public class CronHelper
    {


        /// <summary>
        /// ��ȡ��ǰʱ��֮����һ�δ���ʱ��
        /// </summary>
        /// <param name="cronExpression"></param>
        /// <returns></returns>
        public static DateTimeOffset GetNextOccurrence(string cronExpression)
        {
            return GetNextOccurrence(cronExpression, DateTimeOffset.UtcNow);
        }



        /// <summary>
        /// ��ȡ����ʱ��֮����һ�δ���ʱ��
        /// </summary>
        /// <param name="cronExpression"></param>
        /// <param name="afterTimeUtc"></param>
        /// <returns></returns>
        public static DateTimeOffset GetNextOccurrence(string cronExpression, DateTimeOffset afterTimeUtc)
        {
            return new CronExpression(cronExpression).GetTimeAfter(afterTimeUtc)!.Value;
        }



        /// <summary>
        /// ��ȡ��ǰʱ��֮��N�δ���ʱ��
        /// </summary>
        /// <param name="cronExpression"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public static List<DateTimeOffset> GetNextOccurrences(string cronExpression, int count)
        {
            return GetNextOccurrences(cronExpression, DateTimeOffset.UtcNow, count);
        }



        /// <summary>
        /// ��ȡ����ʱ��֮��N�δ���ʱ��
        /// </summary>
        /// <param name="cronExpression"></param>
        /// <param name="afterTimeUtc"></param>
        /// <returns></returns>
        public static List<DateTimeOffset> GetNextOccurrences(string cronExpression, DateTimeOffset afterTimeUtc, int count)
        {
            CronExpression cron = new(cronExpression);

            List<DateTimeOffset> dateTimeOffsets = [];

            for (int i = 0; i < count; i++)
            {
                afterTimeUtc = cron.GetTimeAfter(afterTimeUtc)!.Value;

                dateTimeOffsets.Add(afterTimeUtc);
            }

            return dateTimeOffsets;
        }



        private class CronExpression
        {

            private const int Second = 0;

            private const int Minute = 1;

            private const int Hour = 2;

            private const int DayOfMonth = 3;

            private const int Month = 4;

            private const int DayOfWeek = 5;

            private const int Year = 6;

            private const int AllSpecInt = 99;

            private const int NoSpecInt = 98;

            private const int AllSpec = AllSpecInt;

            private const int NoSpec = NoSpecInt;

            private SortedSet<int> seconds = null!;

            private SortedSet<int> minutes = null!;

            private SortedSet<int> hours = null!;

            private SortedSet<int> daysOfMonth = null!;

            private SortedSet<int> months = null!;

            private SortedSet<int> daysOfWeek = null!;

            private SortedSet<int> years = null!;

            private bool lastdayOfWeek;

            private int everyNthWeek;

            private int nthdayOfWeek;

            private bool lastdayOfMonth;

            private bool nearestWeekday;

            private int lastdayOffset;

            private static readonly Dictionary<string, int> monthMap = new(20);

            private static readonly Dictionary<string, int> dayMap = new(60);

            private static readonly int MaxYear = DateTime.Now.Year + 100;

            private static readonly char[] splitSeparators = [' ', '\t', '\r', '\n'];

            private static readonly char[] commaSeparator = [','];


            private static readonly Regex regex = new("^L-[0-9]*[W]?", RegexOptions.Compiled);


            private static readonly TimeZoneInfo timeZoneInfo = TimeZoneInfo.Local;

            private static readonly object monthMapLock = new();

            public CronExpression(string cronExpression)
            {
                if (monthMap.Count == 0)
                {
                    lock (monthMapLock)
                    {
                        if (monthMap.Count == 0)
                        {
                            monthMap.Add("JAN", 0);
                            monthMap.Add("FEB", 1);
                            monthMap.Add("MAR", 2);
                            monthMap.Add("APR", 3);
                            monthMap.Add("MAY", 4);
                            monthMap.Add("JUN", 5);
                            monthMap.Add("JUL", 6);
                            monthMap.Add("AUG", 7);
                            monthMap.Add("SEP", 8);
                            monthMap.Add("OCT", 9);
                            monthMap.Add("NOV", 10);
                            monthMap.Add("DEC", 11);

                            dayMap.Add("SUN", 1);
                            dayMap.Add("MON", 2);
                            dayMap.Add("TUE", 3);
                            dayMap.Add("WED", 4);
                            dayMap.Add("THU", 5);
                            dayMap.Add("FRI", 6);
                            dayMap.Add("SAT", 7);
                        }
                    }
                }

                if (cronExpression == null)
                {
                    throw new ArgumentException("cronExpression ����Ϊ��");
                }

                CronExpressionString = CultureInfo.InvariantCulture.TextInfo.ToUpper(cronExpression);
                BuildExpression(CronExpressionString);
            }





            /// <summary>
            /// �������ʽ
            /// </summary>
            /// <param name="expression"></param>
            /// <exception cref="FormatException"></exception>
            private void BuildExpression(string expression)
            {
                try
                {
                    seconds ??= [];
                    minutes ??= [];
                    hours ??= [];
                    daysOfMonth ??= [];
                    months ??= [];
                    daysOfWeek ??= [];
                    years ??= [];

                    int exprOn = Second;

                    string[] exprsTok = expression.Split(splitSeparators, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string exprTok in exprsTok)
                    {
                        string expr = exprTok.Trim();

                        if (expr.Length == 0)
                        {
                            continue;
                        }
                        if (exprOn > Year)
                        {
                            break;
                        }

                        if (exprOn == DayOfMonth && expr.Contains('L') && expr.Length > 1 && expr.Contains(','))
                        {
                            throw new FormatException("��֧�����·ݵ���������ָ����L���͡�LW��");
                        }
                        if (exprOn == DayOfWeek && expr.Contains('L') && expr.Length > 1 && expr.Contains(','))
                        {
                            throw new FormatException("��֧����һ�ܵ���������ָ����L��");
                        }
                        if (exprOn == DayOfWeek && expr.Contains('#') && expr.IndexOf('#', expr.IndexOf('#') + 1) != -1)
                        {
                            throw new FormatException("��֧��ָ���������N���졣");
                        }

                        string[] vTok = expr.Split(commaSeparator);
                        foreach (string v in vTok)
                        {
                            StoreExpressionVals(0, v, exprOn);
                        }

                        exprOn++;
                    }

                    if (exprOn <= DayOfWeek)
                    {
                        throw new FormatException("���ʽ����֮��Ľ�����");
                    }

                    if (exprOn <= Year)
                    {
                        StoreExpressionVals(0, "*", Year);
                    }

                    var dow = GetSet(DayOfWeek);
                    var dom = GetSet(DayOfMonth);

                    bool dayOfMSpec = !dom.Contains(NoSpec);
                    bool dayOfWSpec = !dow.Contains(NoSpec);

                    if (dayOfMSpec && !dayOfWSpec)
                    {
                        // skip
                    }
                    else if (dayOfWSpec && !dayOfMSpec)
                    {
                        // skip
                    }
                    else
                    {
                        throw new FormatException("��֧��ͬʱָ�����ں��ղ�����");
                    }
                }
                catch (FormatException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    throw new FormatException($"�Ƿ��� cron ���ʽ��ʽ ({e.Message})", e);
                }
            }



            /// <summary>
            /// Stores the expression values.
            /// </summary>
            /// <param name="pos">The position.</param>
            /// <param name="s">The string to traverse.</param>
            /// <param name="type">The type of value.</param>
            /// <returns></returns>
            private int StoreExpressionVals(int pos, string s, int type)
            {
                int incr = 0;
                int i = SkipWhiteSpace(pos, s);
                if (i >= s.Length)
                {
                    return i;
                }
                char c = s[i];
                if (c >= 'A' && c <= 'Z' && !s.Equals("L") && !s.Equals("LW") && !regex.IsMatch(s))
                {
                    string sub = s.Substring(i, 3);
                    int sval;
                    int eval = -1;
                    if (type == Month)
                    {
                        sval = GetMonthNumber(sub) + 1;
                        if (sval <= 0)
                        {
                            throw new FormatException($"��Ч���·�ֵ��'{sub}'");
                        }
                        if (s.Length > i + 3)
                        {
                            c = s[i + 3];
                            if (c == '-')
                            {
                                i += 4;
                                sub = s.Substring(i, 3);
                                eval = GetMonthNumber(sub) + 1;
                                if (eval <= 0)
                                {
                                    throw new FormatException(
                                        $"��Ч���·�ֵ�� '{sub}'");
                                }
                            }
                        }
                    }
                    else if (type == DayOfWeek)
                    {
                        sval = GetDayOfWeekNumber(sub);
                        if (sval < 0)
                        {
                            throw new FormatException($"��Ч�����ڼ�ֵ�� '{sub}'");
                        }
                        if (s.Length > i + 3)
                        {
                            c = s[i + 3];
                            if (c == '-')
                            {
                                i += 4;
                                sub = s.Substring(i, 3);
                                eval = GetDayOfWeekNumber(sub);
                                if (eval < 0)
                                {
                                    throw new FormatException(
                                        $"��Ч�����ڼ�ֵ�� '{sub}'");
                                }
                            }
                            else if (c == '#')
                            {
                                try
                                {
                                    i += 4;
                                    nthdayOfWeek = Convert.ToInt32(s[i..], CultureInfo.InvariantCulture);
                                    if (nthdayOfWeek is < 1 or > 5)
                                    {
                                        throw new FormatException("�ܵĵ�n��С��1�����5");
                                    }
                                }
                                catch (Exception)
                                {
                                    throw new FormatException("1 �� 5 ֮�����ֵ������ڡ�#��ѡ�����");
                                }
                            }
                            else if (c == '/')
                            {
                                try
                                {
                                    i += 4;
                                    everyNthWeek = Convert.ToInt32(s[i..], CultureInfo.InvariantCulture);
                                    if (everyNthWeek is < 1 or > 5)
                                    {
                                        throw new FormatException("ÿ������<1��>5");
                                    }
                                }
                                catch (Exception)
                                {
                                    throw new FormatException("1 �� 5 ֮�����ֵ������� '/' ѡ�����");
                                }
                            }
                            else if (c == 'L')
                            {
                                lastdayOfWeek = true;
                                i++;
                            }
                            else
                            {
                                throw new FormatException($"��λ�õķǷ��ַ���'{sub}'");
                            }
                        }
                    }
                    else
                    {
                        throw new FormatException($"��λ�õķǷ��ַ���'{sub}'");
                    }
                    if (eval != -1)
                    {
                        incr = 1;
                    }
                    AddToSet(sval, eval, incr, type);
                    return i + 3;
                }

                if (c == '?')
                {
                    i++;
                    if (i + 1 < s.Length && s[i] != ' ' && s[i + 1] != '\t')
                    {
                        throw new FormatException("'?' ��ķǷ��ַ�: " + s[i]);
                    }
                    if (type != DayOfWeek && type != DayOfMonth)
                    {
                        throw new FormatException(
                            "'?' ֻ��Ϊ���ջ�����ָ����");
                    }
                    if (type == DayOfWeek && !lastdayOfMonth)
                    {
                        int val = daysOfMonth.LastOrDefault();
                        if (val == NoSpecInt)
                        {
                            throw new FormatException(
                                "'?' ֻ��Ϊ���ջ�����ָ����");
                        }
                    }

                    AddToSet(NoSpecInt, -1, 0, type);
                    return i;
                }

                var startsWithAsterisk = c == '*';
                if (startsWithAsterisk || c == '/')
                {
                    if (startsWithAsterisk && i + 1 >= s.Length)
                    {
                        AddToSet(AllSpecInt, -1, incr, type);
                        return i + 1;
                    }
                    if (c == '/' && (i + 1 >= s.Length || s[i + 1] == ' ' || s[i + 1] == '\t'))
                    {
                        throw new FormatException("'/' ��������һ��������");
                    }
                    if (startsWithAsterisk)
                    {
                        i++;
                    }
                    c = s[i];
                    if (c == '/')
                    {
                        // is an increment specified?
                        i++;
                        if (i >= s.Length)
                        {
                            throw new FormatException("�ַ������������");
                        }

                        incr = GetNumericValue(s, i);

                        i++;
                        if (incr > 10)
                        {
                            i++;
                        }
                        CheckIncrementRange(incr, type);
                    }
                    else
                    {
                        if (startsWithAsterisk)
                        {
                            throw new FormatException("�Ǻź�ķǷ��ַ���" + s);
                        }
                        incr = 1;
                    }

                    AddToSet(AllSpecInt, -1, incr, type);
                    return i;
                }
                if (c == 'L')
                {
                    i++;
                    if (type == DayOfMonth)
                    {
                        lastdayOfMonth = true;
                    }
                    if (type == DayOfWeek)
                    {
                        AddToSet(7, 7, 0, type);
                    }
                    if (type == DayOfMonth && s.Length > i)
                    {
                        c = s[i];
                        if (c == '-')
                        {
                            ValueSet vs = GetValue(0, s, i + 1);
                            lastdayOffset = vs.theValue;
                            if (lastdayOffset > 30)
                            {
                                throw new FormatException("�����һ���ƫ�������� <= 30");
                            }
                            i = vs.pos;
                        }
                        if (s.Length > i)
                        {
                            c = s[i];
                            if (c == 'W')
                            {
                                nearestWeekday = true;
                                i++;
                            }
                        }
                    }
                    return i;
                }
                if (c >= '0' && c <= '9')
                {
                    int val = Convert.ToInt32(c.ToString(), CultureInfo.InvariantCulture);
                    i++;
                    if (i >= s.Length)
                    {
                        AddToSet(val, -1, -1, type);
                    }
                    else
                    {
                        c = s[i];
                        if (c >= '0' && c <= '9')
                        {
                            ValueSet vs = GetValue(val, s, i);
                            val = vs.theValue;
                            i = vs.pos;
                        }
                        i = CheckNext(i, s, val, type);
                        return i;
                    }
                }
                else
                {
                    throw new FormatException($"�����ַ���{c}");
                }

                return i;
            }



            // ReSharper disable once UnusedParameter.Local
            private static void CheckIncrementRange(int incr, int type)
            {
                if (incr > 59 && (type == Second || type == Minute))
                {
                    throw new FormatException($"���� > 60 : {incr}");
                }
                if (incr > 23 && type == Hour)
                {
                    throw new FormatException($"���� > 24 : {incr}");
                }
                if (incr > 31 && type == DayOfMonth)
                {
                    throw new FormatException($"���� > 31 : {incr}");
                }
                if (incr > 7 && type == DayOfWeek)
                {
                    throw new FormatException($"���� > 7 : {incr}");
                }
                if (incr > 12 && type == Month)
                {
                    throw new FormatException($"���� > 12 : {incr}");
                }
            }



            /// <summary>
            /// Checks the next value.
            /// </summary>
            /// <param name="pos">The position.</param>
            /// <param name="s">The string to check.</param>
            /// <param name="val">The value.</param>
            /// <param name="type">The type to search.</param>
            /// <returns></returns>
            private int CheckNext(int pos, string s, int val, int type)
            {
                int end = -1;
                int i = pos;

                if (i >= s.Length)
                {
                    AddToSet(val, end, -1, type);
                    return i;
                }

                char c = s[pos];

                if (c == 'L')
                {
                    if (type == DayOfWeek)
                    {
                        if (val < 1 || val > 7)
                        {
                            throw new FormatException("������ֵ�������1��7֮��");
                        }
                        lastdayOfWeek = true;
                    }
                    else
                    {
                        throw new FormatException($"'L' ѡ����������Ч��(λ��={i})");
                    }
                    var data = GetSet(type);
                    data.Add(val);
                    i++;
                    return i;
                }

                if (c == 'W')
                {
                    if (type == DayOfMonth)
                    {
                        nearestWeekday = true;
                    }
                    else
                    {
                        throw new FormatException($"'W' ѡ����������Ч�� (λ��={i})");
                    }
                    if (val > 31)
                    {
                        throw new FormatException("'W' ѡ����ڴ��� 31 ��ֵ��һ�����е����������û������");
                    }

                    var data = GetSet(type);
                    data.Add(val);
                    i++;
                    return i;
                }

                if (c == '#')
                {
                    if (type != DayOfWeek)
                    {
                        throw new FormatException($"'#' ѡ����������Ч�� (λ��={i})");
                    }
                    i++;
                    try
                    {
                        nthdayOfWeek = Convert.ToInt32(s[i..], CultureInfo.InvariantCulture);
                        if (nthdayOfWeek is < 1 or > 5)
                        {
                            throw new FormatException("�ܵĵ�n��С��1�����5");
                        }
                    }
                    catch (Exception)
                    {
                        throw new FormatException("1 �� 5 ֮�����ֵ������ڡ�#��ѡ�����");
                    }

                    var data = GetSet(type);
                    data.Add(val);
                    i++;
                    return i;
                }

                if (c == 'C')
                {
                    if (type == DayOfWeek)
                    {

                    }
                    else if (type == DayOfMonth)
                    {

                    }
                    else
                    {
                        throw new FormatException($"'C' ѡ����������Ч��(λ��={i})");
                    }
                    var data = GetSet(type);
                    data.Add(val);
                    i++;
                    return i;
                }

                if (c == '-')
                {
                    i++;
                    c = s[i];
                    int v = Convert.ToInt32(c.ToString(), CultureInfo.InvariantCulture);
                    end = v;
                    i++;
                    if (i >= s.Length)
                    {
                        AddToSet(val, end, 1, type);
                        return i;
                    }
                    c = s[i];
                    if (c >= '0' && c <= '9')
                    {
                        ValueSet vs = GetValue(v, s, i);
                        int v1 = vs.theValue;
                        end = v1;
                        i = vs.pos;
                    }
                    if (i < s.Length && s[i] == '/')
                    {
                        i++;
                        c = s[i];
                        int v2 = Convert.ToInt32(c.ToString(), CultureInfo.InvariantCulture);
                        i++;
                        if (i >= s.Length)
                        {
                            AddToSet(val, end, v2, type);
                            return i;
                        }
                        c = s[i];
                        if (c >= '0' && c <= '9')
                        {
                            ValueSet vs = GetValue(v2, s, i);
                            int v3 = vs.theValue;
                            AddToSet(val, end, v3, type);
                            i = vs.pos;
                            return i;
                        }
                        AddToSet(val, end, v2, type);
                        return i;
                    }
                    AddToSet(val, end, 1, type);
                    return i;
                }

                if (c == '/')
                {
                    if (i + 1 >= s.Length || s[i + 1] == ' ' || s[i + 1] == '\t')
                    {
                        throw new FormatException("\'/\' ��������һ��������");
                    }

                    i++;
                    c = s[i];
                    int v2 = Convert.ToInt32(c.ToString(), CultureInfo.InvariantCulture);
                    i++;
                    if (i >= s.Length)
                    {
                        CheckIncrementRange(v2, type);
                        AddToSet(val, end, v2, type);
                        return i;
                    }
                    c = s[i];
                    if (c >= '0' && c <= '9')
                    {
                        ValueSet vs = GetValue(v2, s, i);
                        int v3 = vs.theValue;
                        CheckIncrementRange(v3, type);
                        AddToSet(val, end, v3, type);
                        i = vs.pos;
                        return i;
                    }
                    throw new FormatException($"������ַ� '{c}' �� '/'");
                }

                AddToSet(val, end, 0, type);
                i++;
                return i;
            }



            /// <summary>
            /// Gets the cron expression string.
            /// </summary>
            /// <value>The cron expression string.</value>
            private static string CronExpressionString;




            /// <summary>
            /// Skips the white space.
            /// </summary>
            /// <param name="i">The i.</param>
            /// <param name="s">The s.</param>
            /// <returns></returns>
            private static int SkipWhiteSpace(int i, string s)
            {
                for (; i < s.Length && (s[i] == ' ' || s[i] == '\t'); i++)
                {
                }

                return i;
            }



            /// <summary>
            /// Finds the next white space.
            /// </summary>
            /// <param name="i">The i.</param>
            /// <param name="s">The s.</param>
            /// <returns></returns>
            private static int FindNextWhiteSpace(int i, string s)
            {
                for (; i < s.Length && (s[i] != ' ' || s[i] != '\t'); i++)
                {
                }

                return i;
            }



            /// <summary>
            /// Adds to set.
            /// </summary>
            /// <param name="val">The val.</param>
            /// <param name="end">The end.</param>
            /// <param name="incr">The incr.</param>
            /// <param name="type">The type.</param>
            private void AddToSet(int val, int end, int incr, int type)
            {
                var data = GetSet(type);

                if (type == Second || type == Minute)
                {
                    if ((val < 0 || val > 59 || end > 59) && val != AllSpecInt)
                    {
                        throw new FormatException("���Ӻ���ֵ�������0��59֮��");
                    }
                }
                else if (type == Hour)
                {
                    if ((val < 0 || val > 23 || end > 23) && val != AllSpecInt)
                    {
                        throw new FormatException("Сʱֵ�������0��23֮��");
                    }
                }
                else if (type == DayOfMonth)
                {
                    if ((val < 1 || val > 31 || end > 31) && val != AllSpecInt
                        && val != NoSpecInt)
                    {
                        throw new FormatException("����ֵ�������1��31֮��");
                    }
                }
                else if (type == Month)
                {
                    if ((val < 1 || val > 12 || end > 12) && val != AllSpecInt)
                    {
                        throw new FormatException("�·�ֵ�������1��12֮��");
                    }
                }
                else if (type == DayOfWeek)
                {
                    if ((val == 0 || val > 7 || end > 7) && val != AllSpecInt
                        && val != NoSpecInt)
                    {
                        throw new FormatException("������ֵ�������1��7֮��");
                    }
                }

                if ((incr == 0 || incr == -1) && val != AllSpecInt)
                {
                    if (val != -1)
                    {
                        data.Add(val);
                    }
                    else
                    {
                        data.Add(NoSpec);
                    }
                    return;
                }

                int startAt = val;
                int stopAt = end;

                if (val == AllSpecInt && incr <= 0)
                {
                    incr = 1;
                    data.Add(AllSpec);
                }

                if (type == Second || type == Minute)
                {
                    if (stopAt == -1)
                    {
                        stopAt = 59;
                    }
                    if (startAt == -1 || startAt == AllSpecInt)
                    {
                        startAt = 0;
                    }
                }
                else if (type == Hour)
                {
                    if (stopAt == -1)
                    {
                        stopAt = 23;
                    }
                    if (startAt == -1 || startAt == AllSpecInt)
                    {
                        startAt = 0;
                    }
                }
                else if (type == DayOfMonth)
                {
                    if (stopAt == -1)
                    {
                        stopAt = 31;
                    }
                    if (startAt == -1 || startAt == AllSpecInt)
                    {
                        startAt = 1;
                    }
                }
                else if (type == Month)
                {
                    if (stopAt == -1)
                    {
                        stopAt = 12;
                    }
                    if (startAt == -1 || startAt == AllSpecInt)
                    {
                        startAt = 1;
                    }
                }
                else if (type == DayOfWeek)
                {
                    if (stopAt == -1)
                    {
                        stopAt = 7;
                    }
                    if (startAt == -1 || startAt == AllSpecInt)
                    {
                        startAt = 1;
                    }
                }
                else if (type == Year)
                {
                    if (stopAt == -1)
                    {
                        stopAt = MaxYear;
                    }
                    if (startAt == -1 || startAt == AllSpecInt)
                    {
                        startAt = 1970;
                    }
                }

                int max = -1;
                if (stopAt < startAt)
                {
                    max = type switch
                    {
                        Second => 60,
                        Minute => 60,
                        Hour => 24,
                        Month => 12,
                        DayOfWeek => 7,
                        DayOfMonth => 31,
                        Year => throw new ArgumentException("��ʼ��ݱ���С��ֹͣ���"),
                        _ => throw new ArgumentException("�������������"),
                    };
                    stopAt += max;
                }

                for (int i = startAt; i <= stopAt; i += incr)
                {
                    if (max == -1)
                    {
                        data.Add(i);
                    }
                    else
                    {
                        int i2 = i % max;
                        if (i2 == 0 && (type == Month || type == DayOfWeek || type == DayOfMonth))
                        {
                            i2 = max;
                        }

                        data.Add(i2);
                    }
                }
            }



            /// <summary>
            /// Gets the set of given type.
            /// </summary>
            /// <param name="type">The type of set to get.</param>
            /// <returns></returns>
            private SortedSet<int> GetSet(int type)
            {
                return type switch
                {
                    Second => seconds,
                    Minute => minutes,
                    Hour => hours,
                    DayOfMonth => daysOfMonth,
                    Month => months,
                    DayOfWeek => daysOfWeek,
                    Year => years,
                    _ => throw new ArgumentOutOfRangeException("CronHelper.CronExpression.GetSet:" + type),
                };
            }



            /// <summary>
            /// Gets the value.
            /// </summary>
            /// <param name="v">The v.</param>
            /// <param name="s">The s.</param>
            /// <param name="i">The i.</param>
            /// <returns></returns>
            private static ValueSet GetValue(int v, string s, int i)
            {
                char c = s[i];
                StringBuilder s1 = new(v.ToString(CultureInfo.InvariantCulture));
                while (c >= '0' && c <= '9')
                {
                    s1.Append(c);
                    i++;
                    if (i >= s.Length)
                    {
                        break;
                    }
                    c = s[i];
                }
                ValueSet val = new();
                if (i < s.Length)
                {
                    val.pos = i;
                }
                else
                {
                    val.pos = i + 1;
                }
                val.theValue = Convert.ToInt32(s1.ToString(), CultureInfo.InvariantCulture);
                return val;
            }



            /// <summary>
            /// Gets the numeric value from string.
            /// </summary>
            /// <param name="s">The string to parse from.</param>
            /// <param name="i">The i.</param>
            /// <returns></returns>
            private static int GetNumericValue(string s, int i)
            {
                int endOfVal = FindNextWhiteSpace(i, s);
                string val = s[i..endOfVal];
                return Convert.ToInt32(val, CultureInfo.InvariantCulture);
            }



            /// <summary>
            /// Gets the month number.
            /// </summary>
            /// <param name="s">The string to map with.</param>
            /// <returns></returns>
            private static int GetMonthNumber(string s)
            {
                return monthMap.TryGetValue(s, out int value) ? value : -1;
            }



            /// <summary>
            /// Gets the day of week number.
            /// </summary>
            /// <param name="s">The s.</param>
            /// <returns></returns>
            private static int GetDayOfWeekNumber(string s)
            {
                if (dayMap.TryGetValue(s, out int value))
                {
                    return value;
                }

                return -1;
            }



            /// <summary>
            /// �ڸ���ʱ��֮���ȡ��һ������ʱ�䡣
            /// </summary>
            /// <param name="afterTimeUtc">��ʼ������ UTC ʱ�䡣</param>
            /// <returns></returns>
            public DateTimeOffset? GetTimeAfter(DateTimeOffset afterTimeUtc)
            {

                // ��ǰ�ƶ�һ���ӣ���Ϊ�������ڼ���ʱ��*֮��*
                afterTimeUtc = afterTimeUtc.AddSeconds(1);

                // CronTrigger ���������
                DateTimeOffset d = CreateDateTimeWithoutMillis(afterTimeUtc);

                // ����Ϊָ��ʱ��
                d = TimeZoneInfo.ConvertTime(d, timeZoneInfo);

                bool gotOne = false;
                //ѭ��ֱ�����Ǽ������һ�Σ����������Ѿ����� endTime
                while (!gotOne)
                {
                    SortedSet<int> st;
                    int t;
                    int sec = d.Second;

                    st = seconds.GetViewBetween(sec, 9999999);
                    if (st.Count > 0)
                    {
                        sec = st.First();
                    }
                    else
                    {
                        sec = seconds.First();
                        d = d.AddMinutes(1);
                    }
                    d = new(d.Year, d.Month, d.Day, d.Hour, d.Minute, sec, d.Millisecond, d.Offset);

                    int min = d.Minute;
                    int hr = d.Hour;
                    t = -1;

                    st = minutes.GetViewBetween(min, 9999999);
                    if (st.Count > 0)
                    {
                        t = min;
                        min = st.First();
                    }
                    else
                    {
                        min = minutes.First();
                        hr++;
                    }
                    if (min != t)
                    {
                        d = new(d.Year, d.Month, d.Day, d.Hour, min, 0, d.Millisecond, d.Offset);
                        d = SetCalendarHour(d, hr);
                        continue;
                    }
                    d = new(d.Year, d.Month, d.Day, d.Hour, min, d.Second, d.Millisecond, d.Offset);

                    hr = d.Hour;
                    int day = d.Day;
                    t = -1;

                    st = hours.GetViewBetween(hr, 9999999);
                    if (st.Count > 0)
                    {
                        t = hr;
                        hr = st.First();
                    }
                    else
                    {
                        hr = hours.First();
                        day++;
                    }
                    if (hr != t)
                    {
                        int daysInMonth = DateTime.DaysInMonth(d.Year, d.Month);
                        if (day > daysInMonth)
                        {
                            d = new DateTimeOffset(d.Year, d.Month, daysInMonth, d.Hour, 0, 0, d.Millisecond, d.Offset).AddDays(day - daysInMonth);
                        }
                        else
                        {
                            d = new(d.Year, d.Month, day, d.Hour, 0, 0, d.Millisecond, d.Offset);
                        }
                        d = SetCalendarHour(d, hr);
                        continue;
                    }
                    d = new(d.Year, d.Month, d.Day, hr, d.Minute, d.Second, d.Millisecond, d.Offset);

                    day = d.Day;
                    int mon = d.Month;
                    t = -1;
                    int tmon = mon;

                    bool dayOfMSpec = !daysOfMonth.Contains(NoSpec);
                    bool dayOfWSpec = !daysOfWeek.Contains(NoSpec);
                    if (dayOfMSpec && !dayOfWSpec)
                    {
                        // ���»�ȡ����
                        st = daysOfMonth.GetViewBetween(day, 9999999);
                        bool found = st.Count != 0;
                        if (lastdayOfMonth)
                        {
                            if (!nearestWeekday)
                            {
                                t = day;
                                day = GetLastDayOfMonth(mon, d.Year);
                                day -= lastdayOffset;

                                if (t > day)
                                {
                                    mon++;
                                    if (mon > 12)
                                    {
                                        mon = 1;
                                        tmon = 3333; // ȷ������� mon != tmon ����ʧ��
                                        d = d.AddYears(1);
                                    }
                                    day = 1;
                                }
                            }
                            else
                            {
                                t = day;
                                day = GetLastDayOfMonth(mon, d.Year);
                                day -= lastdayOffset;

                                DateTimeOffset tcal = new(d.Year, mon, day, 0, 0, 0, d.Offset);

                                int ldom = GetLastDayOfMonth(mon, d.Year);
                                DayOfWeek dow = tcal.DayOfWeek;

                                if (dow == System.DayOfWeek.Saturday && day == 1)
                                {
                                    day += 2;
                                }
                                else if (dow == System.DayOfWeek.Saturday)
                                {
                                    day -= 1;
                                }
                                else if (dow == System.DayOfWeek.Sunday && day == ldom)
                                {
                                    day -= 2;
                                }
                                else if (dow == System.DayOfWeek.Sunday)
                                {
                                    day += 1;
                                }

                                DateTimeOffset nTime = new(tcal.Year, mon, day, hr, min, sec, d.Millisecond, d.Offset);
                                if (nTime.ToUniversalTime() < afterTimeUtc)
                                {
                                    day = 1;
                                    mon++;
                                }
                            }
                        }
                        else if (nearestWeekday)
                        {
                            t = day;
                            day = daysOfMonth.First();

                            DateTimeOffset tcal = new(d.Year, mon, day, 0, 0, 0, d.Offset);

                            int ldom = GetLastDayOfMonth(mon, d.Year);
                            DayOfWeek dow = tcal.DayOfWeek;

                            if (dow == System.DayOfWeek.Saturday && day == 1)
                            {
                                day += 2;
                            }
                            else if (dow == System.DayOfWeek.Saturday)
                            {
                                day -= 1;
                            }
                            else if (dow == System.DayOfWeek.Sunday && day == ldom)
                            {
                                day -= 2;
                            }
                            else if (dow == System.DayOfWeek.Sunday)
                            {
                                day += 1;
                            }

                            tcal = new(tcal.Year, mon, day, hr, min, sec, d.Offset);
                            if (tcal.ToUniversalTime() < afterTimeUtc)
                            {
                                day = daysOfMonth.First();
                                mon++;
                            }
                        }
                        else if (found)
                        {
                            t = day;
                            day = st.First();

                            //ȷ�����ǲ����ڶ�ʱ�����ܵù��죬�������
                            int lastDay = GetLastDayOfMonth(mon, d.Year);
                            if (day > lastDay)
                            {
                                day = daysOfMonth.First();
                                mon++;
                            }
                        }
                        else
                        {
                            day = daysOfMonth.First();
                            mon++;
                        }

                        if (day != t || mon != tmon)
                        {
                            if (mon > 12)
                            {
                                d = new DateTimeOffset(d.Year, 12, day, 0, 0, 0, d.Offset).AddMonths(mon - 12);
                            }
                            else
                            {
                                //����Ϊ�˱����һ�����ƶ�ʱ���ִ���
                                //�� 30 �� 31 �쵽һ���¸��١� ����ʵ������Ч������ʱ�䡣
                                int lDay = DateTime.DaysInMonth(d.Year, mon);
                                if (day <= lDay)
                                {
                                    d = new(d.Year, mon, day, 0, 0, 0, d.Offset);
                                }
                                else
                                {
                                    d = new DateTimeOffset(d.Year, mon, lDay, 0, 0, 0, d.Offset).AddDays(day - lDay);
                                }
                            }
                            continue;
                        }
                    }
                    else if (dayOfWSpec && !dayOfMSpec)
                    {
                        // ��ȡ���ڼ�����
                        if (lastdayOfWeek)
                        {

                            int dow = daysOfWeek.First();

                            int cDow = (int)d.DayOfWeek + 1;
                            int daysToAdd = 0;
                            if (cDow < dow)
                            {
                                daysToAdd = dow - cDow;
                            }
                            if (cDow > dow)
                            {
                                daysToAdd = dow + (7 - cDow);
                            }

                            int lDay = GetLastDayOfMonth(mon, d.Year);

                            if (day + daysToAdd > lDay)
                            {

                                if (mon == 12)
                                {

                                    d = new DateTimeOffset(d.Year, mon - 11, 1, 0, 0, 0, d.Offset).AddYears(1);
                                }
                                else
                                {
                                    d = new(d.Year, mon + 1, 1, 0, 0, 0, d.Offset);
                                }

                                continue;
                            }

                            // ���ұ�����һ�����һ�γ��ֵ�����...
                            while (day + daysToAdd + 7 <= lDay)
                            {
                                daysToAdd += 7;
                            }

                            day += daysToAdd;

                            if (daysToAdd > 0)
                            {
                                d = new(d.Year, mon, day, 0, 0, 0, d.Offset);

                                continue;
                            }
                        }
                        else if (nthdayOfWeek != 0)
                        {

                            int dow = daysOfWeek.First();

                            int cDow = (int)d.DayOfWeek + 1;
                            int daysToAdd = 0;
                            if (cDow < dow)
                            {
                                daysToAdd = dow - cDow;
                            }
                            else if (cDow > dow)
                            {
                                daysToAdd = dow + (7 - cDow);
                            }

                            bool dayShifted = daysToAdd > 0;

                            day += daysToAdd;
                            int weekOfMonth = day / 7;
                            if (day % 7 > 0)
                            {
                                weekOfMonth++;
                            }

                            daysToAdd = (nthdayOfWeek - weekOfMonth) * 7;
                            day += daysToAdd;
                            if (daysToAdd < 0 || day > GetLastDayOfMonth(mon, d.Year))
                            {
                                if (mon == 12)
                                {
                                    d = new DateTimeOffset(d.Year, mon - 11, 1, 0, 0, 0, d.Offset).AddYears(1);
                                }
                                else
                                {
                                    d = new(d.Year, mon + 1, 1, 0, 0, 0, d.Offset);
                                }

                                continue;
                            }
                            if (daysToAdd > 0 || dayShifted)
                            {
                                d = new(d.Year, mon, day, 0, 0, 0, d.Offset);

                                continue;
                            }
                        }
                        else if (everyNthWeek != 0)
                        {
                            int cDow = (int)d.DayOfWeek + 1;
                            int dow = daysOfWeek.First();

                            st = daysOfWeek.GetViewBetween(cDow, 9999999);
                            if (st.Count > 0)
                            {
                                dow = st.First();
                            }

                            int daysToAdd = 0;
                            if (cDow < dow)
                            {
                                daysToAdd = dow - cDow + 7 * (everyNthWeek - 1);
                            }
                            if (cDow > dow)
                            {
                                daysToAdd = dow + (7 - cDow) + 7 * (everyNthWeek - 1);
                            }


                            if (daysToAdd > 0)
                            {
                                d = new(d.Year, mon, day, 0, 0, 0, d.Offset);
                                d = d.AddDays(daysToAdd);
                                continue;
                            }
                        }
                        else
                        {
                            int cDow = (int)d.DayOfWeek + 1;
                            int dow = daysOfWeek.First();

                            st = daysOfWeek.GetViewBetween(cDow, 9999999);
                            if (st.Count > 0)
                            {
                                dow = st.First();
                            }

                            int daysToAdd = 0;
                            if (cDow < dow)
                            {
                                daysToAdd = dow - cDow;
                            }
                            if (cDow > dow)
                            {
                                daysToAdd = dow + (7 - cDow);
                            }

                            int lDay = GetLastDayOfMonth(mon, d.Year);

                            if (day + daysToAdd > lDay)
                            {

                                if (mon == 12)
                                {
                                    d = new DateTimeOffset(d.Year, mon - 11, 1, 0, 0, 0, d.Offset).AddYears(1);
                                }
                                else
                                {
                                    d = new(d.Year, mon + 1, 1, 0, 0, 0, d.Offset);
                                }
                                continue;
                            }
                            if (daysToAdd > 0)
                            {
                                d = new(d.Year, mon, day + daysToAdd, 0, 0, 0, d.Offset);
                                continue;
                            }
                        }
                    }
                    else
                    {
                        throw new FormatException("��֧��ͬʱָ�������պ����ղ�����");
                    }

                    d = new(d.Year, d.Month, day, d.Hour, d.Minute, d.Second, d.Offset);
                    mon = d.Month;
                    int year = d.Year;
                    t = -1;


                    if (year > MaxYear)
                    {
                        return null;
                    }

                    st = months.GetViewBetween(mon, 9999999);
                    if (st.Count > 0)
                    {
                        t = mon;
                        mon = st.First();
                    }
                    else
                    {
                        mon = months.First();
                        year++;
                    }
                    if (mon != t)
                    {
                        d = new(year, mon, 1, 0, 0, 0, d.Offset);
                        continue;
                    }
                    d = new(d.Year, mon, d.Day, d.Hour, d.Minute, d.Second, d.Offset);
                    year = d.Year;

                    st = years.GetViewBetween(year, 9999999);
                    if (st.Count > 0)
                    {
                        t = year;
                        year = st.First();
                    }
                    else
                    {
                        return null;
                    }

                    if (year != t)
                    {
                        d = new(year, 1, 1, 0, 0, 0, d.Offset);
                        continue;
                    }
                    d = new(year, d.Month, d.Day, d.Hour, d.Minute, d.Second, d.Offset);

                    //Ϊ������Ӧ���ʵ���ƫ����
                    d = new(d.DateTime, timeZoneInfo.BaseUtcOffset);

                    gotOne = true;
                }

                return d.ToUniversalTime();
            }



            /// <summary>
            /// Creates the date time without milliseconds.
            /// </summary>
            /// <param name="time">The time.</param>
            /// <returns></returns>
            private static DateTimeOffset CreateDateTimeWithoutMillis(DateTimeOffset time)
            {
                return new DateTimeOffset(time.Year, time.Month, time.Day, time.Hour, time.Minute, time.Second, time.Offset);
            }



            /// <summary>
            /// Advance the calendar to the particular hour paying particular attention
            /// to daylight saving problems.
            /// </summary>
            /// <param name="date">The date.</param>
            /// <param name="hour">The hour.</param>
            /// <returns></returns>
            private static DateTimeOffset SetCalendarHour(DateTimeOffset date, int hour)
            {

                int hourToSet = hour;
                if (hourToSet == 24)
                {
                    hourToSet = 0;
                }
                DateTimeOffset d = new(date.Year, date.Month, date.Day, hourToSet, date.Minute, date.Second, date.Millisecond, date.Offset);
                if (hour == 24)
                {
                    d = d.AddDays(1);
                }
                return d;
            }



            /// <summary>
            /// Gets the last day of month.
            /// </summary>
            /// <param name="monthNum">The month num.</param>
            /// <param name="year">The year.</param>
            /// <returns></returns>
            private static int GetLastDayOfMonth(int monthNum, int year)
            {
                return DateTime.DaysInMonth(year, monthNum);
            }


            private class ValueSet
            {
                public int theValue;

                public int pos;
            }

        }

    }

}