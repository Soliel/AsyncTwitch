using System;

namespace AsyncTwitch
{
    public struct TwitchMessage
    {
        //The content of a twitch irc message.
        public string   Content { get; }
        //The User Object of the Author of the message.
        public ChatUser Author { get; }
        //Did this message include bits?
        public bool GaveBits { get; }
        //If so how many?
        public int BitAmount { get; }
        //The emote objects included in the message. 
        public TwitchEmote[] Emotes { get; }
        //The Id of the message
        public string Id { get; }

        public TwitchMessage(string content, ChatUser author, bool gaveBits, int bitAmount, TwitchEmote[] emotes, string id)
        {
            Content = content;
            Author = author;
            GaveBits = gaveBits;
            BitAmount = bitAmount;
            Emotes = emotes;
            Id = id;
        }
    }
}
