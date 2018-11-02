using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;

/*
 * TODO: Logging.
 */

namespace AsyncTwitch
{
    public class TwitchConnection :  IrcConnection
    {
        public static event Action<TwitchConnection, TwitchMessage> OnMessageReceived;
        public static event Action<TwitchConnection> OnConnected;
        public static event Action<TwitchConnection> OnChatJoined;
        public static event Action<TwitchConnection, ChatUser> OnChatParted;
        public static event Action<TwitchConnection, RoomState> OnRoomStateChanged;

        public static TwitchConnection Instance;

        public Encoding _utf8noBOM = new UTF8Encoding(false);
        public RoomState roomState = new RoomState();

        private readonly TimeSpan RateLimit = new TimeSpan(0, 0, 0, 1 ,500); //How long since the last message before we send another.
        private Config _loginInfo;
        private bool _isConnected;
        private Queue<string> _messageQueue = new Queue<string>();
        private DateTime _lastMessageTime = DateTime.Now;

        #region REGEX Lines
        private Regex _joinRX = new Regex(@"^:(?<User>[A-Za-z0-9]+)![A-Za-z0-9]+@[A-Za-z0-9]+\.tmi\.twitch\.tv\sJOIN\s", RegexOptions.Compiled);
        private Regex _partRX = new Regex(@"^:(?<User>[A-Za-z0-9]+)![A-Za-z0-9]+@[A-Za-z0-9]+\.tmi\.twitch\.tv\sPART\s", RegexOptions.Compiled);
        private Regex _badgeRX = new Regex(@"(?<=\@badges=|,)\w+\/\d+", RegexOptions.Compiled);
        //private Regex _messageRX = new Regex(@"(?<=[A-Za-z0-9]+![A-Za-z0-9]+\@[A-Za-z0-9]+\.tmi\.twitch\.tv\sPRIVMSG\s#\w+\s:).+", RegexOptions.Compiled);
        private Regex _emoteRX = new Regex(@"(?<=;emotes=)\d+:(\d+-\d+[,/;])+", RegexOptions.Compiled);
        private Regex _roomStateRX = new Regex(@"@(?<Tags>.+)\s:tmi.twitch.tv ROOMSTATE #[A-Za-z0-9]+", RegexOptions.Compiled);
        #endregion

        public static void OnLoad()
        {
            if (Instance != null) return;
            new GameObject("Twitch connection").AddComponent<TwitchConnection>();
        }

        private void Awake()
        {
            Instance = this;
            StartConnection(); //| Debug stuff
        }

        #region EventCallbacks

        private void OnConnectedEventCallback(IAsyncResult ar)
        {
            //Console.WriteLine("Connected Callback");
            OnConnected.EndInvoke(ar);
        }

        private void OnJoinEventCallback(IAsyncResult ar)
        {
            //Console.WriteLine("Join Callback");
            OnChatJoined.EndInvoke(ar);
        }

        private void OnPartEvenCallback(IAsyncResult ar)
        {
            //Console.WriteLine("Part Callback");
            OnChatParted.EndInvoke(ar);
        }

        private void OnMessageReceivedCallback(IAsyncResult ar)
        {
            //Console.WriteLine("Message Callback");
            OnMessageReceived.EndInvoke(ar);
        }

        private void OnRoomStateChangedCallback(IAsyncResult ar)
        {
            //Console.WriteLine("RoomState Callback");
            OnRoomStateChanged.EndInvoke(ar);
        }
        #endregion

        public void StartConnection()
        {
            if (_isConnected) return;

            _loginInfo = Config.LoadFromJSON();
            if (_loginInfo.Username == "") return;
            Connect("irc.twitch.tv", 6667);
            roomState = new RoomState();
            _isConnected = true;
        }

        public override void OnConnect()
        {
            SendRawMessage("CAP REQ :twitch.tv/membership twitch.tv/commands twitch.tv/tags");
            SendRawMessage("PASS " + _loginInfo.OauthKey);
            SendRawMessage("NICK " + _loginInfo.Username);
            SendRawMessage("JOIN #" + _loginInfo.ChannelName);
            OnConnected?.BeginInvoke(this, OnConnectedEventCallback, null);
        }

