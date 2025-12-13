using Twitch.EventSub.Messages.NotificationMessage;
using Twitch.EventSub.Messages.NotificationMessage.Events;
using Twitch.EventSub.Messages.NotificationMessage.Events.ChannelCharity;
using Twitch.EventSub.Messages.NotificationMessage.Events.ChannelChat;
using Twitch.EventSub.Messages.NotificationMessage.Events.ChannelCheer;
using Twitch.EventSub.Messages.NotificationMessage.Events.ChannelGoal;
using Twitch.EventSub.Messages.NotificationMessage.Events.ChannelGuest;
using Twitch.EventSub.Messages.NotificationMessage.Events.ChannelHype;
using Twitch.EventSub.Messages.NotificationMessage.Events.ChannelModerator;
using Twitch.EventSub.Messages.NotificationMessage.Events.ChannelPoints;
using Twitch.EventSub.Messages.NotificationMessage.Events.ChannelPoll;
using Twitch.EventSub.Messages.NotificationMessage.Events.ChannelPrediction;
using Twitch.EventSub.Messages.NotificationMessage.Events.ChannelShield;
using Twitch.EventSub.Messages.NotificationMessage.Events.ChannelShoutout;
using Twitch.EventSub.Messages.NotificationMessage.Events.ChannelSubscription;
using Twitch.EventSub.Messages.NotificationMessage.Events.ChannelUnban;
using Twitch.EventSub.Messages.NotificationMessage.Events.ChannelVIP;
using Twitch.EventSub.Messages.NotificationMessage.Events.ChannelWarning;
using Twitch.EventSub.Messages.NotificationMessage.Events.Stream;
using Twitch.EventSub.User;

namespace TwitchEventSub_Websocket.Tests.NotificationsTests
{
    public class NotificationsProcessingTests
    {
        private readonly string AddPath = Path.Combine("NotificationsTests","NotificationsMessages");

        [Fact]
        public async Task MessageProcessing_IsConduitShardsDisabled()
        {
            var messageString = await HelperFunctions.LoadNotificationAsync(
                "conduit.shard.disabled",
                "1",
                AddPath,
                "ConduitShardDisabled.json"
                );
            // Act
            var result = await MessageProcessing.DeserializeMessageAsync(messageString);
            // Assert
            Assert.NotNull(result);
            Assert.IsType<WebSocketNotificationMessage>(result);
            var notificationMessage = (WebSocketNotificationMessage)result;
            var Event = notificationMessage?.Payload?.Event;
            Assert.IsType<ConduitShardDisabledEvent>(Event);
            var adbreak = (ConduitShardDisabledEvent)Event;
            Assert.Equal("bfcfc993-26b1-b876-44d9-afe75a379dac", adbreak.ConduitId);
            Assert.Equal("befa7b53-d79d-478f-86b9-120f112b044e", notificationMessage.Metadata.MessageId);
            Assert.Equal("webhook", notificationMessage?.Payload?.Subscription.Transport.Method);
            Assert.Equal("uo6dggojyb8d6soh92zknwmi5ej1q2", notificationMessage?.Payload?.Subscription?.Condition.ClientId);
        }

        //drop.entitlement.grant webhook only
        //extension.bits_transaction.create webhook only

        [Fact]
        public async Task MessageProcessing_IsAddBreakBegin()
        {
            string messageString = await HelperFunctions.LoadNotificationAsync(
                "channel.ad_break.begin",
                "1",
                AddPath,
                "ChannelAdBreakBegin.json"
                );
            // Act
            var result = await MessageProcessing.DeserializeMessageAsync(messageString);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<WebSocketNotificationMessage>(result);
            var notificationMessage = (WebSocketNotificationMessage)result;
            var Event = notificationMessage?.Payload?.Event;
            Assert.IsType<ChannelAdBreakBeginEvent>(Event);
            var adbreak = (ChannelAdBreakBeginEvent)Event;
            Assert.Equal("60", adbreak.DurationSeconds);
            Assert.Equal("befa7b53-d79d-478f-86b9-120f112b044e", notificationMessage.Metadata.MessageId);
            Assert.Equal("webhook", notificationMessage?.Payload?.Subscription.Transport.Method);
            Assert.Equal("1337", notificationMessage?.Payload?.Subscription.Condition.BroadcasterUserId);
        }

        [Fact]
        public async Task MessageProcessing_IsChannelBan()
        {
            string messageString = await HelperFunctions.LoadNotificationAsync(
                "channel.ban",
                "1",
                AddPath,
                "ChannelBan.json"
                );
            // Act
            var result = await MessageProcessing.DeserializeMessageAsync(messageString);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<WebSocketNotificationMessage>(result);
            var notificationMessage = (WebSocketNotificationMessage)result;
            var Event = notificationMessage?.Payload?.Event;
            Assert.IsType<ChannelBanEvent>(Event);
            var ban = (ChannelBanEvent)Event;
            Assert.False(ban.IsPermanent);
            Assert.Equal("befa7b53-d79d-478f-86b9-120f112b044e", notificationMessage.Metadata.MessageId);
            Assert.Equal("webhook", notificationMessage?.Payload?.Subscription.Transport.Method);
            Assert.Equal("1337", notificationMessage?.Payload?.Subscription.Condition.BroadcasterUserId);
        }

        [Fact]
        public async Task MessageProcessing_IsChannelFollow()
        {
            string messageString = await HelperFunctions.LoadNotificationAsync(
                "channel.follow",
                "2",
                AddPath,
                "ChannelFollow.json"
                );
            // Act
            var result = await MessageProcessing.DeserializeMessageAsync(messageString);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<WebSocketNotificationMessage>(result);
            var notificationMessage = (WebSocketNotificationMessage)result;
            var Event = notificationMessage?.Payload?.Event;
            Assert.IsType<ChannelFollowEvent>(Event);
            var follow = (ChannelFollowEvent)Event;
            Assert.Equal("1234", follow.UserId);
            Assert.Equal("befa7b53-d79d-478f-86b9-120f112b044e", notificationMessage.Metadata.MessageId);
            Assert.Equal("webhook", notificationMessage?.Payload?.Subscription.Transport.Method);
            Assert.Equal("1337", notificationMessage?.Payload?.Subscription.Condition.BroadcasterUserId);
        }

        [Fact]
        public async Task MessageProcessing_IsChannelGoalBegin()
        {
            string messageString = await HelperFunctions.LoadNotificationAsync(
                "channel.goal.begin",
                "1",
                AddPath,
                "ChannelGoalBegin.json"
                );
            // Act
            var result = await MessageProcessing.DeserializeMessageAsync(messageString);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<WebSocketNotificationMessage>(result);
            var notificationMessage = (WebSocketNotificationMessage)result;
            var Event = notificationMessage?.Payload?.Event;
            Assert.IsType<ChannelGoalBeginEvent>(Event);
            var goalBegin = (ChannelGoalBeginEvent)Event;
            Assert.Equal("Help me get partner!", goalBegin.Description);
            Assert.Equal("befa7b53-d79d-478f-86b9-120f112b044e", notificationMessage.Metadata.MessageId);
            Assert.Equal("webhook", notificationMessage?.Payload?.Subscription.Transport.Method);
            Assert.Equal("141981764", notificationMessage?.Payload?.Subscription.Condition.BroadcasterUserId);
        }

        [Fact]
        public async Task MessageProcessing_IsChannelGoalEnd()
        {
            string messageString = await HelperFunctions.LoadNotificationAsync(
                "channel.goal.end",
                "1",
                AddPath,
                "ChannelGoalEnd.json"
                );
            // Act
            var result = await MessageProcessing.DeserializeMessageAsync(messageString);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<WebSocketNotificationMessage>(result);
            var notificationMessage = (WebSocketNotificationMessage)result;
            var Event = notificationMessage?.Payload?.Event;
            Assert.IsType<ChannelGoalEndEvent>(Event);
            var goalEnd = (ChannelGoalEndEvent)Event;
            Assert.Equal("Help me get partner!", goalEnd.Description);
            Assert.Equal("2020-07-16T17:16:03.17106713Z", goalEnd.EndedAt);
            Assert.Equal("befa7b53-d79d-478f-86b9-120f112b044e", notificationMessage.Metadata.MessageId);
            Assert.Equal("webhook", notificationMessage?.Payload?.Subscription.Transport.Method);
            Assert.Equal("141981764", notificationMessage?.Payload?.Subscription.Condition.BroadcasterUserId);
        }

        [Fact]
        public async Task MessageProcessing_IsChannelGoalProgress()
        {
            string messageString = await HelperFunctions.LoadNotificationAsync(
                "channel.goal.progress",
                "1",
                AddPath,
                "ChannelGoalProgress.json"
                );
            // Act
            var result = await MessageProcessing.DeserializeMessageAsync(messageString);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<WebSocketNotificationMessage>(result);
            var notificationMessage = (WebSocketNotificationMessage)result;
            var Event = notificationMessage?.Payload?.Event;
            Assert.IsType<ChannelGoalProgressEvent>(Event);
            var goalProgress = (ChannelGoalProgressEvent)Event;
            Assert.Equal("Help me get partner!", goalProgress.Description);
            Assert.Equal("befa7b53-d79d-478f-86b9-120f112b044e", notificationMessage.Metadata.MessageId);
            Assert.Equal("webhook", notificationMessage?.Payload?.Subscription.Transport.Method);
            Assert.Equal("141981764", notificationMessage?.Payload?.Subscription.Condition.BroadcasterUserId);
        }

