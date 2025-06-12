﻿using Newtonsoft.Json;
using Twitch.EventSub.Messages.NotificationMessage.Events.ChannelChat;
using Twitch.EventSub.Messages.NotificationMessage.Events.ChannelSubscription;

namespace Twitch.EventSub.Messages.NotificationMessage.Events.Automod.Models
{
    public class Fragments
    {
        [JsonProperty("emotes")]
        public List<Emote> Emotes { get; set; }

        [JsonProperty("cheermotes")]
        public List<Cheermote> Cheermotes { get; set; }
    }
}