using System;
using System.Linq;

namespace AsyncTwitch
{
    public class ChatUser
    {
        //The Twitch username as seen in chat.
        public string DisplayName;
        //The Color of the user in the HEX RGB format: #FFFFFF
        public string Color;
        //The ID of the user.
        public string UserID;
        //If the user is a moderator in the channel.
        public bool IsMod;
        //If the user is the broadcaster.
        public bool IsBroadcaster;
        //Is the user a Subscriber to the channel.
        public bool IsSubscriber;
        //The VIP status of the user
        public bool IsVIP;
        //All the badges the user has.
        public Badge[] Badges;

        public ChatUser()
        {
            DisplayName = "";
            Color = "";
            UserID = "";
            IsMod = false;
            IsBroadcaster = false;
            Badges = new Badge[0];
            IsSubscriber = false;
            IsVIP = false;
        }

        public override string ToString()
        {
            string returnString = "User: " + DisplayName +
                                  "\nColor: " + Color +
                                  "\nUser ID: " + UserID +
                                  "\nIs Moderator: " + IsMod +
                                  "\nIs Broadcaster: " + IsBroadcaster +
                                  "\nIs Subscriber: " + IsSubscriber + 
                                  "\nBadges: ";

            return Badges.Aggregate(returnString, (current, badge) => current + ("\n\tBadge Name: " + badge.BadgeName + "\n\tBadge Version: " + badge.BadgeVersion));
        }
    }

    public class Badge
    {
        public string BadgeName;
        public int BadgeVersion;

        public Badge(string name, int version)
        {
            BadgeName = name;
            BadgeVersion = version;
        }

        public Badge()
        {
            BadgeName = "";
            BadgeVersion = 0;
        }

        public override string ToString()
        {
            return "Badge Name: " + BadgeName + "\nBadge Version: " + BadgeVersion;
        }
    }
}
