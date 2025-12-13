using System.Runtime.Serialization;

namespace Twitch.EventSub.SubsRegister
{
    public enum RegisterKeys
    {
        // Webhook-only events (not supported in WebSocket/EventSub)
        // [EnumMember(Value = "drop.entitlement.grant")]
        // DropEntitlementGrant,
        
        // [EnumMember(Value = "extension.bits_transaction.create")]
        // ExtensionBitsTransactionCreate,
        
        // [EnumMember(Value = "user.authorization.grant")]
        // UserAuthorizationGrant,
        
        // [EnumMember(Value = "user.authorization.revoke")]
        // UserAuthorizationRevoke,
        
        [EnumMember(Value = "user.whisper.message")]
        UserWhisperReceived,
        
        [EnumMember(Value = "automod.message.hold")]
        AutomodMessageHold,
        
        [EnumMember(Value = "automod.message.hold")]
        AutomodMessageHoldV2,
        
        [EnumMember(Value = "automod.message.update")]
        AutomodMessageUpdate,
        
        [EnumMember(Value = "automod.message.update")]
        AutomodMessageUpdateV2,
        
        [EnumMember(Value = "automod.terms.update")]
        AutomodTermsUpdate,
        
        [EnumMember(Value = "automod.settings.update")]
        AutomodSettingsUpdate,
        
        [EnumMember(Value = "conduit.shard.disabled")]
        ConduitShardDisabled,
        
        [EnumMember(Value = "channel.ad_break.begin")]
        ChannelAdBreakBegin,
        
        [EnumMember(Value = "channel.ban")]
        ChannelBan,
        
        [EnumMember(Value = "channel.follow")]
        ChannelFollow,
        
        [EnumMember(Value = "channel.goal.begin")]
        ChannelGoalBegin,
        
        [EnumMember(Value = "channel.goal.end")]
        ChannelGoalEnd,
        
        [EnumMember(Value = "channel.goal.progress")]
        ChannelGoalProgress,
        
        [EnumMember(Value = "channel.guest_star_guest.update")]
        ChannelGuestStarGuestUpdate,
        
        [EnumMember(Value = "channel.guest_star_session.begin")]
        ChannelGuestStarSessionBegin,
        
        [EnumMember(Value = "channel.guest_star_session.end")]
        ChannelGuestStarSessionEnd,
        
        [EnumMember(Value = "channel.guest_star_settings.update")]
        ChannelGuestStarSettingsUpdate,
        
        [EnumMember(Value = "channel.hype_train.begin")]
        ChannelHypeTrainBegin,
        
        [EnumMember(Value = "channel.hype_train.end")]
        ChannelHypeTrainEnd,
        
        [EnumMember(Value = "channel.hype_train.progress")]
        ChannelHypeTrainProgress,
        
        [EnumMember(Value = "channel.charity_campaign.progress")]
        ChannelCharityCampaignProgress,
        
        [EnumMember(Value = "channel.charity_campaign.start")]
        ChannelCharityCampaignStart,
        
        [EnumMember(Value = "channel.charity_campaign.stop")]
        ChannelCharityCampaignStop,
        
        [EnumMember(Value = "channel.charity_campaign.donate")]
        ChannelCharityDonation,
        
        [EnumMember(Value = "channel.chat.clear")]
        ChannelChatClear,
        
        [EnumMember(Value = "channel.chat.clear_user_messages")]
        ChannelChatClearUserMessages,
        
        [EnumMember(Value = "channel.chat.message")]
        ChannelChatMessage,
        
        [EnumMember(Value = "channel.chat.message_delete")]
        ChannelChatMessageDelete,
        
        [EnumMember(Value = "channel.chat.notification")]
        ChannelChatNotification,
        
        [EnumMember(Value = "channel.chat_settings.update")]
        ChannelChatSettingsUpdate,
        
        [EnumMember(Value = "channel.chat.user_message_hold")]
        ChannelChatUserMessageHold,
        
        [EnumMember(Value = "channel.chat.user_message_update")]
        ChannelChatUserMessageUpdate,
        
        [EnumMember(Value = "channel.shared_chat.begin")]
        ChannelSharedChatSessionBegin,
        
        [EnumMember(Value = "channel.shared_chat.update")]
        ChannelSharedChatSessionUpdate,
        
        [EnumMember(Value = "channel.shared_chat.end")]
        ChannelSharedChatSessionEnd,
        
        [EnumMember(Value = "channel.cheer")]
        ChannelCheer,
        
        [EnumMember(Value = "channel.moderator.add")]
        ChannelModeratorAdd,
        
        [EnumMember(Value = "channel.moderator.remove")]
        ChannelModeratorRemove,
        
        [EnumMember(Value = "channel.channel_points_automatic_reward_redemption.add")]
        ChannelPointsAutomaticRewardRedemptionAdd,
        
        [EnumMember(Value = "channel.channel_points_automatic_reward_redemption.add")]
        ChannelPointsAutomaticRewardRedemptionAddV2,
        
        [EnumMember(Value = "channel.channel_points_custom_reward.add")]
        ChannelPointsCustomRewardAdd,
        
        [EnumMember(Value = "channel.channel_points_custom_reward_redemption.add")]
        ChannelPointsCustomRewardRedemptionAdd,
        
        [EnumMember(Value = "channel.channel_points_custom_reward_redemption.update")]
        ChannelPointsCustomRewardRedemptionUpdate,
        