        [Fact]
        public async Task MessageProcessing_IsChannelGuestStarGuestUpdate()
        {
            string messageString = await HelperFunctions.LoadNotificationAsync(
                "channel.guest_star_guest.update",
                "beta",
                AddPath,
                "ChannelGuestStarGuestUpdate.json"
                );
            // Act
            var result = await MessageProcessing.DeserializeMessageAsync(messageString);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<WebSocketNotificationMessage>(result);
            var notificationMessage = (WebSocketNotificationMessage)result;
            var Event = notificationMessage?.Payload?.Event;
            Assert.IsType<ChannelGuestStarGuestUpdateEvent>(Event);
            var guestUpdate = (ChannelGuestStarGuestUpdateEvent)Event;
            Assert.Equal("1", guestUpdate.SlotId);
            Assert.Equal(100, guestUpdate.HostVolume);
            Assert.Equal("befa7b53-d79d-478f-86b9-120f112b044e", notificationMessage.Metadata.MessageId);
            Assert.Equal("webhook", notificationMessage?.Payload?.Subscription.Transport.Method);
            Assert.Equal("1337", notificationMessage?.Payload?.Subscription.Condition.BroadcasterUserId);
        }

        [Fact]
        public async Task MessageProcessing_IsChannelGuestStarSessionBegin()
        {
            string messageString = await HelperFunctions.LoadNotificationAsync(
                "channel.guest_start_session.begin",
                "beta",
                AddPath,
                "ChannelGuestStarSessionBegin.json"
                );
            // Act
            var result = await MessageProcessing.DeserializeMessageAsync(messageString);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<WebSocketNotificationMessage>(result);
            var notificationMessage = (WebSocketNotificationMessage)result;
            var Event = notificationMessage?.Payload?.Event;
            Assert.IsType<ChannelGuestStarSessionBeginEvent>(Event);
            var guestSessionBegin = (ChannelGuestStarSessionBeginEvent)Event;
            Assert.Equal("2KFRQbFtpmfyD3IevNRnCzOPRJI", guestSessionBegin.SessionId);
            Assert.Equal("befa7b53-d79d-478f-86b9-120f112b044e", notificationMessage.Metadata.MessageId);
            Assert.Equal("webhook", notificationMessage?.Payload?.Subscription.Transport.Method);
            Assert.Equal("1337", notificationMessage?.Payload?.Subscription.Condition.BroadcasterUserId);
        }

        [Fact]
        public async Task MessageProcessing_IsChannelGuestStarSessionEnd()
        {
            string messageString = await HelperFunctions.LoadNotificationAsync(
                "channel.guest_star_session.end",
                "beta",
                AddPath,
                "ChannelGuestStarSessionEnd.json"
                );
            // Act
            var result = await MessageProcessing.DeserializeMessageAsync(messageString);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<WebSocketNotificationMessage>(result);
            var notificationMessage = (WebSocketNotificationMessage)result;
            var Event = notificationMessage?.Payload?.Event;
            Assert.IsType<ChannelGuestStarSessionEndEvent>(Event);
            var guestSessionEnd = (ChannelGuestStarSessionEndEvent)Event;
            Assert.Equal("2KFRQbFtpmfyD3IevNRnCzOPRJI", guestSessionEnd.SessionId);
            Assert.Equal("2023-04-11T17:51:29.153485Z", guestSessionEnd.EndedAt);
            Assert.Equal("befa7b53-d79d-478f-86b9-120f112b044e", notificationMessage.Metadata.MessageId);
            Assert.Equal("webhook", notificationMessage?.Payload?.Subscription.Transport.Method);
            Assert.Equal("1337", notificationMessage?.Payload?.Subscription.Condition.BroadcasterUserId);
        }

        [Fact]
        public async Task MessageProcessing_IsChannelGuestStarSettingsUpdate()
        {
            string messageString = await HelperFunctions.LoadNotificationAsync(
                "channel.guest_star_session.update",
                "beta",
                AddPath,
                "ChannelGuestStarSettingsUpdate.json"
                );
            // Act
            var result = await MessageProcessing.DeserializeMessageAsync(messageString);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<WebSocketNotificationMessage>(result);
            var notificationMessage = (WebSocketNotificationMessage)result;
            var Event = notificationMessage?.Payload?.Event;
            Assert.IsType<ChannelGuestStarSettingsUpdateEvent>(Event);
            var guestSettingsUpdate = (ChannelGuestStarSettingsUpdateEvent)Event;
            Assert.True(guestSettingsUpdate.IsModeratorSendLiveEnabled);
            Assert.Equal("befa7b53-d79d-478f-86b9-120f112b044e", notificationMessage.Metadata.MessageId);
            Assert.Equal("webhook", notificationMessage?.Payload?.Subscription.Transport.Method);
            Assert.Equal("1337", notificationMessage?.Payload?.Subscription.Condition.BroadcasterUserId);
        }

        [Fact]
        public async Task MessageProcessing_IsChannelHypeTrainBegin()
        {
            string messageString = await HelperFunctions.LoadNotificationAsync(
                "channel.hype_train.begin",
                "1",
                AddPath,
                "ChannelHypeTrainBegin.json"
                );
            // Act
            var result = await MessageProcessing.DeserializeMessageAsync(messageString);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<WebSocketNotificationMessage>(result);
            var notificationMessage = (WebSocketNotificationMessage)result;
            var Event = notificationMessage?.Payload?.Event;
            Assert.IsType<ChannelHypeTrainBeginEvent>(Event);
            var hypeBegin = (ChannelHypeTrainBeginEvent)Event;
            Assert.Equal("pogchamp", hypeBegin.TopContributions.FirstOrDefault().UserLogin);
            Assert.Equal("pogchamp", hypeBegin.LastContribution.UserLogin);
            Assert.Equal(2, hypeBegin.Level);
            Assert.Equal("befa7b53-d79d-478f-86b9-120f112b044e", notificationMessage.Metadata.MessageId);
            Assert.Equal("webhook", notificationMessage?.Payload?.Subscription.Transport.Method);
            Assert.Equal("1337", notificationMessage?.Payload?.Subscription.Condition.BroadcasterUserId);
        }

        [Fact]
        public async Task MessageProcessing_IsChannelHypeTrainEnd()
        {
            string messageString = await HelperFunctions.LoadNotificationAsync(
                "channel.hype_train.end",
                "1",
                AddPath,
                "ChannelHypeTrainEnd.json"
                );
            // Act
            var result = await MessageProcessing.DeserializeMessageAsync(messageString);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<WebSocketNotificationMessage>(result);
            var notificationMessage = (WebSocketNotificationMessage)result;
            var Event = notificationMessage?.Payload?.Event;
            Assert.IsType<ChannelHypeTrainEndEvent>(Event);
            var hypeEnd = (ChannelHypeTrainEndEvent)Event;
            Assert.Equal("pogchamp", hypeEnd.TopContributions.FirstOrDefault().UserLogin);
            Assert.Equal(2, hypeEnd.Level);
            Assert.Equal("2020-07-15T17:16:11.17106713Z", hypeEnd.EndedAt);
            Assert.Equal("befa7b53-d79d-478f-86b9-120f112b044e", notificationMessage.Metadata.MessageId);
            Assert.Equal("webhook", notificationMessage?.Payload?.Subscription.Transport.Method);
            Assert.Equal("1337", notificationMessage?.Payload?.Subscription.Condition.BroadcasterUserId);
        }

        [Fact]
        public async Task MessageProcessing_IsChannelHypeTrainProgress()
        {
            string messageString = await HelperFunctions.LoadNotificationAsync(
                "channel.hype_train.progress",
                "1",
                AddPath,
                "ChannelHypeTrainProgress.json"
                );
            // Act
            var result = await MessageProcessing.DeserializeMessageAsync(messageString);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<WebSocketNotificationMessage>(result);
            var notificationMessage = (WebSocketNotificationMessage)result;
            var Event = notificationMessage?.Payload?.Event;
            Assert.IsType<ChannelHypeTrainProgressEvent>(Event);
            var hypeEnd = (ChannelHypeTrainProgressEvent)Event;
            Assert.Equal("pogchamp", hypeEnd.TopContributions.FirstOrDefault().UserLogin);
            Assert.Equal("pogchamp", hypeEnd.LastContribution.UserLogin);
            Assert.Equal(2, hypeEnd.Level);
            Assert.Equal(200, hypeEnd.Progress);
            Assert.Equal("befa7b53-d79d-478f-86b9-120f112b044e", notificationMessage.Metadata.MessageId);
            Assert.Equal("webhook", notificationMessage?.Payload?.Subscription.Transport.Method);
            Assert.Equal("1337", notificationMessage?.Payload?.Subscription.Condition.BroadcasterUserId);
        }

        [Fact]
        public async Task MessageProcessing_IsChannelCampaignDonate()
        {
            string messageString = await HelperFunctions.LoadNotificationAsync(
                "channel.charity_campaign.donate",
                "1",
                AddPath,
                "ChannelCharityCampaignDonate.json"
                );
            // Act
            var result = await MessageProcessing.DeserializeMessageAsync(messageString);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<WebSocketNotificationMessage>(result);
            var notificationMessage = (WebSocketNotificationMessage)result;
            var Event = notificationMessage?.Payload?.Event;
            Assert.IsType<ChannelCharityDonationEvent>(Event);
            var charityDonation = (ChannelCharityDonationEvent)Event;
            Assert.Equal("123-abc-456-def", charityDonation.CampaignId);
            Assert.Equal(10000, charityDonation.Amount.Value);
            Assert.Equal("befa7b53-d79d-478f-86b9-120f112b044e", notificationMessage.Metadata.MessageId);
            Assert.Equal("webhook", notificationMessage?.Payload?.Subscription.Transport.Method);
            Assert.Equal("123456", notificationMessage?.Payload?.Subscription.Condition.BroadcasterUserId);
        }

