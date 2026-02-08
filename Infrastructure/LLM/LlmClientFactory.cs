using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace LLM;

public sealed class LlmClientFactory(IServiceProvider serviceProvider) : ILlmClientFactory
{
    public ILlmClient GetClient(string provider)
    {
        if (!TryGetClient(provider, out var client))
        {
            throw new InvalidOperationException($"No ILlmClient registered for provider '{provider}'.");
        }

        return client;
    }

    public bool TryGetClient(string provider, [NotNullWhen(true)] out ILlmClient? client)
    {
        client = serviceProvider.GetKeyedService<ILlmClient>(provider);
        return client != null;
    }
}
