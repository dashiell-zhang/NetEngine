using NPOI.SS.Util;
using SourceGenerator.Runtime.Attributes;
using System;
using System.ComponentModel;

namespace Client.WebAPI.Services;


[RegisterService(Lifetime = ServiceLifetime.Scoped)]
public class Demo3Service
{

    public sealed record ToLowerArgs(string Text);
    public sealed record ToLowerResult(string Text);


    public sealed class UserInfo
    {
        [Description("出生日期，格式 yyyy-MM-dd，例如 2000-01-01")]
        public string Birthday { get; set; } = string.Empty;

        [Description("性别，只能填写 男 或 女")]
        public string Sex { get; set; } = string.Empty;
    }

    [LlmTool("get_age_by_info")]
    [Description("根据用户信息计算年龄")]
    public int GetAgeByInfo(UserInfo info)
    {
        var birthday = DateTime.Parse(info.Birthday);
        var age = DateTime.Now.Year - birthday.Year;
        return age;
    }



    [LlmTool("get_age_by_fields")]
    [Description("根据出生日期和性别计算当前年龄")]
    public int GetAgeByFields(
        [Description("出生日期，格式为 yyyy-MM-dd，例如 2001-05-21")]
        string birthday,
        [Description("性别，只能填写 男 或 女")]
        string sex)
    {
        var date = DateTime.Parse(birthday);
        var age = DateTime.Now.Year - date.Year;
        return age;
    }


    [LlmTool("get_age", "传入生日和性别计算年龄")]
    [Description("根据出生日期和性别计算当前年龄")]
    public Task<string> GetAge(
        [Description("出生日期，格式 yyyy-MM-dd，例如 2000-01-01")]
        DateOnly birthday,
        [Description("性别，只能填写 男 或 女")]
        string sex)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        // 基础年龄 = 年份差
        int age = today.Year - birthday.Year;

        // 如果今年生日还没到，需要减 1
        var thisYearBirthday = new DateOnly(today.Year, birthday.Month, birthday.Day);
        if (today < thisYearBirthday)
        {
            age--;
        }

        // 防御：未来日期
        if (age < 0)
        {
            return Task.FromResult("生日无效");
        }

        string result = $"{sex}，今年 {age} 岁";

        return Task.FromResult(result);
    }



    [LlmTool("sum", "计算 a + b")]
    public Task<double> SumAsync(double a, double b, CancellationToken cancellationToken)
    {
        Console.WriteLine(111);
        return Task.FromResult(a + b);
    }

    [LlmTool("to_upper", "把 text 转成大写")]
    public Task<string> ToUpperAsync(string text, CancellationToken cancellationToken)
    {
        Console.WriteLine(222);
        return Task.FromResult(text.ToUpperInvariant());
    }

    [LlmTool("to_lower", "把 text 转成小写")]
    public Task<ToLowerResult> ToLowerAsync(ToLowerArgs args, CancellationToken cancellationToken)
    {
        Console.WriteLine(333);
        return Task.FromResult(new ToLowerResult((args.Text ?? string.Empty).ToLowerInvariant()));
    }

}
