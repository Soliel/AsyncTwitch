using System;
using System.Collections.Generic;
using System.ComponentModel;
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

        // OnConnected
        public void OnConnected(TwitchConnection obj)
        {
            _onConnected?.Invoke(obj);
        }
        public Action<TwitchConnection> GetOnConnected()
        {
            return _onConnected;
        }

        // OnRawMessageReceived
        public void OnRawMessageReceived(string stringMsg)
        {
            _onRawMessageReceived?.Invoke(stringMsg);
        }
        public Action<string> GetOnRawMessageReceived()
        {
            return _onRawMessageReceived;
        }

        // OnRoomStateChanged
        public void OnRoomStateChanged(TwitchConnection obj, RoomState roomstate)
        {
            _onRoomStateChanged?.Invoke(obj, roomstate);
        }
        public Action<TwitchConnection, RoomState> GetOnRoomStateChanged()
        {
            return _onRoomStateChanged;
        }

        // OnMessageReceived
        public void OnMessageReceived(TwitchConnection obj, TwitchMessage msg)
        {
            _onMessageReceived?.Invoke(obj, msg);
        }
        public Action<TwitchConnection, TwitchMessage> GetOnMessageReceived()
        {
            return _onMessageReceived;
        }

        // OnChatJoined
        public void OnChatJoined(TwitchConnection obj)
        {
            _onChatJoined?.Invoke(obj);
        }
        public Action<TwitchConnection> GetOnChatJoined()
        {
            return _onChatJoined;
        }

        // OnChatParted
        public void OnChatParted(TwitchConnection obj, ChatUserListing user)
        {
            _onChatParted?.Invoke(obj, user.User);
        }
        public Action<TwitchConnection, ChatUser> GetOnChatParted()
        {
            return _onChatParted;
        }

        // OnChannelJoined
        public void OnChannelJoined(TwitchConnection obj, string channel)
        {
            _onChannelJoined?.Invoke(obj, channel);
        }
        public Action<TwitchConnection, string> GetOnChannelJoined()
        {
            return _onChannelJoined;
        }

        // OnChannelParted
        public void OnChannelParted(TwitchConnection obj, string channel)
        {
            _onChannelParted?.Invoke(obj, channel);
        }
        public Action<TwitchConnection, string> GetOnChannelParted()
        {
            return _onChannelParted;
        }
    }
}
