using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Twitch.EventSub.APIConduit;
using Xunit;

namespace TwitchEventSub_Websocket.Tests.Phase7Tests;

public class ConduitApiHeaderTests
{
    private sealed class CaptureHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Last;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Last = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(@"{ ""data"": [ { ""id"": ""c1"", ""shard_count"": 1 } ] }") });
        }
    }

    [Fact]
    public async Task GetConduitIds_SetsPerRequestHeaders_NotSharedDefaults()
    {
        var handler = new CaptureHandler();
        var client = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);
        var api = new TwitchApiConduit(factory.Object);

        await api.GetConduitIdsAsync("apptoken", "clientid", CancellationToken.None);

        Assert.Equal("Bearer", handler.Last!.Headers.Authorization!.Scheme);
        Assert.Equal("apptoken", handler.Last.Headers.Authorization.Parameter);
        Assert.Null(client.DefaultRequestHeaders.Authorization);
    }
}