        #region MessageParsing
        public override void ProcessMessage(byte[] msg)
        {
            string stringMsg = _utf8noBOM.GetString(msg);
            //Console.WriteLine(stringMsg);
            if (FilterJoinPart(stringMsg)) return;
            if (_roomStateRX.IsMatch(stringMsg))
            {

                try
                {
                    FilterRoomState(stringMsg);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }

                OnRoomStateChanged?.BeginInvoke(this, roomState, OnRoomStateChangedCallback, null);
                return;
            }

            TwitchMessage message = new TwitchMessage();
            string[] splitMsg = stringMsg.Split(new string[]{" :"}, 3, StringSplitOptions.RemoveEmptyEntries); //a space colon is a better indicator of message sections.
            if(splitMsg.Length >= 3)
                message.Content = splitMsg[2];
            message.RawMessage = stringMsg;
            splitMsg = splitMsg[0].Split(';');

            foreach (string Tag in splitMsg)
            {
                string[] splitTag = Tag.Split('=');
                if (splitMsg.Length < 1) break;
                if (splitTag.Length < 2) continue;
                //Console.WriteLine(splitTag[0] + "\n" + splitTag[1]);
                switch (splitTag[0])
                {
                    case "bits":
                        //Console.WriteLine("Found bits tag");
                        message.GaveBits = true;
                        message.BitAmount = int.Parse(splitTag[1]);
                        break;
                    case "display-name":
                        //Console.WriteLine("Found display name");
                        if (roomState.UserList.Exists(x => x.User.DisplayName == splitTag[1])) {
                            message.Author = roomState.UserList.Find(x => x.User.DisplayName == splitTag[1]).User;
                            break;
                        }

                        ChatUser author = CreateChatUser(stringMsg);
                        if(stringMsg.Contains(":tmi.twitch.tv PRIVMSG"))
                            roomState.AddUserToList(author);
                        message.Author = author;
                        break;
                    case "emotes":
                        //Console.WriteLine("Found Emotes");
                        if (_emoteRX.IsMatch(stringMsg))
                        {
                            message.Emotes = ParseEmotes(splitTag[1]);
                        }

                        break;
                    case "id":
                        //Console.WriteLine("Found ID");
                        message.Id = splitTag[1];
                        break;
                    default:
                        break;
                }
            }

            try
            {
                OnMessageReceived?.BeginInvoke(this, message, OnMessageReceivedCallback, 0);
                //Console.WriteLine(message);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            
            
        }

        //These events don't work very well due to caching and they don't have much data to begin with. don't use them.
        private bool FilterJoinPart(string msg)
        {
            try
            {
                /*
                 Left in for development purposes no flame.
                 if (_joinRX.IsMatch(msg))
                {
                    ChatUser joinedUser;
                    string username = _joinRX.Match(msg).Groups["User"].Value;
                    if (roomState.UserList.Exists(x => x.User.DisplayName == username))
                    {
                        joinedUser = roomState.UserList.Find(x => x.User.DisplayName == username).User;
                        roomState.UserList.Find(x => x.User.DisplayName == username).UpdateTime();
                    }
                    else
                    {
                        joinedUser = CreateChatUser(msg);
                        roomState.AddUserToList(joinedUser);
                    }

                    Turns out on Join doesn't return enough info for a user object.
                    OnChatJoined?.BeginInvoke(this, joinedUser, OnJoinEventCallback, null);
                    return true;
                }*/

                if (_joinRX.IsMatch(msg))
                {
                    OnChatJoined?.BeginInvoke(this, OnJoinEventCallback, msg);
                }

                if (_partRX.IsMatch(msg))
                {
                    string username = _partRX.Match(msg).Groups["User"].Value;
                    ChatUser partedUser = roomState.UserList.Find(x => x.User.DisplayName == username).User;
                    roomState.RemoveUserFromList(username);
                    OnChatParted?.BeginInvoke(this, partedUser, OnPartEvenCallback, msg);
                    return true;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }

            return false;
        }

        private void FilterRoomState(string msg)
        {
            string Tags = _roomStateRX.Match(msg).Groups["Tags"].Value;
            string[] TagArray = Tags.Split(';');
            foreach(string Tag in TagArray)
            {
                string[] TagParts = Tag.Split('=');

                if (TagParts.Length < 2)
                    continue;

                switch(TagParts[0])
                {
                    case "broadcaster-lang":
                        roomState.BroadcasterLang = TagParts[1];
                        break;
                    case "emote-only":
                        roomState.EmoteOnly = int.Parse(TagParts[1]) > 0;
                        break;
                    case "followers-only":
                        roomState.FollowersOnly = int.Parse(TagParts[1]);
                        break;
                    case "r9k":
                        roomState.R9KMode = int.Parse(TagParts[1]) > 0;
                        break;
                    case "slow":
                        roomState.SlowMode = int.Parse(TagParts[1]);
                        break;
                    case "subs-only":
                        roomState.SubOnly = int.Parse(TagParts[1]) > 0;
                        break;
                    case "room-id":
                        roomState.RoomID = TagParts[1];
                        break;
                    default:
                        break;
                }
            }
            //Console.WriteLine(roomState.ToString());
        }

        //Called on UserMessaging to create the ChatUser object.
        public ChatUser CreateChatUser(string chatJoin)
        {
            ChatUser newUser = new ChatUser();
            MatchCollection badgesCollection = _badgeRX.Matches(chatJoin);
            newUser.Badges = new Badge[badgesCollection.Count];
            int i = 0;

            foreach (Match match in badgesCollection)
            {
                string[] badgeInfo = match.Value.Split('/');
                Badge badge = new Badge(badgeInfo[0], int.Parse(badgeInfo[1]));
                newUser.Badges[i] = badge;

                if (badgeInfo[0] == "broadcaster")
                {
                    newUser.IsBroadcaster = true;
                }

                if (badgeInfo[0] == "subscriber")
                {
                    newUser.IsSubscriber = true;
                }

                i++;
            }

            string[] tagFilteredString = chatJoin.Split(':');
            tagFilteredString = tagFilteredString[0].Split(';');

            foreach (string Tag in tagFilteredString)
            {
                string[] TagParts = Tag.Split('=');

                if (TagParts.Length < 2)
                    continue;

                switch (TagParts[0])
                {
                    case "color":
                        newUser.Color = TagParts[1];
                        break;
                    case "display-name":
                        newUser.DisplayName = TagParts[1];
                        break;
                    case "mod":
                        newUser.IsMod = int.Parse(TagParts[1]) > 0;
                        break;
                    default:
                        break;
                }
            }

            return newUser;
        }

        public TwitchEmote[] ParseEmotes(string emoteString)
        {
            emoteString.Remove(emoteString.Length - 1); //remove trailing semicolon.
            string[] emoteSplit = emoteString.Split('/');
            
            TwitchEmote[] emoteArray = new TwitchEmote[emoteSplit.Length];
            for (int i = 0; i < emoteSplit.Length; i ++)
            {
                TwitchEmote twitchEmote = new TwitchEmote();
                string[] emoteData = emoteSplit[i].Split(':');
                twitchEmote.Id = emoteData[0];
                string[] emoteIndices = emoteData[1].Split(',');
                twitchEmote.Index = new string[emoteIndices.Length][];

                for (int j = 0; j < emoteIndices.Length; j++)
                {
                    twitchEmote.Index[j] = emoteIndices[j].Split('-');
                }

                emoteArray[i] = twitchEmote;
            }
            return emoteArray;
        }
        #endregion

        //This is a simple Queueing system to avoid the ratelimit.
        //We can expand upon this later by finding how many messages we've sent in the last 30 seconds.
        public void SendChatMessage(string msg)
        {
            if (DateTime.Now - _lastMessageTime >= RateLimit)
            {
                Send("PRIVMSG #" + _loginInfo.ChannelName + " :" + msg);
            }
            _messageQueue.Enqueue("PRIVMSG #"+ _loginInfo.ChannelName + " :" + msg);
            DateTime timeUntilRateLimit = _lastMessageTime.Add(RateLimit);
            Task.Delay(timeUntilRateLimit - DateTime.Now).ContinueWith(SendMessageFromQueue);
            _lastMessageTime = timeUntilRateLimit;
        }

        public void SendRawMessage(string msg)
        {
            if (DateTime.Now - _lastMessageTime >= RateLimit)
            {
                Send(msg);
            }
            _messageQueue.Enqueue(msg);
            DateTime timeUntilRateLimit = _lastMessageTime.Add(RateLimit);
            Task.Delay(timeUntilRateLimit - DateTime.Now).ContinueWith(SendMessageFromQueue);
            _lastMessageTime = timeUntilRateLimit;
        }

        //The Send method appends CR LF to the end of every message so we don't have to worry.
        private void SendMessageFromQueue(Task task)
        {
            Send(_messageQueue.Dequeue());
        }
    }
}
