using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Twitch.EventSub;
using Xunit;

namespace TwitchEventSub_Websocket.Tests.Phase8Tests;

public class RedundancyOptionsTests
{
    [Fact]
    public void RedundancyFactor_ExceedingMaxConduits_FailsValidation()
    {
        var services = new ServiceCollection();
        services.AddOptions<EventSubClientOptions>()
            .Configure(o => { o.ClientId = "x"; o.AppAccessToken = "y"; o.MaxConduits = 2; o.RedundancyFactor = 3; })
            .ValidateDataAnnotations()
            .Validate(o => o.RedundancyFactor <= o.MaxConduits, "RedundancyFactor must be <= MaxConduits")
            .ValidateOnStart();
        var provider = services.BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<EventSubClientOptions>>().Value);
    }

    [Fact]
    public void RedundancyFactor_WithinLimits_Passes()
    {
        var services = new ServiceCollection();
        services.AddOptions<EventSubClientOptions>()
            .Configure(o => { o.ClientId = "x"; o.AppAccessToken = "y"; o.MaxConduits = 3; o.RedundancyFactor = 2; })
            .ValidateDataAnnotations()
            .Validate(o => o.RedundancyFactor <= o.MaxConduits, "RedundancyFactor must be <= MaxConduits")
            .ValidateOnStart();
        var provider = services.BuildServiceProvider();
        Assert.Equal(2, provider.GetRequiredService<IOptions<EventSubClientOptions>>().Value.RedundancyFactor);
    }
}