        [Fact]
        public async Task MessageProcessing_IsChannelCampaignProgress()
        {
            string messageString = await HelperFunctions.LoadNotificationAsync(
                "channel.charity_campaign.progress",
                "1",
                AddPath,
                "ChannelCharityCampaignProgress.json"
                );
            // Act
            var result = await MessageProcessing.DeserializeMessageAsync(messageString);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<WebSocketNotificationMessage>(result);
            var notificationMessage = (WebSocketNotificationMessage)result;
            var Event = notificationMessage?.Payload?.Event;
            Assert.IsType<ChannelCharityCampaignProgressEvent>(Event);
            var charityProgress = (ChannelCharityCampaignProgressEvent)Event;
            Assert.Equal(1500000, charityProgress.TargetAmount.Value);
            Assert.Equal(260000, charityProgress.CurrentAmount.Value);
            Assert.Equal("https://abc.cloudfront.net/ppgf/1000/100.png", charityProgress.CharityLogo);
            Assert.Equal("befa7b53-d79d-478f-86b9-120f112b044e", notificationMessage.Metadata.MessageId);
            Assert.Equal("webhook", notificationMessage?.Payload?.Subscription.Transport.Method);
            Assert.Equal("123456", notificationMessage?.Payload?.Subscription.Condition.BroadcasterUserId);
        }

        [Fact]
        public async Task MessageProcessing_IsChannelCampaignStart()
        {
            string messageString = await HelperFunctions.LoadNotificationAsync(
                "channel.charity_campaign.start",
                "1",
                AddPath,
                "ChannelCharityCampaignStart.json"
                );
            // Act
            var result = await MessageProcessing.DeserializeMessageAsync(messageString);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<WebSocketNotificationMessage>(result);
            var notificationMessage = (WebSocketNotificationMessage)result;
            var Event = notificationMessage?.Payload?.Event;
            Assert.IsType<ChannelCharityCampaignStartEvent>(Event);
            var charityStart = (ChannelCharityCampaignStartEvent)Event;
            Assert.Equal(1500000, charityStart.TargetAmount.Value);
            Assert.Equal(0, charityStart.CurrentAmount.Value);
            Assert.Equal("https://abc.cloudfront.net/ppgf/1000/100.png", charityStart.CharityLogo);
            Assert.Equal("befa7b53-d79d-478f-86b9-120f112b044e", notificationMessage.Metadata.MessageId);
            Assert.Equal("webhook", notificationMessage?.Payload?.Subscription.Transport.Method);
            Assert.Equal("123456", notificationMessage?.Payload?.Subscription.Condition.BroadcasterUserId);
        }

        [Fact]
        public async Task MessageProcessing_IsChannelCampaignStop()
        {
            string messageString = await HelperFunctions.LoadNotificationAsync(
                "channel.charity_campaign.stop",
                "1",
                AddPath,
                "ChannelCharityCampaignStop.json"
                );
            // Act
            var result = await MessageProcessing.DeserializeMessageAsync(messageString);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<WebSocketNotificationMessage>(result);
            var notificationMessage = (WebSocketNotificationMessage)result;
            var Event = notificationMessage?.Payload?.Event;
            Assert.IsType<ChannelCharityCampaignStopEvent>(Event);
            var charityStop = (ChannelCharityCampaignStopEvent)Event;
            Assert.Equal(1500000, charityStop.TargetAmount.Value);
            Assert.Equal(1450000, charityStop.CurrentAmount.Value);
            Assert.Equal("2022-07-26T22:00:03.17106713Z", charityStop.StoppedAt);
            Assert.Equal("befa7b53-d79d-478f-86b9-120f112b044e", notificationMessage.Metadata.MessageId);
            Assert.Equal("webhook", notificationMessage?.Payload?.Subscription.Transport.Method);
            Assert.Equal("123456", notificationMessage?.Payload?.Subscription.Condition.BroadcasterUserId);
        }

        [Fact]
        public async Task MessageProcessing_IsChatClear()
        {
            string messageString = await HelperFunctions.LoadNotificationAsync(
                "channel.chat.clear",
                "1",
                AddPath,
                "ChannelChatClear.json"
                );
            // Act
            var result = await MessageProcessing.DeserializeMessageAsync(messageString);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<WebSocketNotificationMessage>(result);
            var notificationMessage = (WebSocketNotificationMessage)result;
            var Event = notificationMessage?.Payload?.Event;
            Assert.IsType<ChannelChatClearEvent>(Event);
            var chatClear = (ChannelChatClearEvent)Event;
            Assert.Equal("1337", chatClear.BroadcasterUserId);
            Assert.Equal("befa7b53-d79d-478f-86b9-120f112b044e", notificationMessage.Metadata.MessageId);
            Assert.Equal("webhook", notificationMessage?.Payload?.Subscription.Transport.Method);
            Assert.Equal("1337", notificationMessage?.Payload?.Subscription.Condition.BroadcasterUserId);
        }

        [Fact]
        public async Task MessageProcessing_IsChatClearUser()
        {
            string messageString = await HelperFunctions.LoadNotificationAsync(
                "channel.chat.clear_user_messages",
                "1",
                AddPath,
                "ChannelChatClearUser.json"
                );
            // Act
            var result = await MessageProcessing.DeserializeMessageAsync(messageString);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<WebSocketNotificationMessage>(result);
            var notificationMessage = (WebSocketNotificationMessage)result;
            var Event = notificationMessage?.Payload?.Event;
            Assert.IsType<ChannelChatClearUserMessagesEvent>(Event);
            var chatClearUser = (ChannelChatClearUserMessagesEvent)Event;
            Assert.Equal("7734", chatClearUser.TargetUserId);
            Assert.Equal("befa7b53-d79d-478f-86b9-120f112b044e", notificationMessage.Metadata.MessageId);
            Assert.Equal("webhook", notificationMessage?.Payload?.Subscription.Transport.Method);
            Assert.Equal("1337", notificationMessage?.Payload?.Subscription.Condition.BroadcasterUserId);
        }

        [Fact]
        public async Task MessageProcessing_IsChatMessage()
        {
            string messageString = await HelperFunctions.LoadNotificationAsync(
                "channel.chat.message",
                "1",
                AddPath,
                "ChannelChatMessage.json"
                );
            // Act
            var result = await MessageProcessing.DeserializeMessageAsync(messageString);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<WebSocketNotificationMessage>(result);
            var notificationMessage = (WebSocketNotificationMessage)result;
            var Event = notificationMessage?.Payload?.Event;
            Assert.IsType<ChannelChatMessageEvent>(Event);
            var chatMessage = (ChannelChatMessageEvent)Event;
            Assert.Equal("4145994", chatMessage.ChatterUserId);
            Assert.Equal("Hi chat", chatMessage.Message.Text);
            Assert.Null(chatMessage.Message.Fragments.FirstOrDefault().Cheermote);
            Assert.Equal("1", chatMessage.Badges.FirstOrDefault().Id);
            Assert.Null(chatMessage.ChannelPointsCustomRewardId);
            Assert.Equal("befa7b53-d79d-478f-86b9-120f112b044e", notificationMessage.Metadata.MessageId);
            Assert.Equal("websocket", notificationMessage?.Payload?.Subscription.Transport.Method);
            Assert.Equal("1971641", notificationMessage?.Payload?.Subscription.Condition.BroadcasterUserId);
        }

        [Fact]
        public async Task MessageProcessing_IsChatMessageDelete()
        {
            string messageString = await HelperFunctions.LoadNotificationAsync(
                "channel.chat.message_delete",
                "1",
                AddPath,
                "ChannelChatMessageDelete.json"
                );
            // Act
            var result = await MessageProcessing.DeserializeMessageAsync(messageString);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<WebSocketNotificationMessage>(result);
            var notificationMessage = (WebSocketNotificationMessage)result;
            var Event = notificationMessage?.Payload?.Event;
            Assert.IsType<ChannelChatMessageDeleteEvent>(Event);
            var chatMessageDelete = (ChannelChatMessageDeleteEvent)Event;
            Assert.Equal("ab24e0b0-2260-4bac-94e4-05eedd4ecd0e", chatMessageDelete.MessageId);
            Assert.Equal("befa7b53-d79d-478f-86b9-120f112b044e", notificationMessage.Metadata.MessageId);
            Assert.Equal("webhook", notificationMessage?.Payload?.Subscription.Transport.Method);
            Assert.Equal("1337", notificationMessage?.Payload?.Subscription.Condition.BroadcasterUserId);
        }

