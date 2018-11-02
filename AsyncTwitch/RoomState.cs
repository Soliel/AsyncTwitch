using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AsyncTwitch
{
    /*
     * This struct contains 1-1 bindings with https://dev.twitch.tv/docs/irc/tags/#roomstate-twitch-tags.
     * It will also contain a list of users and time since their last message.
     * Holding this time is because in rooms with > 100 chatters JOIN and PART messages are not sent for normal users,
     * So we need another way to keep track. 
     */
    public class RoomState
    {
        //The Language of the broadcaster, e.g. en or fi
        public string BroadcasterLang { get; set; }
        //Is Emote only mode on?
        public bool EmoteOnly { get; set; }
        //Followers Only, valid values are -1(disabled), 0(all followers can chat) or
        //A positive number that is the amount of minutes until they can chat.
        public int FollowersOnly { get; set; }
        //is r9k mode enabled
        public bool R9KMode { get; set; }
        //How many seconds users must wait between messages.
        public int SlowMode { get; set; }
        //is sub only mode on?
        public bool SubOnly { get; set; }
        //The Id of the Channel
        public string RoomID { get; set; }

        public List<ChatUserListing> UserList { get; private set; }
        private Timer _cleaningTimer; //We have to keep a refrence to the timer to avoid garbage collection.

        public RoomState()
        {
            RoomID = "";
            BroadcasterLang = "";
            EmoteOnly = false;
            FollowersOnly = 0;
            R9KMode = false;
            SlowMode = 0;
            SubOnly = false;

            UserList = new List<ChatUserListing>();
            _cleaningTimer = null;

            // ReSharper disable once AssignNullToNotNullAttribute
            _cleaningTimer = new Timer(RemoveInactiveUsers, UserList, TimeSpan.Zero, TimeSpan.FromMinutes(30));
        }

        private void RemoveInactiveUsers(object chatListing)
        {
            try
            {
                List<ChatUserListing> userList = (List<ChatUserListing>) chatListing;
                userList.RemoveAll(x =>
                    (!x.User.IsMod && !x.User.IsBroadcaster) &&
                    DateTime.Now - x.LastMsgTime >= TimeSpan.FromMinutes(30));
                UserList = userList;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        //If someone is being added to the list we can assume they just sent a message.
        public void AddUserToList(ChatUser user)
        {
            try
            {
                UserList.Add(new ChatUserListing(user, DateTime.Now));
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public void RemoveUserFromList(string displayName)
        {
            try
            {
                UserList.RemoveAll(x => x.User.DisplayName == displayName);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public override string ToString()
        {
            string returnString = "Broadcaster Language: " + BroadcasterLang +
                                  "\nEmote Only Mode: " + EmoteOnly +
                                  "\nFollowers Only: " + FollowersOnly +
                                  "\nSubscribers Only: " + SubOnly +
                                  "\nR9K Mode: " + R9KMode +
                                  "\nSlow Mode: " + SlowMode + "\n\n";

            foreach (ChatUserListing chatUserListing in UserList)
            {
                returnString += chatUserListing.ToString();
            }

            return returnString;
        }
    }

    public class ChatUserListing
    {
        public ChatUser User { get; set; }
        public DateTime LastMsgTime { get; set; }

        public ChatUserListing(ChatUser user, DateTime lastMessageTime)
        {
            User = user;
            LastMsgTime = lastMessageTime;
        }

        public ChatUserListing()
        {
            User = new ChatUser();
            LastMsgTime = DateTime.MinValue;
        }

        public void UpdateTime()
        {
            LastMsgTime = DateTime.Now;
        }

        public override string ToString()
        {
            string returnString = "Chat User Listing: \n";
            returnString += User.ToString();
            returnString += "\n\nLast Message: " + LastMsgTime;

            return returnString;
        }
    }
}
