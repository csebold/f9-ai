using System;
using System.Threading;
using System.Threading.Tasks;

namespace ChatClient.Services;

internal sealed class FallbackLlmClient : ILlmClient
{
    private readonly string _message;

    public FallbackLlmClient(string message)
    {
        _message = message;
    }

    public Task<string> GetResponseAsync(string prompt, CancellationToken cancellationToken = default)
        => Task.FromResult(_message);
}