        [Fact]
        public async Task MessageProcessing_IsChatNotification()
        {
            string messageString = await HelperFunctions.LoadNotificationAsync(
                "channel.chat.notification",
                "1",
                AddPath,
                "ChannelChatNotification.json"
                );
            // Act
            var result = await MessageProcessing.DeserializeMessageAsync(messageString);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<WebSocketNotificationMessage>(result);
            var notificationMessage = (WebSocketNotificationMessage)result;
            var Event = notificationMessage?.Payload?.Event;
            Assert.IsType<ChannelChatNotificationEvent>(Event);
            var chatMessageNotification = (ChannelChatNotificationEvent)Event;
            Assert.Equal("1", chatMessageNotification.Badges.FirstOrDefault().Id);
            Assert.Equal("chat message", chatMessageNotification.SystemMessage);
            Assert.Equal("static", chatMessageNotification.Message.Fragments.FirstOrDefault().Emote.Format.FirstOrDefault());
            Assert.Equal("emote", chatMessageNotification.Message.Fragments.FirstOrDefault().Type);
            Assert.Equal("ab24e0b0-2260-4bac-94e4-05eedd4ecd0e", chatMessageNotification.MessageId);
            Assert.Equal("befa7b53-d79d-478f-86b9-120f112b044e", notificationMessage.Metadata.MessageId);
            Assert.Equal("webhook", notificationMessage?.Payload?.Subscription.Transport.Method);
            Assert.Equal("1337", notificationMessage?.Payload?.Subscription.Condition.BroadcasterUserId);
        }

        [Fact]
        public async Task MessageProcessing_IsChatSettingsUpdate()
        {
            string messageString = await HelperFunctions.LoadNotificationAsync(
                "channel.chat_settings.update",
                "1",
                AddPath,
                "ChannelChatSettingsUpdate.json"
                );
            // Act
            var result = await MessageProcessing.DeserializeMessageAsync(messageString);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<WebSocketNotificationMessage>(result);
            var notificationMessage = (WebSocketNotificationMessage)result;
            var Event = notificationMessage?.Payload?.Event;
            Assert.IsType<ChannelChatSettingsUpdateEvent>(Event);
            var chatSettingsUpdate = (ChannelChatSettingsUpdateEvent)Event;
            Assert.True(chatSettingsUpdate.EmoteMode);
            Assert.Equal(10, chatSettingsUpdate.SlowModeWaitTimeSeconds);
            Assert.Equal("befa7b53-d79d-478f-86b9-120f112b044e", notificationMessage.Metadata.MessageId);
            Assert.Equal("webhook", notificationMessage?.Payload?.Subscription.Transport.Method);
            Assert.Equal("1337", notificationMessage?.Payload?.Subscription.Condition.BroadcasterUserId);
        }

        [Fact]
        public async Task MessageProcessing_IsChatUserMessageHold()
        {
            string messageString = await HelperFunctions.LoadNotificationAsync(
                "channel.chat.user_message_hold",
                "1",
                AddPath,
                "ChannelChatUserMessageHold.json"
                );
            // Act
            var result = await MessageProcessing.DeserializeMessageAsync(messageString);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<WebSocketNotificationMessage>(result);
            var notificationMessage = (WebSocketNotificationMessage)result;
            var Event = notificationMessage?.Payload?.Event;
            Assert.IsType<ChannelChatUserMessageHoldEvent>(Event);
            var chatUserMessageHold = (ChannelChatUserMessageHoldEvent)Event;
            Assert.Equal("emote", chatUserMessageHold.Message.Fragments.FirstOrDefault().Type);
            Assert.Equal("foo", chatUserMessageHold.Message.Fragments.FirstOrDefault().Emote.Id);
            Assert.Equal("befa7b53-d79d-478f-86b9-120f112b044e", notificationMessage.Metadata.MessageId);
            Assert.Equal("webhook", notificationMessage?.Payload?.Subscription.Transport.Method);
            Assert.Equal("1337", notificationMessage?.Payload?.Subscription.Condition.BroadcasterUserId);
        }

        [Fact]
        public async Task MessageProcessing_IsChatUserMessageUpdate()
        {
            string messageString = await HelperFunctions.LoadNotificationAsync(
                "channel.chat.user_message_update",
                "1",
                AddPath,
                "ChannelChatUserMessageUpdate.json"
                );
            // Act
            var result = await MessageProcessing.DeserializeMessageAsync(messageString);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<WebSocketNotificationMessage>(result);
            var notificationMessage = (WebSocketNotificationMessage)result;
            var Event = notificationMessage?.Payload?.Event;
            Assert.IsType<ChannelChatUserMessageUpdateEvent>(Event);
            var chatUserMessageUpdate = (ChannelChatUserMessageUpdateEvent)Event;
            Assert.Equal("emote", chatUserMessageUpdate.Message.Fragments.FirstOrDefault().Type);
            Assert.Equal("foo", chatUserMessageUpdate.Message.Fragments.FirstOrDefault().Emote.Id);
            Assert.Equal("befa7b53-d79d-478f-86b9-120f112b044e", notificationMessage.Metadata.MessageId);
            Assert.Equal("webhook", notificationMessage?.Payload?.Subscription.Transport.Method);
            Assert.Equal("1337", notificationMessage?.Payload?.Subscription.Condition.BroadcasterUserId);
        }

        [Fact]
        public async Task MessageProcessing_IsChannelCheer()
        {
            string messageString = await HelperFunctions.LoadNotificationAsync(
                "channel.cheer",
                "1",
                AddPath,
                "ChannelCheer.json"
                );
            // Act
            var result = await MessageProcessing.DeserializeMessageAsync(messageString);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<WebSocketNotificationMessage>(result);
            var notificationMessage = (WebSocketNotificationMessage)result;
            var Event = notificationMessage?.Payload?.Event;
            Assert.IsType<ChannelCheerEvent>(Event);
            var Cheer = (ChannelCheerEvent)Event;
            Assert.Equal("pogchamp", Cheer.Message);
            Assert.Equal(1000, Cheer.Bits);
            Assert.Equal("befa7b53-d79d-478f-86b9-120f112b044e", notificationMessage.Metadata.MessageId);
            Assert.Equal("webhook", notificationMessage?.Payload?.Subscription.Transport.Method);
            Assert.Equal("1337", notificationMessage?.Payload?.Subscription.Condition.BroadcasterUserId);
        }

        [Fact]
        public async Task MessageProcessing_IsChannelModeratorAdd()
        {
            string messageString = await HelperFunctions.LoadNotificationAsync(
                "channel.moderate.add",
                "1",
                AddPath,
                "ChannelModeratorAdd.json"
                );
            // Act
            var result = await MessageProcessing.DeserializeMessageAsync(messageString);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<WebSocketNotificationMessage>(result);
            var notificationMessage = (WebSocketNotificationMessage)result;
            var Event = notificationMessage?.Payload?.Event;
            Assert.IsType<ChannelModeratorAddEvent>(Event);
            var moderatorAdd = (ChannelModeratorAddEvent)Event;
            Assert.Equal("1234", moderatorAdd.UserId);
            Assert.Equal("Mod_User", moderatorAdd.UserName);
            Assert.Equal("befa7b53-d79d-478f-86b9-120f112b044e", notificationMessage.Metadata.MessageId);
            Assert.Equal("webhook", notificationMessage?.Payload?.Subscription.Transport.Method);
            Assert.Equal("1337", notificationMessage?.Payload?.Subscription.Condition.BroadcasterUserId);
        }

        [Fact]
        public async Task MessageProcessing_IsChannelModeratorRemoved()
        {
            string messageString = await HelperFunctions.LoadNotificationAsync(
                "channel.moderate.remove",
                "1",
                AddPath,
                "ChannelModeratorRemove.json"
                );
            // Act
            var result = await MessageProcessing.DeserializeMessageAsync(messageString);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<WebSocketNotificationMessage>(result);
            var notificationMessage = (WebSocketNotificationMessage)result;
            var Event = notificationMessage?.Payload?.Event;
            Assert.IsType<ChannelModeratorRemoveEvent>(Event);
            var moderatorRemove = (ChannelModeratorRemoveEvent)Event;
            Assert.Equal("1234", moderatorRemove.UserId);
            Assert.Equal("Not_Mod_User", moderatorRemove.UserName);
            Assert.Equal("befa7b53-d79d-478f-86b9-120f112b044e", notificationMessage.Metadata.MessageId);
            Assert.Equal("webhook", notificationMessage?.Payload?.Subscription.Transport.Method);
            Assert.Equal("1337", notificationMessage?.Payload?.Subscription.Condition.BroadcasterUserId);
        }

        [Fact]
        public async Task MessageProcessing_IsChannelPointsAutomaticRewardRedemptionAdd()
        {
            string messageString = await HelperFunctions.LoadNotificationAsync(
                "channel.channel_points_automatic_reward_redemption.add",
                "1",
                AddPath,
                "ChannelPointsAutomaticRewardAdd.json"
                );
            // Act
            var result = await MessageProcessing.DeserializeMessageAsync(messageString);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<WebSocketNotificationMessage>(result);
            var notificationMessage = (WebSocketNotificationMessage)result;
            var Event = notificationMessage?.Payload?.Event;
            Assert.IsType<ChannelPointsAutomaticRewardRedemptionAddEvent>(Event);
            var pointsAutomaticRewardRedemptionAdd = (ChannelPointsAutomaticRewardRedemptionAddEvent)Event;
            Assert.Equal("81274", pointsAutomaticRewardRedemptionAdd.Message.Emotes.FirstOrDefault().Id);
            Assert.Equal("Hello world! VoHiYo ", pointsAutomaticRewardRedemptionAdd.UserInput);
            Assert.Equal("befa7b53-d79d-478f-86b9-120f112b044e", notificationMessage.Metadata.MessageId);
            Assert.Equal("webhook", notificationMessage?.Payload?.Subscription.Transport.Method);
            Assert.Equal("12826", notificationMessage?.Payload?.Subscription.Condition.BroadcasterUserId);
        }

