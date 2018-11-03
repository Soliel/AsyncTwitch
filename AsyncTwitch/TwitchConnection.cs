using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using JetBrains.Annotations;
using UnityEngine;

/*
 * TODO: Logging.
 */

namespace AsyncTwitch
{
    [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
    public class TwitchConnection : IrcConnection
    {
        public static TwitchConnection Instance;
        private readonly Queue<string> _messageQueue = new Queue<string>();

        //How long since the last message before we send another.
        private readonly TimeSpan _rateLimit = new TimeSpan(0, 0, 0, 1, 500); 

        private bool _isConnected;
        private DateTime _lastMessageTime = DateTime.Now;
        private Config _loginInfo;

        public Encoding Utf8NoBom = new UTF8Encoding(false);
        public RoomState RoomState = new RoomState();
        public static event Action<TwitchConnection, TwitchMessage> OnMessageReceived;
        public static event Action<TwitchConnection> OnConnected;
        public static event Action<TwitchConnection> OnChatJoined;
        public static event Action<TwitchConnection, ChatUser> OnChatParted;
        public static event Action<TwitchConnection, RoomState> OnRoomStateChanged;

        public static void OnLoad()
        {
            if (Instance != null) return;
            new GameObject("Twitch connection").AddComponent<TwitchConnection>();
        }

        [UsedImplicitly]
        private void Awake()
        {
            Instance = this;
        }

        [UsedImplicitly]
        public void StartConnection()
        {
            if (_isConnected) return;

            _loginInfo = Config.LoadFromJSON();
            if (_loginInfo.Username == "") return;
            Connect("irc.twitch.tv", 6667);
            RoomState = new RoomState();
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

        //This is a simple Queueing system to avoid the ratelimit.
        //We can expand upon this later by finding how many messages we've sent in the last 30 seconds.
        [UsedImplicitly]
        public void SendChatMessage(string msg)
        {
            if (DateTime.Now - _lastMessageTime >= _rateLimit) Send("PRIVMSG #" + _loginInfo.ChannelName + " :" + msg);
            _messageQueue.Enqueue("PRIVMSG #" + _loginInfo.ChannelName + " :" + msg);
            DateTime timeUntilRateLimit = _lastMessageTime.Add(_rateLimit);
            Task.Delay(timeUntilRateLimit - DateTime.Now).ContinueWith(SendMessageFromQueue);
            _lastMessageTime = timeUntilRateLimit;
        }

        public void SendRawMessage(string msg)
        {
            if (DateTime.Now - _lastMessageTime >= _rateLimit) Send(msg);
            _messageQueue.Enqueue(msg);
            DateTime timeUntilRateLimit = _lastMessageTime.Add(_rateLimit);
            Task.Delay(timeUntilRateLimit - DateTime.Now).ContinueWith(SendMessageFromQueue);
            _lastMessageTime = timeUntilRateLimit;
        }

        //The Send method appends CR LF to the end of every message so we don't have to worry.
        private void SendMessageFromQueue(Task task)
        {
            Send(_messageQueue.Dequeue());
        }

        #region REGEX Lines

        private readonly Regex _joinRX =
            new Regex(@"^:(?<User>[A-Za-z0-9]+)![A-Za-z0-9]+@[A-Za-z0-9]+\.tmi\.twitch\.tv\sJOIN\s",
                RegexOptions.Compiled);

        private readonly Regex _partRX =
            new Regex(@"^:(?<User>[A-Za-z0-9]+)![A-Za-z0-9]+@[A-Za-z0-9]+\.tmi\.twitch\.tv\sPART\s",
                RegexOptions.Compiled);

        private readonly Regex _badgeRX = new Regex(@"(?<=\@badges=|,)\w+\/\d+", RegexOptions.Compiled);
        private readonly Regex _emoteRX = new Regex(@"(?<=;emotes=)\d+:(\d+-\d+[,/;])+", RegexOptions.Compiled);

        private readonly Regex _roomStateRX =
            new Regex(@"@(?<Tags>.+)\s:tmi.twitch.tv ROOMSTATE #[A-Za-z0-9]+", RegexOptions.Compiled);

        #endregion

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

        #region MessageParsing

        public override void ProcessMessage(byte[] msg)
        {
            string stringMsg = Utf8NoBom.GetString(msg);
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

                OnRoomStateChanged?.BeginInvoke(this, RoomState, OnRoomStateChangedCallback, null);
                return;
            }

            TwitchMessage message = new TwitchMessage();
            string[] splitMsg = stringMsg.Split(new[] {" :"}, 3, StringSplitOptions.RemoveEmptyEntries); 
            if (splitMsg.Length >= 3)
                message.Content = splitMsg[2];
            message.RawMessage = stringMsg;
            splitMsg = splitMsg[0].Split(';');

            foreach (string msgTag in splitMsg)
            {
                string[] splitTag = msgTag.Split('=');
                if (splitMsg.Length < 1) break;
                if (splitTag.Length < 2) continue;
                switch (splitTag[0])
                {
                    case "bits":
                        message.GaveBits = true;
                        message.BitAmount = int.Parse(splitTag[1]);
                        break;
                    case "display-name":
                        message.Author = FindChatUser(splitTag[1], stringMsg);
                        break;
                    case "emotes":
                        if (_emoteRX.IsMatch(stringMsg)) message.Emotes = ParseEmotes(splitTag[1]);
                        break;
                    case "id":
                        message.Id = splitTag[1];
                        break;
                }
            }

            OnMessageReceived?.BeginInvoke(this, message, OnMessageReceivedCallback, 0);
        }

        //These events don't work very well due to caching and they don't have much data to begin with. don't use them.
        private bool FilterJoinPart(string msg)
        {
            if (_joinRX.IsMatch(msg)) OnChatJoined?.BeginInvoke(this, OnJoinEventCallback, msg);

            if (_partRX.IsMatch(msg))
            {
                string username = _partRX.Match(msg).Groups["User"].Value;
                ChatUser partedUser = RoomState.UserList.Find(x => x.User.DisplayName == username).User;
                RoomState.RemoveUserFromList(username);
                OnChatParted?.BeginInvoke(this, partedUser, OnPartEvenCallback, msg);
                return true;
            }

            return false;
        }

        private void FilterRoomState(string msg)
        {
            string tags = _roomStateRX.Match(msg).Groups["Tags"].Value;
            string[] tagArray = tags.Split(';');
            foreach (string msgTag in tagArray)
            {
                string[] tagParts = msgTag.Split('=');

                if (tagParts.Length < 2)
                    continue;

                switch (tagParts[0])
                {
                    case "broadcaster-lang":
                        RoomState.BroadcasterLang = tagParts[1];
                        break;
                    case "emote-only":
                        RoomState.EmoteOnly = int.Parse(tagParts[1]) > 0;
                        break;
                    case "followers-only":
                        RoomState.FollowersOnly = int.Parse(tagParts[1]);
                        break;
                    case "r9k":
                        RoomState.R9KMode = int.Parse(tagParts[1]) > 0;
                        break;
                    case "slow":
                        RoomState.SlowMode = int.Parse(tagParts[1]);
                        break;
                    case "subs-only":
                        RoomState.SubOnly = int.Parse(tagParts[1]) > 0;
                        break;
                    case "room-id":
                        RoomState.RoomID = tagParts[1];
                        break;
                }
            }
        }

        public ChatUser FindChatUser(string displayName, string rawMessage)
        {
            if (RoomState.UserList.Exists(x => x.User.DisplayName == displayName))
            {
                return RoomState.UserList.Find(x => x.User.DisplayName == displayName).User;
            }

            ChatUser author = CreateChatUser(rawMessage);
            if (rawMessage.Contains(":tmi.twitch.tv PRIVMSG")) RoomState.AddUserToList(author);
            return author;
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

                if (badgeInfo[0] == "broadcaster") newUser.IsBroadcaster = true;

                if (badgeInfo[0] == "subscriber") newUser.IsSubscriber = true;

                i++;
            }

            string[] tagFilteredString = chatJoin.Split(':');
            tagFilteredString = tagFilteredString[0].Split(';');

            foreach (string msgTag in tagFilteredString)
            {
                string[] tagParts = msgTag.Split('=');

                if (tagParts.Length < 2)
                    continue;

                switch (tagParts[0])
                {
                    case "color":
                        newUser.Color = tagParts[1];
                        break;
                    case "display-name":
                        newUser.DisplayName = tagParts[1];
                        break;
                    case "mod":
                        newUser.IsMod = int.Parse(tagParts[1]) > 0;
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
            for (int i = 0; i < emoteSplit.Length; i++)
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
    }
}