using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using JetBrains.Annotations;
using NLog;
using UnityEngine;
using Logger = NLog.Logger;

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

        public static bool IsConnected;
        private DateTime _lastMessageTime = DateTime.Now;
        private Config _loginInfo;
        private Logger _logger;
        private int _lastReconnectCount = 0;

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
            _logger = LogManager.GetCurrentClassLogger();
            Instance = this;
            DontDestroyOnLoad(this);
        }

        [UsedImplicitly]
        public void StartConnection()
        {
            lock (Instance)
            {
                string pluginName = Assembly.GetCallingAssembly().FullName;

                RegisterPlugin(pluginName);

                if (IsConnected) return;

                _loginInfo = Config.LoadFromJson();
                //if (_loginInfo.Username == "") return;
                Connect("irc.twitch.tv", 6667);
                IsConnected = true;
            }
        }

        #region Registration

        private void RegisterPlugin(string pluginIdentifier) {
            if (RegisteredPlugins.ContainsKey(pluginIdentifier)) return;
            RegisteredPlugins[pluginIdentifier] = new RegisteredPlugin();
        }

        public void RegisterOnConnected(Action<TwitchConnection> callback)
        {
            string pluginIdentifier = Assembly.GetCallingAssembly().FullName;
            if (!RegisteredPlugins.ContainsKey(pluginIdentifier)) RegisterPlugin(pluginIdentifier);
            RegisteredPlugins[pluginIdentifier]._onConnected += callback;
        }

        public void RegisterOnRawMessageReceived(Action<string> callback)
        {
            string pluginIdentifier = Assembly.GetCallingAssembly().FullName;
            if (!RegisteredPlugins.ContainsKey(pluginIdentifier)) RegisterPlugin(pluginIdentifier);
            RegisteredPlugins[pluginIdentifier]._onRawMessageReceived += callback;
        }

        public void RegisterOnRoomStateChanged(Action<TwitchConnection, RoomState> callback)
        {
            string pluginIdentifier = Assembly.GetCallingAssembly().FullName;
            if (!RegisteredPlugins.ContainsKey(pluginIdentifier)) RegisterPlugin(pluginIdentifier);
            RegisteredPlugins[pluginIdentifier]._onRoomStateChanged += callback;

            foreach (KeyValuePair<string,RoomState> kvp in RoomStates)
            {
                if (kvp.Value.RoomID != String.Empty)
                {
                    Task.Run(() => RegisteredPlugins[pluginIdentifier].OnRoomStateChanged(this, kvp.Value));
                }
            }
        }

        public void RegisterOnMessageReceived(Action<TwitchConnection, TwitchMessage> callback)
        {
            string pluginIdentifier = Assembly.GetCallingAssembly().FullName;
            if (!RegisteredPlugins.ContainsKey(pluginIdentifier)) RegisterPlugin(pluginIdentifier);
            RegisteredPlugins[pluginIdentifier]._onMessageReceived += callback;
        }

        public void RegisterOnChatJoined(Action<TwitchConnection> callback)
        {
            string pluginIdentifier = Assembly.GetCallingAssembly().FullName;
            if (!RegisteredPlugins.ContainsKey(pluginIdentifier)) RegisterPlugin(pluginIdentifier);
            RegisteredPlugins[pluginIdentifier]._onChatJoined += callback;
        }

        public void RegisterOnChatParted(Action<TwitchConnection, ChatUser> callback)
        {
            string pluginIdentifier = Assembly.GetCallingAssembly().FullName;
            if (!RegisteredPlugins.ContainsKey(pluginIdentifier)) RegisterPlugin(pluginIdentifier);
            RegisteredPlugins[pluginIdentifier]._onChatParted += callback;
        }

        public void RegisterOnChannelParted(Action<TwitchConnection, string> callback)
        {
            string pluginIdentifier = Assembly.GetCallingAssembly().FullName;
            if (!RegisteredPlugins.ContainsKey(pluginIdentifier)) RegisterPlugin(pluginIdentifier);
            RegisteredPlugins[pluginIdentifier]._onChannelParted += callback;
        }

        public void RegisterOnChannelJoined(Action<TwitchConnection, string> callback)
        {
            string pluginIdentifier = Assembly.GetCallingAssembly().FullName;
            if (!RegisteredPlugins.ContainsKey(pluginIdentifier)) RegisterPlugin(pluginIdentifier);
            RegisteredPlugins[pluginIdentifier]._onChannelJoined += callback;

            foreach (KeyValuePair<string, RoomState> kvp in RoomStates)
            {
                Task.Run(() => RegisteredPlugins[pluginIdentifier].OnChannelJoined(this, kvp.Key)); 
            }
        }

        #endregion

        public override void OnConnect()
        {
            SendRawMessage("CAP REQ :twitch.tv/membership twitch.tv/commands twitch.tv/tags");
            if (_loginInfo.Username == String.Empty || _loginInfo.OauthKey == String.Empty)
            {
                SendRawMessage("NICK justinfan" + new System.Random(DateTime.Now.Millisecond).Next(1000, 100000).ToString());
            }
            else
            {
                SendRawMessage("PASS " + _loginInfo.OauthKey);
                SendRawMessage("NICK " + _loginInfo.Username);
            }

            if (_reconnectCount > _lastReconnectCount)
            {
                _lastReconnectCount = _reconnectCount;
                foreach (KeyValuePair<string, RoomState> room in RoomStates)
                {
                    JoinRoom(room.Key);
                }
            }
            else
            {
                if (_loginInfo.ChannelName != String.Empty) JoinRoom(_loginInfo.ChannelName);
            }

            Task.Run(() => OnConnectedTask(this));
        }

        //This is a simple Queueing system to avoid the ratelimit.
        //We can expand upon this later by finding how many messages we've sent in the last 30 seconds.
        [UsedImplicitly]
        public void SendChatMessage(string msg)
        {
            if (DateTime.Now - _lastMessageTime >= _rateLimit)
            {
                Send("PRIVMSG #" + _loginInfo.ChannelName + " :" + msg);
                return;
            }
            _messageQueue.Enqueue("PRIVMSG #" + _loginInfo.ChannelName + " :" + msg);
            DateTime timeUntilRateLimit = _lastMessageTime.Add(_rateLimit);
            Task.Delay((timeUntilRateLimit - DateTime.Now) < TimeSpan.Zero ? TimeSpan.Zero : timeUntilRateLimit - DateTime.Now).ContinueWith(SendMessageFromQueue);
            _lastMessageTime = timeUntilRateLimit;
        }

        public void SendRawMessage(string msg)
        {
            if (DateTime.Now - _lastMessageTime >= _rateLimit)
            {
                Send(msg);
                return;
            }
            _messageQueue.Enqueue(msg);
            DateTime timeUntilRateLimit = _lastMessageTime.Add(_rateLimit);
            Task.Delay((timeUntilRateLimit - DateTime.Now) < TimeSpan.Zero ? TimeSpan.Zero : timeUntilRateLimit - DateTime.Now).ContinueWith(SendMessageFromQueue);
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
            if (channel != String.Empty) channel = channel.ToLower();
            if (!RoomStates.ContainsKey(channel))
            {
                RoomState newRoomState = new RoomState();
                RoomStates[channel] = newRoomState;
            }
            SendRawMessage("JOIN #" + channel);

            Task.Run(() => OnChannelJoinedTask(channel));
        }

        public void PartRoom(string channel)
        {
            if (!RoomStates.ContainsKey(channel)) return;

            RoomStates.Remove(channel);
            SendRawMessage("PART #" + channel);

            Task.Run(() => OnChannelPartedTask(channel));
        }

        #region Event Tasks

        private void OnConnectedTask(TwitchConnection obj)
        {
            foreach (KeyValuePair<string, RegisteredPlugin> kvp in RegisteredPlugins)
            {
                try
                {
                    kvp.Value.OnConnected(obj);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"An exception occured while trying to call OnConnected for plugin {kvp.Key} (Exception: {e.ToString()})");
                }
            }
        }

        private void OnChannelJoinedTask(string channel)
        {
            foreach (KeyValuePair<string, RegisteredPlugin> kvp in RegisteredPlugins)
            {
                try
                {
                    kvp.Value.OnChannelJoined(this, channel);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"An exception occured while trying to call OnChannelJoined for plugin {kvp.Key} (Exception: {e.ToString()})");
                }
            }
        }

        private void OnChannelPartedTask(string channel)
        {
            foreach (KeyValuePair<string, RegisteredPlugin> kvp in RegisteredPlugins)
            {
                try
                {
                    kvp.Value.OnChannelParted(this, channel);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"An exception occured while trying to call OnChannelParted for plugin {kvp.Key} (Exception: {e.ToString()})");
                }
            }
        }

        private void OnRawMessageReceivedTask(string rawMessage)
        {
            foreach (KeyValuePair<string, RegisteredPlugin> kvp in RegisteredPlugins)
            {
                try
                {
                    kvp.Value.OnRawMessageReceived(rawMessage);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"An exception occured while trying to call OnRawMessageReceived for plugin {kvp.Key} (Exception: {e.ToString()})");
                }
            }
        }

        private void OnRoomStateChangedTask(TwitchConnection obj, RoomState roomstate)
        {
            foreach (KeyValuePair<string, RegisteredPlugin> kvp in RegisteredPlugins)
            {
                try
                {
                    kvp.Value.OnRoomStateChanged(obj, roomstate);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"An exception occured while trying to call OnRoomStateChanged for plugin {kvp.Key} (Exception: {e.ToString()})");
                }
            }
        }

        private void OnMessageReceivedTask(TwitchConnection obj, TwitchMessage msg)
        {
            foreach (KeyValuePair<string, RegisteredPlugin> kvp in RegisteredPlugins)
            {
                try
                {
                    kvp.Value.OnMessageReceived(obj, msg);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"An exception occured while trying to call OnMessageReceived for plugin {kvp.Key} (Exception: {e.ToString()})");
                }
            }
        }

        private void OnChatJoinedTask(TwitchConnection obj, string msg)
        {
            foreach (KeyValuePair<string, RegisteredPlugin> kvp in RegisteredPlugins)
            {
                try
                {
                    kvp.Value.OnChatJoined(obj, msg);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"An exception occured while trying to call OnChatJoined for plugin {kvp.Key} (Exception: {e.ToString()})");
                }
            }
        }

        private void OnChatPartedTask(TwitchConnection obj, ChatUserListing listing, string msg)
        {
            foreach (KeyValuePair<string, RegisteredPlugin> kvp in RegisteredPlugins)
            {
                try
                {
                    kvp.Value.OnChatParted(obj, listing, msg);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"An exception occured while trying to call OnChatParted for plugin {kvp.Key} (Exception: {e.ToString()})");
                }
            }
        }


        #endregion

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
        private readonly Regex _channelName = new Regex(@"tmi\.twitch\.tv\s(?<Command>\w+)\s#(?<Channel>[A-Za-z0-9_]+)", RegexOptions.Compiled);
        private readonly Regex _userName = new Regex(@"\s:(?<User>[A-Za-z0-9_]+)![A-Za-z0-9_]+@[A-Za-z0-9_]+\.", RegexOptions.Compiled);

        #endregion

        #region MessageParsing

        public override void ProcessMessage(byte[] msg)
        {
            _logger.Trace("Entered into subclass.");
            string stringMsg = "";
            try
            {
                stringMsg = Utf8NoBom.GetString(msg);
                Task.Run(() => OnRawMessageReceivedTask(stringMsg));
            }
            catch (Exception e)
            {
                _logger.Error(e);
            }

            _logger.Trace(stringMsg);

            if (stringMsg == "PING :tmi.twitch.tv")
            {
                _logger.Trace("Received ping, sending pong.");
                Send("PONG :tmi.twitch.tv");
            }
            string channel = "";

            try
            {
                if (_channelName.IsMatch(stringMsg))
                    channel = _channelName.Match(stringMsg).Groups["Channel"].Value;
            }
            catch (Exception e)
            {
                _logger.Error(e);
            }

            //if (FilterJoinPart(stringMsg, channel)) return;
            if (_roomStateRX.IsMatch(stringMsg))
            {
                _logger.Trace("Roomstate Found.");
                try
                {
                    FilterRoomState(stringMsg, channel);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }

                Task.Run(() => OnRoomStateChangedTask(this, RoomStates[channel]));
                return;
            }

            _logger.Trace("Parsing message.");
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

            Task.Run(() => OnMessageReceivedTask(this, message));
        }

        //These events don't work very well due to caching and they don't have much data to begin with. don't use them.
        private bool FilterJoinPart(string msg, string channel)
        {
            if (_joinRX.IsMatch(msg)) Task.Run(() => OnChatJoinedTask(this, msg));

            if (_partRX.IsMatch(msg))
            {
                ChatUserListing partedUser = null;
                string username = "";
                try
                {
                    username = _partRX.Match(msg).Groups["User"].Value;
                    partedUser = RoomStates[channel].UserList.FirstOrDefault(x => x.User.DisplayName == username);
                }
                catch (Exception e)
                {
                    _logger.Error(e);
                }

                if (partedUser.User != null)
                {
                    try
                    {
                        RoomStates[channel].RemoveUserFromList(username);
                        Task.Run(() => OnChatPartedTask(this, partedUser, msg));
                    }
                    catch (Exception e)
                    {
                        _logger.Error(e);
                    }
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
        public ChatUser CreateChatUser(string chatMsg)
        {
            //_logger.Log(LogLevel.Debug, $"ChatMessage: {chatMsg}");

            ChatUser newUser = new ChatUser();
            MatchCollection badgesCollection = _badgeRX.Matches(chatMsg);
            newUser.Badges = new Badge[badgesCollection.Count];
            int i = 0;

            foreach (Match match in badgesCollection)
            {
                string[] badgeInfo = match.Value.Split('/');
                Badge badge = new Badge(badgeInfo[0], int.Parse(badgeInfo[1]));
                newUser.Badges[i] = badge;

                if (badge.BadgeName == "broadcaster") newUser.IsBroadcaster = true;
                if (badge.BadgeName == "vip") newUser.IsVIP = true;
               i++;
            }

            string[] tagFilteredString = chatMsg.Split(new char[] { ' ' }, 2)[0].Substring(1).Split(';');
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
                    case "subscriber":
                        newUser.IsSubscriber = int.Parse(tagParts[1]) > 0;
                        break;
                    case "user-id":
                        newUser.UserID = tagParts[1];
                        break;
                }
            }

            if (newUser.DisplayName == "" && _channelName.IsMatch(chatMsg))
            {
                Match userMatch = _userName.Match(chatMsg);
                newUser.DisplayName = userMatch.Groups["User"].Value;
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