        [Fact]
        public async Task MessageProcessing_IsChannelPointsCustomRewardAdd()
        {
            string messageString = await HelperFunctions.LoadNotificationAsync(
                "channel.channel_points_custom_reward.add",
                "1",
                AddPath,
                "ChannelPointsCustomRewardAdd.json"
                );
            // Act
            var result = await MessageProcessing.DeserializeMessageAsync(messageString);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<WebSocketNotificationMessage>(result);
            var notificationMessage = (WebSocketNotificationMessage)result;
            var Event = notificationMessage?.Payload?.Event;
            Assert.IsType<ChannelPointsCustomRewardAddEvent>(Event);
            var pointsCustomRewardAdd = (ChannelPointsCustomRewardAddEvent)Event;
            Assert.Equal(1000, pointsCustomRewardAdd.MaxPerStream.Value);
            Assert.Equal(1000, pointsCustomRewardAdd.MaxPerUserPerStream.Value);
            Assert.Equal(1000, pointsCustomRewardAdd.GlobalCooldown.Seconds);
            Assert.Equal("https://static-cdn.jtvnw.net/image-1.png", pointsCustomRewardAdd.Image.Url1x);
            Assert.Equal("https://static-cdn.jtvnw.net/default-1.png", pointsCustomRewardAdd.DefaultImage.Url1x);
            Assert.Equal("befa7b53-d79d-478f-86b9-120f112b044e", notificationMessage.Metadata.MessageId);
            Assert.Equal("webhook", notificationMessage?.Payload?.Subscription.Transport.Method);
            Assert.Equal("1337", notificationMessage?.Payload?.Subscription.Condition.BroadcasterUserId);
        }

        [Fact]
        public async Task MessageProcessing_IsChannelPointsCustomRewardRedemptionAdd()
        {
            string messageString = await HelperFunctions.LoadNotificationAsync(
                "channel.channel_points_automatic_reward_redemption.add",
                "1",
                AddPath,
                "ChannelPointsCustomRewardRedemptionAdd.json"
                );
            // Act
            var result = await MessageProcessing.DeserializeMessageAsync(messageString);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<WebSocketNotificationMessage>(result);
            var notificationMessage = (WebSocketNotificationMessage)result;
            var Event = notificationMessage?.Payload?.Event;
            Assert.IsType<ChannelPointsCustomRewardRedemptionAddEvent>(Event);
            var pointsCustomRewardRedemptionAdd = (ChannelPointsCustomRewardRedemptionAddEvent)Event;
            Assert.Equal("92af127c-7326-4483-a52b-b0da0be61c01", pointsCustomRewardRedemptionAdd.Reward.Id);
            Assert.Equal("2020-07-15T17:16:03.17106713Z", pointsCustomRewardRedemptionAdd.RedeemedAt);
            Assert.Equal("befa7b53-d79d-478f-86b9-120f112b044e", notificationMessage.Metadata.MessageId);
            Assert.Equal("webhook", notificationMessage?.Payload?.Subscription.Transport.Method);
            Assert.Equal("1337", notificationMessage?.Payload?.Subscription.Condition.BroadcasterUserId);
        }

        [Fact]
        public async Task MessageProcessing_IsChannelPointsCustomRewardRedemptionUpdate()
        {
            string messageString = await HelperFunctions.LoadNotificationAsync(
                "channel.channel_points_custom_reward_redemption.update",
                "1",
                AddPath,
                "ChannelPointsCustomRewardRedemptionUpdate.json"
                );
            // Act
            var result = await MessageProcessing.DeserializeMessageAsync(messageString);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<WebSocketNotificationMessage>(result);
            var notificationMessage = (WebSocketNotificationMessage)result;
            var Event = notificationMessage?.Payload?.Event;
            Assert.IsType<ChannelPointsCustomRewardRedemptionUpdateEvent>(Event);
            var pointsCustomRewardRedemptionUpdate = (ChannelPointsCustomRewardRedemptionUpdateEvent)Event;
            Assert.Equal("92af127c-7326-4483-a52b-b0da0be61c01", pointsCustomRewardRedemptionUpdate.Reward.Id);
            Assert.Equal("2020-07-15T17:16:03.17106713Z", pointsCustomRewardRedemptionUpdate.RedeemedAt);
            Assert.Equal("befa7b53-d79d-478f-86b9-120f112b044e", notificationMessage.Metadata.MessageId);
            Assert.Equal("webhook", notificationMessage?.Payload?.Subscription.Transport.Method);
            Assert.Equal("1337", notificationMessage?.Payload?.Subscription.Condition.BroadcasterUserId);
        }

        [Fact]
        public async Task MessageProcessing_IsChannelPointsCustomRewardRemove()
        {
            string messageString = await HelperFunctions.LoadNotificationAsync(
                "channel.channel_points_custom_reward.remove",
                "1",
                AddPath,
                "ChannelPointsCustomRewardRemove.json"
                );
            // Act
            var result = await MessageProcessing.DeserializeMessageAsync(messageString);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<WebSocketNotificationMessage>(result);
            var notificationMessage = (WebSocketNotificationMessage)result;
            var Event = notificationMessage?.Payload?.Event;
            Assert.IsType<ChannelPointsCustomRewardRemoveEvent>(Event);
            var pointsCustomRewardRemove = (ChannelPointsCustomRewardRemoveEvent)Event;
            Assert.Equal(1000, pointsCustomRewardRemove.MaxPerStream.Value);
            Assert.Equal(1000, pointsCustomRewardRemove.MaxPerUserPerStream.Value);
            Assert.Equal(1000, pointsCustomRewardRemove.GlobalCooldown.Seconds);
            Assert.Equal("https://static-cdn.jtvnw.net/image-1.png", pointsCustomRewardRemove.Image.Url1x);
            Assert.Equal("https://static-cdn.jtvnw.net/default-1.png", pointsCustomRewardRemove.DefaultImage.Url1x);
            Assert.Equal("befa7b53-d79d-478f-86b9-120f112b044e", notificationMessage.Metadata.MessageId);
            Assert.Equal("webhook", notificationMessage?.Payload?.Subscription.Transport.Method);
            Assert.Equal("1337", notificationMessage?.Payload?.Subscription.Condition.BroadcasterUserId);
        }

        [Fact]
        public async Task MessageProcessing_IsChannelPointsCustomRewardUpdate()
        {
            string messageString = await HelperFunctions.LoadNotificationAsync(
                "channel.channel_points_custom_reward.update",
                "1",
                AddPath,
                "ChannelPointsCustomRewardUpdate.json"
                );
            // Act
            var result = await MessageProcessing.DeserializeMessageAsync(messageString);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<WebSocketNotificationMessage>(result);
            var notificationMessage = (WebSocketNotificationMessage)result;
            var Event = notificationMessage?.Payload?.Event;
            Assert.IsType<ChannelPointsCustomRewardUpdateEvent>(Event);
            var pointsCustomRewardUpdate = (ChannelPointsCustomRewardUpdateEvent)Event;
            Assert.Equal(1000, pointsCustomRewardUpdate.MaxPerStream.Value);
            Assert.Equal(1000, pointsCustomRewardUpdate.MaxPerUserPerStream.Value);
            Assert.Equal(1000, pointsCustomRewardUpdate.GlobalCooldown.Seconds);
            Assert.Equal("https://static-cdn.jtvnw.net/image-1.png", pointsCustomRewardUpdate.Image.Url1x);
            Assert.Equal("https://static-cdn.jtvnw.net/default-1.png", pointsCustomRewardUpdate.DefaultImage.Url1x);
            Assert.Equal("befa7b53-d79d-478f-86b9-120f112b044e", notificationMessage.Metadata.MessageId);
            Assert.Equal("webhook", notificationMessage?.Payload?.Subscription.Transport.Method);
            Assert.Equal("1337", notificationMessage?.Payload?.Subscription.Condition.BroadcasterUserId);
        }

        [Fact]
        public async Task MessageProcessing_IsChannelPollBegin()
        {
            string messageString = await HelperFunctions.LoadNotificationAsync(
                "channel.poll.begin",
                "1",
                AddPath,
                "ChannelPollBegin.json"
                );
            // Act
            var result = await MessageProcessing.DeserializeMessageAsync(messageString);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<WebSocketNotificationMessage>(result);
            var notificationMessage = (WebSocketNotificationMessage)result;
            var Event = notificationMessage?.Payload?.Event;
            Assert.IsType<ChannelPollBeginEvent>(Event);
            var pollBegin = (ChannelPollBeginEvent)Event;
            Assert.Equal("Aren�t shoes just really hard socks?", pollBegin.Title);
            Assert.Equal("123", pollBegin.Choices.FirstOrDefault().Id);
            Assert.Equal(10, pollBegin.BitsVoting.AmountPerVote);
            Assert.Equal(10, pollBegin.ChannelPointsVoting.AmountPerVote);
            Assert.Equal("befa7b53-d79d-478f-86b9-120f112b044e", notificationMessage.Metadata.MessageId);
            Assert.Equal("webhook", notificationMessage?.Payload?.Subscription.Transport.Method);
            Assert.Equal("1337", notificationMessage?.Payload?.Subscription.Condition.BroadcasterUserId);
        }

