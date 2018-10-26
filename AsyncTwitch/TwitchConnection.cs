using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace AsyncTwitch
{
    public class TwitchConnection :  IrcConnection
    {
        public static event Action<TwitchConnection, TwitchMessage> OnMessageRecieved;
        public static event Action<TwitchConnection> OnConnected;
        public static event Action<TwitchConnection, ChatUser> OnChatJoined;
        public static event Action<TwitchConnection, ChatUser> OnChatParted;

        public static TwitchConnection Instance;

        private readonly TimeSpan RateLimit = new TimeSpan(0, 0, 0, 1 ,500); //How long since the last message before we send another.
        private Config _loginInfo;
        private bool _isConnected;
        private Queue<string> _messageQueue = new Queue<string>();
        private DateTime _lastMessageTime = DateTime.MinValue;

        #region REGEX Lines
        private Regex JoinRX = new Regex(@"^:[A-Za-z0-9]+![A-Za-z0-9]+@[A-Za-z0-9]+\.tmi\.twitch\.tv\sJOIN\s", RegexOptions.Compiled);
        private Regex PartRX = new Regex(@"^:[A-Za-z0-9]+![A-Za-z0-9]+@[A-Za-z0-9]+\.tmi\.twitch\.tv\sPART\s", RegexOptions.Compiled);
        

        #endregion

        public static void OnLoad()
        {
            if (Instance != null) return;
            new GameObject("Twitch connection").AddComponent<TwitchConnection>();
        }

        private void Awake()
        {
            Instance = this;


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
               
        }

        public override void ProcessMessage(byte[] msg)
        {

        }

        private bool FilterJoinPart(string msg)
        {


            return false;
        } 

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

        private void SendMessageFromQueue(Task task)
        {
            Send(_messageQueue.Dequeue());
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
    }
}
