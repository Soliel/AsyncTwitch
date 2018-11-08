using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsyncTwitch
{
    public class RegisteredPlugin
    {
        public event Action<TwitchConnection, TwitchMessage> _onMessageReceived = null;
        public event Action<TwitchConnection> _onConnected = null;
        public event Action<TwitchConnection> _onChatJoined = null;
        public event Action<TwitchConnection, ChatUser> _onChatParted = null;
        public event Action<TwitchConnection, RoomState> _onRoomStateChanged = null;
        public event Action<string> _onRawMessageReceived = null;
        public event Action<TwitchConnection, string> _onChannelJoined = null;
        public event Action<TwitchConnection, string> _onChannelParted = null;

        public void OnConnected(TwitchConnection obj)
        {
            _onConnected?.Invoke(obj);
        }

        public void OnRawMessageReceived(string stringMsg)
        {
            _onRawMessageReceived?.Invoke(stringMsg);
        }

        public void OnRoomStateChanged(TwitchConnection obj, RoomState roomstate)
        {
            _onRoomStateChanged?.Invoke(obj, roomstate);
        }

        public void OnMessageReceived(TwitchConnection obj, TwitchMessage msg)
        {
            _onMessageReceived?.Invoke(obj, msg);
        }

        public void OnChatJoined(TwitchConnection obj, string msg)
        {
            _onChatJoined?.Invoke(obj);
        }

        public void OnChatParted(TwitchConnection obj, ChatUserListing user, string msg)
        {
            _onChatParted?.Invoke(obj, user.User);
        }

        public void OnChannelJoined(TwitchConnection obj, string channel)
        {
            _onChannelJoined?.Invoke(obj, channel);
        }

        public void OnChannelParted(TwitchConnection obj, string channel)
        {
            _onChannelParted?.Invoke(obj, channel);
        }
    }
}