        [Fact]
        public async Task MessageProcessing_IsChannelPollEnd()
        {
            string messageString = await HelperFunctions.LoadNotificationAsync(
                "channel.poll.end",
                "1",
                AddPath,
                "ChannelPollEnd.json"
                );
            // Act
            var result = await MessageProcessing.DeserializeMessageAsync(messageString);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<WebSocketNotificationMessage>(result);
            var notificationMessage = (WebSocketNotificationMessage)result;
            var Event = notificationMessage?.Payload?.Event;
            Assert.IsType<ChannelPollEndEvent>(Event);
            var pollEnd = (ChannelPollEndEvent)Event;
            Assert.Equal("Aren�t shoes just really hard socks?", pollEnd.Title);
            Assert.Equal("123", pollEnd.Choices.FirstOrDefault().Id);
            Assert.Equal(10, pollEnd.BitsVoting.AmountPerVote);
            Assert.Equal(10, pollEnd.ChannelPointsVoting.AmountPerVote);
            Assert.Equal("2020-07-15T17:16:11.17106713Z", pollEnd.EndedAt);
            Assert.Equal("befa7b53-d79d-478f-86b9-120f112b044e", notificationMessage.Metadata.MessageId);
            Assert.Equal("webhook", notificationMessage?.Payload?.Subscription.Transport.Method);
            Assert.Equal("1337", notificationMessage?.Payload?.Subscription.Condition.BroadcasterUserId);
        }

        [Fact]
        public async Task MessageProcessing_IsChannelPollProgress()
        {
            string messageString = await HelperFunctions.LoadNotificationAsync(
                "channel.poll.progress",
                "1",
                AddPath,
                "ChannelPollProgress.json"
                );
            // Act
            var result = await MessageProcessing.DeserializeMessageAsync(messageString);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<WebSocketNotificationMessage>(result);
            var notificationMessage = (WebSocketNotificationMessage)result;
            var Event = notificationMessage?.Payload?.Event;
            Assert.IsType<ChannelPollProgressEvent>(Event);
            var pollProgress = (ChannelPollProgressEvent)Event;
            Assert.Equal("Aren�t shoes just really hard socks?", pollProgress.Title);
            Assert.Equal("123", pollProgress.Choices.FirstOrDefault().Id);
            Assert.Equal(10, pollProgress.BitsVoting.AmountPerVote);
            Assert.Equal(10, pollProgress.ChannelPointsVoting.AmountPerVote);
            Assert.Equal("2020-07-15T17:16:08.17106713Z", pollProgress.EndsAt);
            Assert.Equal("befa7b53-d79d-478f-86b9-120f112b044e", notificationMessage.Metadata.MessageId);
            Assert.Equal("webhook", notificationMessage?.Payload?.Subscription.Transport.Method);
            Assert.Equal("1337", notificationMessage?.Payload?.Subscription.Condition.BroadcasterUserId);
        }

        [Fact]
        public async Task MessageProcessing_IsChannelPredictionBegin()
        {
            string messageString = await HelperFunctions.LoadNotificationAsync(
                "channel.prediction.begin",
                "1",
                AddPath,
                "ChannelPredictionBegin.json"
                );
            // Act
            var result = await MessageProcessing.DeserializeMessageAsync(messageString);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<WebSocketNotificationMessage>(result);
            var notificationMessage = (WebSocketNotificationMessage)result;
            var Event = notificationMessage?.Payload?.Event;
            Assert.IsType<ChannelPredictionBeginEvent>(Event);
            var pointsCustomRewardUpdate = (ChannelPredictionBeginEvent)Event;
            Assert.Equal("Aren�t shoes just really hard socks?", pointsCustomRewardUpdate.Title);
            Assert.Equal("1243456", pointsCustomRewardUpdate.Outcomes.FirstOrDefault().Id);
            Assert.Equal("2020-07-15T17:21:03.17106713Z", pointsCustomRewardUpdate.LocksAt);
            Assert.Equal("befa7b53-d79d-478f-86b9-120f112b044e", notificationMessage.Metadata.MessageId);
            Assert.Equal("webhook", notificationMessage?.Payload?.Subscription.Transport.Method);
            Assert.Equal("1337", notificationMessage?.Payload?.Subscription.Condition.BroadcasterUserId);
        }

        [Fact]
        public async Task MessageProcessing_IsChannelPredictionEnd()
        {
            string messageString = await HelperFunctions.LoadNotificationAsync(
                "channel.prediction.end",
                "1",
                AddPath,
                "ChannelPredictionEnd.json"
                );
            // Act
            var result = await MessageProcessing.DeserializeMessageAsync(messageString);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<WebSocketNotificationMessage>(result);
            var notificationMessage = (WebSocketNotificationMessage)result;
            var Event = notificationMessage?.Payload?.Event;
            Assert.IsType<ChannelPredictionEndEvent>(Event);
            var predictionEnd = (ChannelPredictionEndEvent)Event;
            Assert.Equal("Aren�t shoes just really hard socks?", predictionEnd.Title);
            Assert.Equal("12345", predictionEnd.Outcomes.FirstOrDefault().Id);
            Assert.Equal("Cool_User", predictionEnd.Outcomes.FirstOrDefault().TopPredictors.FirstOrDefault().UserName);
            Assert.Equal("2020-07-15T17:16:11.17106713Z", predictionEnd.EndedAt);
            Assert.Equal("befa7b53-d79d-478f-86b9-120f112b044e", notificationMessage.Metadata.MessageId);
            Assert.Equal("webhook", notificationMessage?.Payload?.Subscription.Transport.Method);
            Assert.Equal("1337", notificationMessage?.Payload?.Subscription.Condition.BroadcasterUserId);
        }

        [Fact]
        public async Task MessageProcessing_IsChannelPredictionLock()
        {
            string messageString = await HelperFunctions.LoadNotificationAsync(
                "channel.prediction.lock",
                "1",
                AddPath,
                "ChannelPredictionLock.json"
                );
            // Act
            var result = await MessageProcessing.DeserializeMessageAsync(messageString);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<WebSocketNotificationMessage>(result);
            var notificationMessage = (WebSocketNotificationMessage)result;
            var Event = notificationMessage?.Payload?.Event;
            Assert.IsType<ChannelPredictionLockEvent>(Event);
            var predictionLock = (ChannelPredictionLockEvent)Event;
            Assert.Equal("Aren’t shoes just really hard socks?", predictionLock.Title);
            Assert.Equal("1243456", predictionLock.Outcomes.FirstOrDefault().Id);
            Assert.Equal("2020-07-15T17:21:03.17106713Z", predictionLock.LockedAt);
            Assert.Equal("befa7b53-d79d-478f-86b9-120f112b044e", notificationMessage.Metadata.MessageId);
            Assert.Equal("webhook", notificationMessage?.Payload?.Subscription.Transport.Method);
            Assert.Equal("1337", notificationMessage?.Payload?.Subscription.Condition.BroadcasterUserId);
        }

        [Fact]
        public async Task MessageProcessing_IsChannelPredictionProgress()
        {
            string messageString = await HelperFunctions.LoadNotificationAsync(
                "channel.prediction.progress",
                "1",
                AddPath,
                "ChannelPredictionProgress.json"
                );
            // Act
            var result = await MessageProcessing.DeserializeMessageAsync(messageString);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<WebSocketNotificationMessage>(result);
            var notificationMessage = (WebSocketNotificationMessage)result;
            var Event = notificationMessage?.Payload?.Event;
            Assert.IsType<ChannelPredictionProgressEvent>(Event);
            var predictionProgress = (ChannelPredictionProgressEvent)Event;
            Assert.Equal("Aren�t shoes just really hard socks?", predictionProgress.Title);
            Assert.Equal("1243456", predictionProgress.Outcomes.FirstOrDefault().Id);
            Assert.Equal("2020-07-15T17:21:03.17106713Z", predictionProgress.LocksAt);
            Assert.Equal("befa7b53-d79d-478f-86b9-120f112b044e", notificationMessage.Metadata.MessageId);
            Assert.Equal("webhook", notificationMessage?.Payload?.Subscription.Transport.Method);
            Assert.Equal("1337", notificationMessage?.Payload?.Subscription.Condition.BroadcasterUserId);
        }

        [Fact]
        public async Task MessageProcessing_IsChannelRaid()
        {
            string messageString = await HelperFunctions.LoadNotificationAsync(
                "channel.raid",
                "1",
                AddPath,
                "ChannelRaid.json"
                );
            // Act
            var result = await MessageProcessing.DeserializeMessageAsync(messageString);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<WebSocketNotificationMessage>(result);
            var notificationMessage = (WebSocketNotificationMessage)result;
            var Event = notificationMessage?.Payload?.Event;
            Assert.IsType<ChannelRaidEvent>(Event);
            var raid = (ChannelRaidEvent)Event; ;
            Assert.Equal(9001, raid.Viewers);
            Assert.Equal("befa7b53-d79d-478f-86b9-120f112b044e", notificationMessage.Metadata.MessageId);
            Assert.Equal("webhook", notificationMessage?.Payload?.Subscription.Transport.Method);
            Assert.Equal("1337", notificationMessage?.Payload?.Subscription.Condition.ToBroadcasterUserId);
        }

        [Fact]
        public async Task MessageProcessing_IsChannelShieldModeBegin()
        {
            string messageString = await HelperFunctions.LoadNotificationAsync(
                "channel.shield_mode.begin",
                "1",
                AddPath,
                "ChannelShieldModeBegin.json"
                );
            // Act
            var result = await MessageProcessing.DeserializeMessageAsync(messageString);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<WebSocketNotificationMessage>(result);
            var notificationMessage = (WebSocketNotificationMessage)result;
            var Event = notificationMessage?.Payload?.Event;
            Assert.IsType<ChannelShieldModeBeginEvent>(Event);
            var shieldModeBegin = (ChannelShieldModeBeginEvent)Event; ;
            Assert.Equal("2022-07-26T17:00:03.17106713Z", shieldModeBegin.StartedAt);
            Assert.Equal("befa7b53-d79d-478f-86b9-120f112b044e", notificationMessage.Metadata.MessageId);
            Assert.Equal("webhook", notificationMessage?.Payload?.Subscription.Transport.Method);
            Assert.Equal("12345", notificationMessage?.Payload?.Subscription.Condition.BroadcasterUserId);
        }

