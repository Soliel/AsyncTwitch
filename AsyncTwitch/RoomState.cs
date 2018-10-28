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
    public struct RoomState
    {
        //The Language of the broadcaster, e.g. en or fi
        public string BroadcasterLang { get; }
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

        public List<ChatUserListing> UserList { get; private set; }
        private Timer _cleaningTimer; //We have to keep a refrence to the timer to avoid garbage collection.


        public RoomState(string lang, bool emoteOnly, int followersOnly, bool r9KMode, int slowMode, bool subOnly)
        {
            BroadcasterLang = lang;
            EmoteOnly = emoteOnly;
            FollowersOnly = followersOnly;
            R9KMode = r9KMode;
            SlowMode = slowMode;
            SubOnly = subOnly;

            UserList = new List<ChatUserListing>();
            _cleaningTimer = null;

            // ReSharper disable once AssignNullToNotNullAttribute
            _cleaningTimer = new Timer(RemoveInactiveUsers, UserList, TimeSpan.Zero, TimeSpan.FromMinutes(30));
        }

        private void RemoveInactiveUsers(object chatListing)
        {
            List<ChatUserListing> userList = (List<ChatUserListing>) chatListing;
            userList.RemoveAll(x => (!x.User.IsMod && !x.User.IsBroadcaster) && DateTime.Now - x.LastMsgTime >= TimeSpan.FromMinutes(30));
            UserList = userList;
        }

        //If someone is being added to the list we can assume they just sent a message.
        public void AddUserToList(ChatUser user)
        {
            UserList.Add(new ChatUserListing(user, DateTime.Now));
        }

        public void RemoveUserFromList(string userID)
        {
            UserList.RemoveAll(x => x.User.UserID == userID);
        }
    }

    public struct ChatUserListing
    {
        public ChatUser User { get; set; }
        public DateTime LastMsgTime { get; set; }

        public ChatUserListing(ChatUser user, DateTime lastMessageTime)
        {
            User = user;
            LastMsgTime = lastMessageTime;
        }
    }
}
