using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Newtonsoft.Json;
using System.Net;
using Twitch.EventSub.API;
using Twitch.EventSub.API.Enums;
using Twitch.EventSub.API.Models;
using Xunit;

namespace TwitchEventSub_Websocket.Tests.Phase7Tests;

public class CursorPaginationTests
{
    [Fact]
    public void Deserialize_ReadsCursorFromPaginationObject()
    {
        var json = @"{ ""total"": 2, ""data"": [], ""pagination"": { ""cursor"": ""abc123"" } }";
        var resp = JsonConvert.DeserializeObject<GetSubscriptionsResponse>(json);
        Assert.NotNull(resp);
        Assert.Equal("abc123", resp!.Pagination.Cursor);
    }

    [Fact]
    public void Deserialize_EmptyPagination_CursorIsNullOrEmpty()
    {
        var json = @"{ ""total"": 0, ""data"": [], ""pagination"": {} }";
        var resp = JsonConvert.DeserializeObject<GetSubscriptionsResponse>(json);
        Assert.NotNull(resp);
        Assert.True(string.IsNullOrEmpty(resp!.Pagination.Cursor));
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode code, string body)> _responses;
        public StubHandler(Queue<(HttpStatusCode, string)> responses) => _responses = responses;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var (code, body) = _responses.Dequeue();
            return Task.FromResult(new HttpResponseMessage(code) { Content = new StringContent(body) });
        }
    }

    private static HttpClient FakeSequenceClient(params (HttpStatusCode code, string body)[] responses)
        => new HttpClient(new StubHandler(new Queue<(HttpStatusCode, string)>(responses)));

    [Fact]
    public async Task GetAllSubscriptions_FollowsCursorAcrossPages()
    {
        var page1 = @"{ ""total"": 2, ""data"": [ { ""id"": ""s1"", ""type"": ""channel.update"", ""version"": ""2"", ""condition"": { ""broadcaster_user_id"": ""1"" } } ], ""pagination"": { ""cursor"": ""next"" } }";
        var page2 = @"{ ""total"": 2, ""data"": [ { ""id"": ""s2"", ""type"": ""channel.follow"", ""version"": ""2"", ""condition"": { ""broadcaster_user_id"": ""1"" } } ], ""pagination"": {} }";

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>()))
               .Returns(FakeSequenceClient((HttpStatusCode.OK, page1), (HttpStatusCode.OK, page2)));
        var api = new TwitchApi(factory.Object);
        using var cts = new CancellationTokenSource();

        var all = await api.GetAllSubscriptionsAsync("cid", "tok", cts, NullLogger.Instance, SubscriptionStatusTypes.Empty);

        var ids = all.SelectMany(r => r.Data).Select(d => d.Id).ToList();
        Assert.Equal(new[] { "s1", "s2" }, ids);
    }

    [Fact]
    public async Task Validate_SetsPerRequestHeaders_NotSharedDefaults()
    {
        HttpRequestMessage? captured = null;
        var handler = new CaptureHandler(req => { captured = req; return (HttpStatusCode.OK, "{}"); });
        var client = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);
        var api = new TwitchApi(factory.Object);
        using var cts = new CancellationTokenSource();

        await api.ValidateTokenAsync("usertoken", cts, NullLogger.Instance);

        Assert.NotNull(captured);
        Assert.Equal("OAuth", captured!.Headers.Authorization!.Scheme);
        Assert.Equal("usertoken", captured.Headers.Authorization.Parameter);
        Assert.Null(client.DefaultRequestHeaders.Authorization);
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, (HttpStatusCode, string)> _fn;
        public CaptureHandler(Func<HttpRequestMessage, (HttpStatusCode, string)> fn) => _fn = fn;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var (code, body) = _fn(request);
            return Task.FromResult(new HttpResponseMessage(code) { Content = new StringContent(body) });
        }
    }
}