        [Fact]
        public async Task MessageProcessing_IsChannelShieldModeEnd()
        {
            string messageString = await HelperFunctions.LoadNotificationAsync(
                "channel.shield_mode.end",
                "1",
                AddPath,
                "ChannelShieldModeEnd.json"
                );
            // Act
            var result = await MessageProcessing.DeserializeMessageAsync(messageString);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<WebSocketNotificationMessage>(result);
            var notificationMessage = (WebSocketNotificationMessage)result;
            var Event = notificationMessage?.Payload?.Event;
            Assert.IsType<ChannelShieldModeEndEvent>(Event);
            var shieldModeEnd = (ChannelShieldModeEndEvent)Event; ;
            Assert.Equal("2022-07-27T01:30:23.17106713Z", shieldModeEnd.EndedAt);
            Assert.Equal("befa7b53-d79d-478f-86b9-120f112b044e", notificationMessage.Metadata.MessageId);
            Assert.Equal("webhook", notificationMessage?.Payload?.Subscription.Transport.Method);
            Assert.Equal("12345", notificationMessage?.Payload?.Subscription.Condition.BroadcasterUserId);
        }

        [Fact]
        public async Task MessageProcessing_IsChannelShoutoutCreate()
        {
            string messageString = await HelperFunctions.LoadNotificationAsync(
                "channel.shoutout.create",
                "1",
                AddPath,
                "ChannelShoutoutCreate.json"
                );
            // Act
            var result = await MessageProcessing.DeserializeMessageAsync(messageString);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<WebSocketNotificationMessage>(result);
            var notificationMessage = (WebSocketNotificationMessage)result;
            var Event = notificationMessage?.Payload?.Event;
            Assert.IsType<ChannelShoutoutCreateEvent>(Event);
            var shoutoutCreate = (ChannelShoutoutCreateEvent)Event; ;
            Assert.Equal(860, shoutoutCreate.ViewerCount);
            Assert.Equal("SandySanderman", shoutoutCreate.ToBroadcasterUserName);
            Assert.Equal("befa7b53-d79d-478f-86b9-120f112b044e", notificationMessage.Metadata.MessageId);
            Assert.Equal("webhook", notificationMessage?.Payload?.Subscription.Transport.Method);
            Assert.Equal("12345", notificationMessage?.Payload?.Subscription.Condition.BroadcasterUserId);
        }

        [Fact]
        public async Task MessageProcessing_IsChannelShoutoutReceive()
        {
            string messageString = await HelperFunctions.LoadNotificationAsync(
                "channel.shoutout.receive",
                "1",
                AddPath,
                "ChannelShoutoutReceive.json"
                );
            // Act
            var result = await MessageProcessing.DeserializeMessageAsync(messageString);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<WebSocketNotificationMessage>(result);
            var notificationMessage = (WebSocketNotificationMessage)result;
            var Event = notificationMessage?.Payload?.Event;
            Assert.IsType<ChannelShoutoutReceivedEvent>(Event);
            var shoutoutReceive = (ChannelShoutoutReceivedEvent)Event; ;
            Assert.Equal(860, shoutoutReceive.ViewerCount);
            Assert.Equal("SimplySimple", shoutoutReceive.FromBroadcasterUserName);
            Assert.Equal("befa7b53-d79d-478f-86b9-120f112b044e", notificationMessage.Metadata.MessageId);
            Assert.Equal("webhook", notificationMessage?.Payload?.Subscription.Transport.Method);
            Assert.Equal("626262", notificationMessage?.Payload?.Subscription.Condition.BroadcasterUserId);
        }

        [Fact]
        public async Task MessageProcessing_IsChannelSubscribe()
        {
            string messageString = await HelperFunctions.LoadNotificationAsync(
                "channel.subscribe",
                "1",
                AddPath,
                "ChannelSubscribe.json"
                );
            // Act
            var result = await MessageProcessing.DeserializeMessageAsync(messageString);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<WebSocketNotificationMessage>(result);
            var notificationMessage = (WebSocketNotificationMessage)result;
            var Event = notificationMessage?.Payload?.Event;
            Assert.IsType<ChannelSubscribeEvent>(Event);
            var subscribe = (ChannelSubscribeEvent)Event; ;
            Assert.Equal("1000", subscribe.Tier);
            Assert.False(subscribe.IsGift);
            Assert.Equal("befa7b53-d79d-478f-86b9-120f112b044e", notificationMessage.Metadata.MessageId);
            Assert.Equal("webhook", notificationMessage?.Payload?.Subscription.Transport.Method);
            Assert.Equal("1337", notificationMessage?.Payload?.Subscription.Condition.BroadcasterUserId);
        }

        [Fact]
        public async Task MessageProcessing_IsChannelSubscribtionEnd()
        {
            string messageString = await HelperFunctions.LoadNotificationAsync(
                "channel.subscription.end",
                "1",
                AddPath,
                "ChannelSubscriptionEnd.json"
                );
            // Act
            var result = await MessageProcessing.DeserializeMessageAsync(messageString);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<WebSocketNotificationMessage>(result);
            var notificationMessage = (WebSocketNotificationMessage)result;
            var Event = notificationMessage?.Payload?.Event;
            Assert.IsType<ChannelSubscriptionEndEvent>(Event);
            var subscribeEnd = (ChannelSubscriptionEndEvent)Event; ;
            Assert.Equal("1000", subscribeEnd.Tier);
            Assert.False(subscribeEnd.IsGift);
            Assert.Equal("befa7b53-d79d-478f-86b9-120f112b044e", notificationMessage.Metadata.MessageId);
            Assert.Equal("webhook", notificationMessage?.Payload?.Subscription.Transport.Method);
            Assert.Equal("1337", notificationMessage?.Payload?.Subscription.Condition.BroadcasterUserId);
        }

        [Fact]
        public async Task MessageProcessing_IsChannelSubscribtionGift()
        {
            string messageString = await HelperFunctions.LoadNotificationAsync(
                "channel.subscription.gift",
                "1",
                AddPath,
                "ChannelSubscriptionGift.json"
                );
            // Act
            var result = await MessageProcessing.DeserializeMessageAsync(messageString);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<WebSocketNotificationMessage>(result);
            var notificationMessage = (WebSocketNotificationMessage)result;
            var Event = notificationMessage?.Payload?.Event;
            Assert.IsType<ChannelSubscriptionGiftEvent>(Event);
            var subscribeGift = (ChannelSubscriptionGiftEvent)Event; ;
            Assert.Equal("1000", subscribeGift.Tier);
            Assert.Equal(284, subscribeGift.CumulativeTotal);
            Assert.Equal("befa7b53-d79d-478f-86b9-120f112b044e", notificationMessage.Metadata.MessageId);
            Assert.Equal("webhook", notificationMessage?.Payload?.Subscription.Transport.Method);
            Assert.Equal("1337", notificationMessage?.Payload?.Subscription.Condition.BroadcasterUserId);
        }

        [Fact]
        public async Task MessageProcessing_IsChannelSubscribtionMessage()
        {
            string messageString = await HelperFunctions.LoadNotificationAsync(
                "channel.subscription.message",
                "1",
                AddPath,
                "ChannelSubscriptionMessage.json"
                );
            // Act
            var result = await MessageProcessing.DeserializeMessageAsync(messageString);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<WebSocketNotificationMessage>(result);
            var notificationMessage = (WebSocketNotificationMessage)result;
            var Event = notificationMessage?.Payload?.Event;
            Assert.IsType<ChannelSubscriptionMessageEvent>(Event);
            var subscribeGift = (ChannelSubscriptionMessageEvent)Event; ;
            Assert.Equal("1000", subscribeGift.Tier);
            Assert.Equal(23, subscribeGift.Message.Emotes.FirstOrDefault().Begin);
            Assert.Equal("befa7b53-d79d-478f-86b9-120f112b044e", notificationMessage.Metadata.MessageId);
            Assert.Equal("webhook", notificationMessage?.Payload?.Subscription.Transport.Method);
            Assert.Equal("1337", notificationMessage?.Payload?.Subscription.Condition.BroadcasterUserId);
        }

        [Fact]
        public async Task MessageProcessing_IsChannelUnban()
        {
            string messageString = await HelperFunctions.LoadNotificationAsync(
                "channel.unban",
                "1",
                AddPath,
                "ChannelUnban.json"
                );
            // Act
            var result = await MessageProcessing.DeserializeMessageAsync(messageString);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<WebSocketNotificationMessage>(result);
            var notificationMessage = (WebSocketNotificationMessage)result;
            var Event = notificationMessage?.Payload?.Event;
            Assert.IsType<ChannelUnbanEvent>(Event);
            var unban = (ChannelUnbanEvent)Event; ;
            Assert.Equal("1234", unban.UserId);
            Assert.Equal("befa7b53-d79d-478f-86b9-120f112b044e", notificationMessage.Metadata.MessageId);
            Assert.Equal("webhook", notificationMessage?.Payload?.Subscription.Transport.Method);
            Assert.Equal("1337", notificationMessage?.Payload?.Subscription.Condition.BroadcasterUserId);
        }

