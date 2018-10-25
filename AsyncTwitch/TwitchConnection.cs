using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsyncTwitch
{
    public class TwitchConnection : IRCConnection, MonoBehaviour
    {
        public static event Action<TwitchConnection, TwitchMessage> OnMessageRecieved;
        public static event Action<TwitchConnection> OnConnected;
        public static event Action<TwitchConnection, ChatUser> OnChatJoined;
        public static event Action<TwitchConnection, ChatUser> OnChatParted;

        public static TwitchConnection Instance;

        private Config _loginInfo = new Config();
        private bool isConnected = false;


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
            if (isConnected) return;

            _loginInfo = Config.LoadFromJSON();
            if (_loginInfo.Username == "") return;

            Connect("irc.twitch.tv", 6667);
            isConnected = true;
        }

        public override void OnConnect()
        {
               
        }

        public override void ProcessMessage(byte[] msg)
        {

        }

        public void SendChatMessage(string msg)
        {

        }

        public void 

        public void SendRawMessage(string msg)
        {

        }
    }
}
