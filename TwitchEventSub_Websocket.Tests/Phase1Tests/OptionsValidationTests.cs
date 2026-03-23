using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Twitch.EventSub;
using Xunit;

namespace TwitchEventSub_Websocket.Tests.Phase1Tests;

public class OptionsValidationTests
{
    [Fact]
    public void ClientId_Empty_FailsValidation()
    {
        var services = new ServiceCollection();
        services.AddOptions<EventSubClientOptions>()
            .Configure(o => { o.ClientId = ""; o.AppAccessToken = "valid"; })
            .ValidateDataAnnotations()
            .ValidateOnStart();
        var provider = services.BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<EventSubClientOptions>>().Value);
    }

    [Fact]
    public void AppAccessToken_Missing_FailsValidation()
    {
        var services = new ServiceCollection();
        services.AddOptions<EventSubClientOptions>()
            .Configure(o => { o.ClientId = "valid"; o.AppAccessToken = ""; })
            .ValidateDataAnnotations()
            .ValidateOnStart();
        var provider = services.BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<EventSubClientOptions>>().Value);
    }

    [Fact]
    public void KeepaliveSeconds_OutOfRange_FailsValidation()
    {
        var services = new ServiceCollection();
        services.AddOptions<EventSubClientOptions>()
            .Configure(o => { o.ClientId = "x"; o.AppAccessToken = "y"; o.KeepaliveTimeoutSeconds = 5; })
            .ValidateDataAnnotations()
            .ValidateOnStart();
        var provider = services.BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<EventSubClientOptions>>().Value);
    }

    [Fact]
    public void ValidOptions_PassesValidation()
    {
        var services = new ServiceCollection();
        services.AddOptions<EventSubClientOptions>()
            .Configure(o => { o.ClientId = "myClientId"; o.AppAccessToken = "myAppToken"; })
            .ValidateDataAnnotations()
            .ValidateOnStart();
        var provider = services.BuildServiceProvider();
        var opts = provider.GetRequiredService<IOptions<EventSubClientOptions>>().Value;
        Assert.Equal("myClientId", opts.ClientId);
        Assert.Equal("myAppToken", opts.AppAccessToken);
    }
}