        [Fact]
        public async Task MessageProcessing_IsChannelUnbanRequestCreate()
        {
            string messageString = await HelperFunctions.LoadNotificationAsync(
                "channel.unban_request.create",
                "1",
                AddPath,
                "ChannelUnbanRequestCreate.json"
                );
            // Act
            var result = await MessageProcessing.DeserializeMessageAsync(messageString);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<WebSocketNotificationMessage>(result);
            var notificationMessage = (WebSocketNotificationMessage)result;
            var Event = notificationMessage?.Payload?.Event;
            Assert.IsType<ChannelUnbanRequestCreateEvent>(Event);
            var unbanCreate = (ChannelUnbanRequestCreateEvent)Event; ;
            Assert.Equal("unban me", unbanCreate.Text);
            Assert.Equal("befa7b53-d79d-478f-86b9-120f112b044e", notificationMessage.Metadata.MessageId);
            Assert.Equal("webhook", notificationMessage?.Payload?.Subscription.Transport.Method);
            Assert.Equal("1337", notificationMessage?.Payload?.Subscription.Condition.BroadcasterUserId);
        }

        [Fact]
        public async Task MessageProcessing_IsChannelUnbanRequestResolve()
        {
            string messageString = await HelperFunctions.LoadNotificationAsync(
                "channel.unban_request.resolve",
                "1",
                AddPath,
                "ChannelUnbanRequestResolve.json"
                );
            // Act
            var result = await MessageProcessing.DeserializeMessageAsync(messageString);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<WebSocketNotificationMessage>(result);
            var notificationMessage = (WebSocketNotificationMessage)result;
            var Event = notificationMessage?.Payload?.Event;
            Assert.IsType<ChannelUnbanRequestResolveEvent>(Event);
            var unbanResolve = (ChannelUnbanRequestResolveEvent)Event; ;
            Assert.Equal("no", unbanResolve.ResolutionText);
            Assert.Equal("befa7b53-d79d-478f-86b9-120f112b044e", notificationMessage.Metadata.MessageId);
            Assert.Equal("webhook", notificationMessage?.Payload?.Subscription.Transport.Method);
            Assert.Equal("1337", notificationMessage?.Payload?.Subscription.Condition.BroadcasterUserId);
        }

        [Fact]
        public async Task MessageProcessing_IsChannelUpdate()
        {
            string messageString = await HelperFunctions.LoadNotificationAsync(
                "channel.update",
                "2",
                AddPath,
                "ChannelUpdate.json"
                );
            // Act
            var result = await MessageProcessing.DeserializeMessageAsync(messageString);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<WebSocketNotificationMessage>(result);
            var notificationMessage = (WebSocketNotificationMessage)result;
            var Event = notificationMessage?.Payload?.Event;
            Assert.IsType<ChannelUpdateEvent>(Event);
            var update = (ChannelUpdateEvent)Event;
            Assert.Equal("Grand Theft Auto", update.CategoryName);
            Assert.Equal("befa7b53-d79d-478f-86b9-120f112b044e", notificationMessage.Metadata.MessageId);
            Assert.Equal("webhook", notificationMessage?.Payload?.Subscription.Transport.Method);
            Assert.Equal("1337", notificationMessage?.Payload?.Subscription.Condition.BroadcasterUserId);
        }

        [Fact]
        public async Task MessageProcessing_IsChannelVIPAdd()
        {
            string messageString = await HelperFunctions.LoadNotificationAsync(
                "channel.vip.add",
                "1",
                AddPath,
                "ChannelVIPAdd.json"
                );
            // Act
            var result = await MessageProcessing.DeserializeMessageAsync(messageString);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<WebSocketNotificationMessage>(result);
            var notificationMessage = (WebSocketNotificationMessage)result;
            var Event = notificationMessage?.Payload?.Event;
            Assert.IsType<ChannelVIPAddEvent>(Event);
            var VIPAdd = (ChannelVIPAddEvent)Event;
            Assert.Equal("1234", VIPAdd.UserId);
            Assert.Equal("befa7b53-d79d-478f-86b9-120f112b044e", notificationMessage.Metadata.MessageId);
            Assert.Equal("webhook", notificationMessage?.Payload?.Subscription.Transport.Method);
            Assert.Equal("1337", notificationMessage?.Payload?.Subscription.Condition.BroadcasterUserId);
        }

        [Fact]
        public async Task MessageProcessing_IsChannelVIPRemove()
        {
            string messageString = await HelperFunctions.LoadNotificationAsync(
                "channel.vip.remove",
                "1",
                AddPath,
                "ChannelVIPRemove.json"
                );
            // Act
            var result = await MessageProcessing.DeserializeMessageAsync(messageString);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<WebSocketNotificationMessage>(result);
            var notificationMessage = (WebSocketNotificationMessage)result;
            var Event = notificationMessage?.Payload?.Event;
            Assert.IsType<ChannelVIPRemoveEvent>(Event);
            var VIPRemove = (ChannelVIPRemoveEvent)Event;
            Assert.Equal("1234", VIPRemove.UserId);
            Assert.Equal("befa7b53-d79d-478f-86b9-120f112b044e", notificationMessage.Metadata.MessageId);
            Assert.Equal("webhook", notificationMessage?.Payload?.Subscription.Transport.Method);
            Assert.Equal("1337", notificationMessage?.Payload?.Subscription.Condition.BroadcasterUserId);
        }

        [Fact]
        public async Task MessageProcessing_IsChannelWarningAcknowledge()
        {
            string messageString = await HelperFunctions.LoadNotificationAsync(
                "channel.warning.acknowledge",
                "1",
                AddPath,
                "ChannelWarningAcknowledge.json"
                );
            // Act
            var result = await MessageProcessing.DeserializeMessageAsync(messageString);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<WebSocketNotificationMessage>(result);
            var notificationMessage = (WebSocketNotificationMessage)result;
            var Event = notificationMessage?.Payload?.Event;
            Assert.IsType<ChannelWarningAcknowledgeEvent>(Event);
            var warnAck = (ChannelWarningAcknowledgeEvent)Event;
            Assert.Equal("141981764", warnAck.UserId);
            Assert.Equal("befa7b53-d79d-478f-86b9-120f112b044e", notificationMessage.Metadata.MessageId);
            Assert.Equal("webhook", notificationMessage?.Payload?.Subscription.Transport.Method);
            Assert.Equal("423374343", notificationMessage?.Payload?.Subscription.Condition.BroadcasterUserId);
        }

        [Fact]
        public async Task MessageProcessing_IsChannelWarningSend()
        {
            string messageString = await HelperFunctions.LoadNotificationAsync(
                "channel.warning.send",
                "1",
                AddPath,
                "ChannelWarningSend.json"
                );
            // Act
            var result = await MessageProcessing.DeserializeMessageAsync(messageString);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<WebSocketNotificationMessage>(result);
            var notificationMessage = (WebSocketNotificationMessage)result;
            var Event = notificationMessage?.Payload?.Event;
            Assert.IsType<ChannelWarningSendEvent>(Event);
            var warnSend = (ChannelWarningSendEvent)Event;
            Assert.Equal("141981764", warnSend.UserId);
            Assert.Equal("cut it out", warnSend.Reason);
            Assert.Equal("befa7b53-d79d-478f-86b9-120f112b044e", notificationMessage.Metadata.MessageId);
            Assert.Equal("webhook", notificationMessage?.Payload?.Subscription.Transport.Method);
            Assert.Equal("423374343", notificationMessage?.Payload?.Subscription.Condition.BroadcasterUserId);
        }

        [Fact]
        public async Task MessageProcessing_IsStreamOffline()
        {
            string messageString = await HelperFunctions.LoadNotificationAsync(
                "stream.offline",
                "1",
                AddPath,
                "StreamOffline.json"
                );
            // Act
            var result = await MessageProcessing.DeserializeMessageAsync(messageString);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<WebSocketNotificationMessage>(result);
            var notificationMessage = (WebSocketNotificationMessage)result;
            var Event = notificationMessage?.Payload?.Event;
            Assert.IsType<StreamOfflineEvent>(Event);
            var streamOffline = (StreamOfflineEvent)Event;
            Assert.Equal("Cool_User", streamOffline.BroadcasterUserName);
            Assert.Equal("befa7b53-d79d-478f-86b9-120f112b044e", notificationMessage.Metadata.MessageId);
            Assert.Equal("webhook", notificationMessage?.Payload?.Subscription.Transport.Method);
            Assert.Equal("1337", notificationMessage?.Payload?.Subscription.Condition.BroadcasterUserId);
        }

        public async Task MessageProcessing_IsStreamOnline()
        {
            string messageString = await HelperFunctions.LoadNotificationAsync(
                "stream.online",
                "1",
                AddPath,
                "StreamOnline.json"
                );
            // Act
            var result = await MessageProcessing.DeserializeMessageAsync(messageString);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<WebSocketNotificationMessage>(result);
            var notificationMessage = (WebSocketNotificationMessage)result;
            var Event = notificationMessage?.Payload?.Event;
            Assert.IsType<StreamOnlineEvent>(Event);
            var streamOnline = (StreamOnlineEvent)Event;
            Assert.Equal("live", streamOnline.Type);
            Assert.Equal("befa7b53-d79d-478f-86b9-120f112b044e", notificationMessage.Metadata.MessageId);
            Assert.Equal("webhook", notificationMessage?.Payload?.Subscription.Transport.Method);
            Assert.Equal("423374343", notificationMessage?.Payload?.Subscription.Condition.BroadcasterUserId);
        }

        //UserAuthGrand is for webhook
        //UserAuthRevoke is for webhook
        //UserUpdate is anomalous
        //WhisperReceiver is anomalous
    }
}