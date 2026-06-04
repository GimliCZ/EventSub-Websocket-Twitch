using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Twitch.EventSub;
using Xunit;

namespace TwitchEventSub_Websocket.Tests.Phase8Tests;

public class AddTwitchEventSubValidationTests
{
    [Fact]
    public void IServiceProviderOverload_InvalidRedundancy_FailsValidation()
    {
        var services = new ServiceCollection();
        services.AddTwitchEventSub((sp, o) =>
        {
            o.ClientId = "x"; o.AppAccessToken = "y"; o.MaxConduits = 2; o.RedundancyFactor = 3;
        });
        var provider = services.BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<EventSubClientOptions>>().Value);
    }

    [Fact]
    public void IServiceProviderOverload_MissingClientId_FailsValidation()
    {
        var services = new ServiceCollection();
        services.AddTwitchEventSub((sp, o) =>
        {
            o.ClientId = ""; o.AppAccessToken = "y";
        });
        var provider = services.BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<EventSubClientOptions>>().Value);
    }

    [Fact]
    public void IServiceProviderOverload_Valid_Passes()
    {
        var services = new ServiceCollection();
        services.AddTwitchEventSub((sp, o) =>
        {
            o.ClientId = "cid"; o.AppAccessToken = "tok"; o.MaxConduits = 3; o.RedundancyFactor = 2;
        });
        var provider = services.BuildServiceProvider();
        var opts = provider.GetRequiredService<IOptions<EventSubClientOptions>>().Value;
        Assert.Equal("cid", opts.ClientId);
        Assert.Equal(2, opts.RedundancyFactor);
    }
}
