namespace Application.Service.LLM;

public interface ILlmToolProvider
{
    IReadOnlyList<LlmAgentTool> GetTools();
}
