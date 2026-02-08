using System.Diagnostics.CodeAnalysis;

namespace LLM;

public interface ILlmClientFactory
{
    ILlmClient GetClient(string provider);

    bool TryGetClient(string provider, [NotNullWhen(true)] out ILlmClient? client);
}
