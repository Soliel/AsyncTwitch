using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;

namespace AsyncTwitch
{
    public class TwitchConnection :  IrcConnection
    {
        public static event Action<TwitchConnection, TwitchMessage> OnMessageReceived;
        public static event Action<TwitchConnection> OnConnected;
        public static event Action<TwitchConnection, ChatUser> OnChatJoined;
        public static event Action<TwitchConnection, ChatUser> OnChatParted;

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
        private Regex _messageRX = new Regex(@"(?<=[A-Za-z0-9]+![A-Za-z0-9]+\@[A-Za-z0-9]+\.tmi\.twitch\.tv\sPRIVMSG\s#\w+\s:).+", RegexOptions.Compiled);
        private Regex _emoteRX = new Regex(@"(?<=;emotes=|\/)(\d+):(\d+-\d+[,/;])+", RegexOptions.Compiled);
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
            StartConnection();
        }

        public void StartConnection()
        {
            if (_isConnected) return;

            _loginInfo = Config.LoadFromJSON();
            if (_loginInfo.Username == "") return;
            Connect("irc.twitch.tv", 6667);
            _isConnected = true;
        }

        public override void OnConnect()
        {
            SendRawMessage("CAP REQ :twitch.tv/membership twitch.tv/commands twitch.tv/tags");
            SendRawMessage("PASS " + _loginInfo.OauthKey);
            SendRawMessage("NICK " + _loginInfo.Username);
            SendRawMessage("JOIN #" + _loginInfo.ChannelName);
        }

        public override void ProcessMessage(byte[] msg)
        {
            string stringMsg = _utf8noBOM.GetString(msg);
            Console.WriteLine(stringMsg);
            if (FilterJoinPart(stringMsg)) return;
            if (_roomStateRX.IsMatch(stringMsg))
                FilterRoomState(stringMsg);
        }

        private bool FilterJoinPart(string msg)
        {
            if(_joinRX.IsMatch(msg))
            {
                _joinRX.Match(msg).Groups["User"];
                return true;
            }
            else if(_partRX.IsMatch(msg))
            {
                _partRX.Match(msg).Groups["User"];
                return true;
            }
            return false;
        }

        private bool FilterRoomState(string msg)
        {
            string Tags = _roomStateRX.Match(msg).Groups["Tags"];
            string[] TagArray = Tags.Split(";");
            foreach(var Tag in TagArray)
            {
                var TagParts = Tag.Split("=");
                switch(TagParts[0])
                {
                    case "broadcaster-lang":
                        
                }
            }
        }

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
