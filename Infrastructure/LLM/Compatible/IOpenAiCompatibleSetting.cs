namespace LLM.Compatible;

public interface IOpenAiCompatibleSetting
{
    string BaseUrl { get; set; }

    string ApiKey { get; set; }

    string? DefaultModel { get; set; }

    TimeSpan Timeout { get; set; }
}

