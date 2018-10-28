using System;

namespace AsyncTwitch
{
    public struct ChatUser
    {
        //The Twitch username as seen in chat.
        public string DisplayName { get; }
        //The Color of the user in the HEX RGB format: #FFFFFF
        public string Color { get; }
        //The ID of the user.
        public string UserID { get;  }
        //If the user is a moderator in the channel.
        public bool IsMod { get; }
        //If the user is the broadcaster.
        public bool IsBroadcaster { get; }
        //All the badges the user has.
        public string[] Badges { get; }

        public ChatUser(string displayName, string color, string userId, bool isMod, bool isBroadcaster, string[] badges)
        {
            DisplayName = displayName;
            Color = color;
            UserID = userId;
            IsMod = isMod;
            IsBroadcaster = isBroadcaster;
            Badges = badges;
        }
    }
}
