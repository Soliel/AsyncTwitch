﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
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
    public class RegisteredPlugin
    {
        public event Action<TwitchConnection, TwitchMessage> OnMessageReceived = null;
        public event Action<TwitchConnection> OnConnected = null;
        public event Action<TwitchConnection> OnChatJoined = null;
        public event Action<TwitchConnection, ChatUser> OnChatParted = null;
        public event Action<TwitchConnection, RoomState> OnRoomStateChanged = null;
        public event Action<string> OnRawMessageReceived = null;

        public void TryInvokeOnConnected(TwitchConnection obj)
        {
            OnConnected?.BeginInvoke(obj, OnConnectedEventCallback, null);
        }

        public void TryInvokeOnRawMessageReceived(string stringMsg)
        {
            OnRawMessageReceived?.BeginInvoke(stringMsg, OnRawMessageReceivedCallback, null);
        }

        public void TryInvokeOnRoomStateChanged(TwitchConnection obj, RoomState roomstate)
        {
            OnRoomStateChanged?.BeginInvoke(obj, roomstate, OnRoomStateChangedCallback, null);
        }

        public void TryInvokeOnMessageReceived(TwitchConnection obj, TwitchMessage msg)
        {
            OnMessageReceived?.BeginInvoke(obj, msg, OnMessageReceivedCallback, null);
        }

        public void TryInvokeOnChatJoined(TwitchConnection obj, string msg)
        {
            OnChatJoined?.BeginInvoke(obj, OnJoinEventCallback, msg);
        }

        public void TryInvokeOnChatParted(TwitchConnection obj, ChatUserListing user, string msg)
        {
            OnChatParted?.BeginInvoke(obj, user.User, OnPartEvenCallback, msg);
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

        private void OnRawMessageReceivedCallback(IAsyncResult ar)
        {
            OnRawMessageReceived.EndInvoke(ar);
        }

        #endregion
    }


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
        //public RoomState RoomState = new RoomState();
        public Dictionary<string, RoomState> RoomStates = new Dictionary<string, RoomState>();
        public Dictionary<string, RegisteredPlugin> RegisteredPlugins = new Dictionary<string, RegisteredPlugin>();



        public static void OnLoad()
        {
            if (Instance != null) return;
            new GameObject("Twitch connection").AddComponent<TwitchConnection>();
        }

        [UsedImplicitly]
        private void Awake()
        {
            Instance = this;
            DontDestroyOnLoad(this);
        }

        [UsedImplicitly]
        public void StartConnection()
        {
            if (_isConnected) return;

            _loginInfo = Config.LoadFromJSON();
            if (_loginInfo.Username == "") return;
            Connect("irc.twitch.tv", 6667);
            _isConnected = true;
        }

        public void RegisterPlugin(string pluginIdentifier) {
            if (RegisteredPlugins.ContainsKey(pluginIdentifier)) return;
            RegisteredPlugins[pluginIdentifier] = new RegisteredPlugin();
        }

        public void RegisterOnConnected(string pluginIdentifier, Action<TwitchConnection> callback)
        {
            if (!RegisteredPlugins.ContainsKey(pluginIdentifier)) RegisterPlugin(pluginIdentifier);
            RegisteredPlugins[pluginIdentifier].OnConnected += callback;
        }

        public void RegisterOnRawMessageReceived(string pluginIdentifier, Action<string> callback)
        {
            if (!RegisteredPlugins.ContainsKey(pluginIdentifier)) RegisterPlugin(pluginIdentifier);
            RegisteredPlugins[pluginIdentifier].OnRawMessageReceived += callback;
        }

        public void RegisterOnRoomStateChanged(string pluginIdentifier, Action<TwitchConnection, RoomState> callback)
        {
            if (!RegisteredPlugins.ContainsKey(pluginIdentifier)) RegisterPlugin(pluginIdentifier);
            RegisteredPlugins[pluginIdentifier].OnRoomStateChanged += callback;
        }

        public void RegisterOnMessageReceived(string pluginIdentifier, Action<TwitchConnection, TwitchMessage> callback)
        {
            if (!RegisteredPlugins.ContainsKey(pluginIdentifier)) RegisterPlugin(pluginIdentifier);
            RegisteredPlugins[pluginIdentifier].OnMessageReceived += callback;
        }

        public void RegisterOnChatJoined(string pluginIdentifier, Action<TwitchConnection> callback)
        {
            if (!RegisteredPlugins.ContainsKey(pluginIdentifier)) RegisterPlugin(pluginIdentifier);
            RegisteredPlugins[pluginIdentifier].OnChatJoined += callback;
        }

        public void RegisterOnChatParted(string pluginIdentifier, TwitchConnection obj, Action<TwitchConnection,ChatUser> callback)
        {
            if (!RegisteredPlugins.ContainsKey(pluginIdentifier)) RegisterPlugin(pluginIdentifier);
            RegisteredPlugins[pluginIdentifier].OnChatParted += callback;
        }

        public override void OnConnect()
        {
            SendRawMessage("CAP REQ :twitch.tv/membership twitch.tv/commands twitch.tv/tags");
            SendRawMessage("PASS " + _loginInfo.OauthKey);
            SendRawMessage("NICK " + _loginInfo.Username);
            JoinRoom(_loginInfo.ChannelName);

            foreach (KeyValuePair<string, RegisteredPlugin> kvp in RegisteredPlugins) kvp.Value.TryInvokeOnConnected(this);
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

        [UsedImplicitly]
        public void JoinRoom(string channel)
        {
            if (RoomStates.ContainsKey(channel)) return;

            RoomState newRoomState = new RoomState();
            RoomStates[channel] = newRoomState;
            SendRawMessage("JOIN #" + channel);
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
        private readonly Regex _channelName = new Regex(@":tmi.twitch.tv\s(?<Command>\w+)\s#(?<Channel>[A-Za-z0-9_]+)", RegexOptions.Compiled);

        #endregion

        #region MessageParsing

        public override void ProcessMessage(byte[] msg)
        {
            string stringMsg = Utf8NoBom.GetString(msg);
            foreach (KeyValuePair<string, RegisteredPlugin> kvp in RegisteredPlugins) kvp.Value.TryInvokeOnRawMessageReceived(stringMsg);

            if (stringMsg == "PING :tmi.twitch.tv")
            {
                Send("PONG :tmi.twitch.tv");
            }

            string channel = "";
            if (_channelName.IsMatch(stringMsg))
                channel = _channelName.Match(stringMsg).Groups["Channel"].Value;

            if (FilterJoinPart(stringMsg, channel)) return;

            if (_roomStateRX.IsMatch(stringMsg))
            {
                try
                {
                    FilterRoomState(stringMsg, channel);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
               
                foreach (KeyValuePair<string, RegisteredPlugin> kvp in RegisteredPlugins) kvp.Value.TryInvokeOnRoomStateChanged(this, RoomStates[channel]);
                return;
            }

            TwitchMessage message = new TwitchMessage();
            string[] splitMsg = stringMsg.Split(new[] {" :"}, 3, StringSplitOptions.RemoveEmptyEntries); 
            if (splitMsg.Length >= 3)
                message.Content = splitMsg[2];
            message.RawMessage = stringMsg;
            splitMsg = splitMsg[0].Split(';');

            if (RoomStates.ContainsKey(channel))
            {
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
                            message.Author = FindChatUser(splitTag[1], stringMsg, channel);
                            break;
                        case "emotes":
                            if (_emoteRX.IsMatch(stringMsg)) message.Emotes = ParseEmotes(splitTag[1]);
                            break;
                        case "id":
                            message.Id = splitTag[1];
                            break;
                    }
                }
                message.Room = RoomStates[channel];
            }

            foreach (KeyValuePair<string, RegisteredPlugin> kvp in RegisteredPlugins) kvp.Value.TryInvokeOnMessageReceived(this, message);
        }

        //These events don't work very well due to caching and they don't have much data to begin with. don't use them.
        private bool FilterJoinPart(string msg, string channel)
        {
            if (_joinRX.IsMatch(msg)) foreach (KeyValuePair<string, RegisteredPlugin> kvp in RegisteredPlugins) kvp.Value.TryInvokeOnChatJoined(this, msg);

            if (_partRX.IsMatch(msg))
            {
                string username = _partRX.Match(msg).Groups["User"].Value;
                ChatUserListing partedUser = RoomStates[channel].UserList.FirstOrDefault(x => x.User.DisplayName == username);
                if (partedUser.User != null)
                {
                    RoomStates[channel].RemoveUserFromList(username);
                    foreach (KeyValuePair<string, RegisteredPlugin> kvp in RegisteredPlugins) kvp.Value.TryInvokeOnChatParted(this, partedUser, msg); 
                }

                return true;
            }

            return false;
        }

        private void FilterRoomState(string msg, string channel)
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
                        RoomStates[channel].BroadcasterLang = tagParts[1];
                        break;
                    case "emote-only":
                        RoomStates[channel].EmoteOnly = int.Parse(tagParts[1]) > 0;
                        break;
                    case "followers-only":
                        RoomStates[channel].FollowersOnly = int.Parse(tagParts[1]);
                        break;
                    case "r9k":
                        RoomStates[channel].R9KMode = int.Parse(tagParts[1]) > 0;
                        break;
                    case "slow":
                        RoomStates[channel].SlowMode = int.Parse(tagParts[1]);
                        break;
                    case "subs-only":
                        RoomStates[channel].SubOnly = int.Parse(tagParts[1]) > 0;
                        break;
                    case "room-id":
                        RoomStates[channel].RoomID = tagParts[1];
                        break;
                }
            }
        }

        public ChatUser FindChatUser(string displayName, string rawMessage, string channel)
        {
            if (RoomStates[channel].UserList.Exists(x => x.User.DisplayName == displayName))
            {
                return RoomStates[channel].UserList.Find(x => x.User.DisplayName == displayName).User;
            }

            ChatUser author = CreateChatUser(rawMessage);
            if (rawMessage.Contains(":tmi.twitch.tv PRIVMSG")) RoomStates[channel].AddUserToList(author);
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