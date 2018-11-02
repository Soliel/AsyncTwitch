# Beat Saber Asynchronous Twitch Library
#### This mod does not do anything on  it's own. It is purely for use by other mod authors.
---
# For Mod Users:
Twitch Lib is meant to be very easy to use and setup. If you've used any other twitch mod it's very similar. 

In order to use mods that depend on this library you must fill out the Config file `AsyncTwitchConfig.json` located in `C:\Steam\steamapps\common\Beat Saber\Config` or in your **Config** folder  if you've installed Beat Saber to another location.

If the File is not there run the game with the mod installed and it will be generated. 

Once you find the file you must fill it out with your Twitch Username and Twitch Channel Name (**Which are most likely the same**).

```json
{
    "Username": "<Your twitch login name>",
    "ChannelName": "<Your twitch channel name>", 
    "OauthKey": "<put your oauth key here>"
}
```
Once this is filled out if you're encountering any issues say hi in #Support in the [BSMG Discord](https://discord.gg/beatsabermods).
___
# For Modders:
I'm slowly working on creating full documentation for the plugin but the basic info goes like this. 

The Mod creates an instance:

`TwitchConnection.Instance`  

Which you should use to start the irc connection:

`TwitchConnection.Instance.StartConnection()`

After you've started the IRC connection (A new one will not be created if another mod has already started it.) You must add your event handlers to one of these for events:
```cs
        public static event Action<TwitchConnection, TwitchMessage> OnMessageReceived;
        public static event Action<TwitchConnection> OnConnected;
        public static event Action<TwitchConnection> OnChatJoined;
        public static event Action<TwitchConnection, ChatUser> OnChatParted;
        public static event Action<TwitchConnection, RoomState> OnRoomStateChanged;
```
For information on the types returned check out the [github](https://github.com/Soliel/AsyncTwitch).
```cs
public OnLoad() {
    TwitchConnection.OnMessageReceived += MyMessageHandler;
}

public void MyMessageHandler(TwitchConnection connection, TwitchMessage msg) {
    //Handle message here.
}
```
The TwitchConnection instance contains values to keep track of the current roomstate including chat limits and a user list. Again check out the [github](https://github.com/Soliel/AsyncTwitch) for more information. 

## Happy Modding!
