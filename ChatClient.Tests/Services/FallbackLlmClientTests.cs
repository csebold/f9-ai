using System.Threading.Tasks;
using ChatClient.Services;
using Xunit;

namespace ChatClient.Tests.Services;

public class FallbackLlmClientTests
{
    [Fact]
    public async Task GetResponseAsync_ReturnsConfiguredMessage()
    {
        var client = new FallbackLlmClient("offline");

        var response = await client.GetResponseAsync("ignored");

        Assert.Equal("offline", response);
    }
}
