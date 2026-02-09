namespace LLM.Compatible;

public interface IOpenAiCompatibleSetting
{
    string BaseUrl { get; set; }

    string ApiKey { get; set; }
}