        [EnumMember(Value = "channel.channel_points_custom_reward.remove")]
        ChannelPointsCustomRewardRemove,
        
        [EnumMember(Value = "channel.channel_points_custom_reward.update")]
        ChannelPointsCustomRewardUpdate,
        
        [EnumMember(Value = "channel.poll.begin")]
        ChannelPollBegin,
        
        [EnumMember(Value = "channel.poll.progress")]
        ChannelPollProgress,
        
        [EnumMember(Value = "channel.poll.end")]
        ChannelPollEnd,
        
        [EnumMember(Value = "channel.prediction.begin")]
        ChannelPredictionBegin,
        
        [EnumMember(Value = "channel.prediction.progress")]
        ChannelPredictionProgress,
        
        [EnumMember(Value = "channel.prediction.lock")]
        ChannelPredictionLock,
        
        [EnumMember(Value = "channel.prediction.end")]
        ChannelPredictionEnd,
        
        [EnumMember(Value = "channel.raid")]
        ChannelRaid,
        
        [EnumMember(Value = "channel.shield_mode.begin")]
        ChannelShieldModeBegin,
        
        [EnumMember(Value = "channel.shield_mode.end")]
        ChannelShieldModeEnd,
        
        [EnumMember(Value = "channel.shoutout.create")]
        ChannelShoutoutCreate,
        
        [EnumMember(Value = "channel.shoutout.receive")]
        ChannelShoutoutReceived,
        
        [EnumMember(Value = "channel.subscribe")]
        ChannelSubscribe,
        
        [EnumMember(Value = "channel.subscription.end")]
        ChannelSubscriptionEnd,
        
        [EnumMember(Value = "channel.subscription.gift")]
        ChannelSubscriptionGift,
        
        [EnumMember(Value = "channel.subscription.message")]
        ChannelSubscriptionMessage,
        
        [EnumMember(Value = "channel.suspicious_user.message")]
        ChannelSuspiciousUserMessage,
        
        [EnumMember(Value = "channel.suspicious_user.update")]
        ChannelSuspiciousUserUpdate,
        
        [EnumMember(Value = "channel.unban")]
        ChannelUnban,
        
        [EnumMember(Value = "channel.unban_request.create")]
        ChannelUnbanRequestCreate,
        
        [EnumMember(Value = "channel.unban_request.resolve")]
        ChannelUnbanRequestResolve,
        
        [EnumMember(Value = "channel.bits.use")]
        ChannelBitsUse,
        
        [EnumMember(Value = "channel.update")]
        ChannelUpdate,
        
        [EnumMember(Value = "channel.vip.add")]
        ChannelVIPAdd,
        
        [EnumMember(Value = "channel.vip.remove")]
        ChannelVIPRemove,
        
        [EnumMember(Value = "channel.warning.acknowledge")]
        ChannelWarningAcknowledge,
        
        [EnumMember(Value = "channel.warning.send")]
        ChannelWarningSend,
        
        [EnumMember(Value = "stream.offline")]
        StreamOffline,
        
        [EnumMember(Value = "stream.online")]
        StreamOnline,
        
        [EnumMember(Value = "user.update")]
        UserUpdate
    }
    
    public static class RegisterKeysExtensions
    {
        private static readonly Dictionary<RegisterKeys, string> _enumToString;
        private static readonly Dictionary<string, RegisterKeys> _stringToEnum;
        
        static RegisterKeysExtensions()
        {
            _enumToString = new Dictionary<RegisterKeys, string>();
            _stringToEnum = new Dictionary<string, RegisterKeys>();
            
            foreach (RegisterKeys key in Enum.GetValues(typeof(RegisterKeys)))
            {
                var memberInfo = typeof(RegisterKeys).GetMember(key.ToString())[0];
                var attribute = memberInfo.GetCustomAttributes(typeof(EnumMemberAttribute), false)
                    .FirstOrDefault() as EnumMemberAttribute;
                
                if (attribute != null)
                {
                    _enumToString[key] = attribute.Value;
                    
                    // Store first occurrence for reverse lookup
                    if (!_stringToEnum.ContainsKey(attribute.Value))
                    {
                        _stringToEnum[attribute.Value] = key;
                    }
                }
            }
        }
        
        /// <summary>
        /// Converts the enum to its string representation
        /// </summary>
        public static string ToEventString(this RegisterKeys key)
        {
            return _enumToString.TryGetValue(key, out var value) ? value : key.ToString();
        }
        
        /// <summary>
        /// Parses a string to its RegisterKeys enum value
        /// </summary>
        public static RegisterKeys FromEventString(string eventString)
        {
            if (_stringToEnum.TryGetValue(eventString, out var key))
            {
                return key;
            }
            throw new ArgumentException($"Unknown event string: {eventString}");
        }
        
        /// <summary>
        /// Tries to parse a string to its RegisterKeys enum value
        /// </summary>
        public static bool TryFromEventString(string eventString, out RegisterKeys key)
        {
            return _stringToEnum.TryGetValue(eventString, out key);
        }
        
        /// <summary>
        /// Gets all unique event strings
        /// </summary>
        public static IEnumerable<string> GetAllEventStrings()
        {
            return _stringToEnum.Keys;
        }
        
        /// <summary>
        /// Gets all enum values
        /// </summary>
        public static IEnumerable<RegisterKeys> GetAllKeys()
        {
            return Enum.GetValues(typeof(RegisterKeys)).Cast<RegisterKeys>();
        }
    }
}