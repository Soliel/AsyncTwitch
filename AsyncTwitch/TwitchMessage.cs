using System;

namespace AsyncTwitch
{
    public class TwitchMessage
    {
        //The content of a twitch irc message.
        public string Content;
        //The User Object of the Author of the message.
        public ChatUser Author;
        //Did this message include bits?
        public bool GaveBits;
        //If so how many?
        public int BitAmount;
        //The emote objects included in the message. 
        public TwitchEmote[] Emotes;
        //The Id of the message
        public string Id;
        //The Raw message incase I miss something.
        public string RawMessage;

        public TwitchMessage()
        {
            Content = "";
            Author = new ChatUser();
            GaveBits = false;
            BitAmount = 0;
            Emotes = new TwitchEmote[0];
            Id = "";
            RawMessage = "";
        }

        public override string ToString()
        {
            string returnString = "Message: \n\tContent: " + Content +
                                  "\n\tGave Bits: " + GaveBits + " How Many: " + BitAmount +
                                  "\n\tMessage ID: " + Id;
            returnString += "\n\nAuthor: " + Author.ToString();

            foreach (TwitchEmote twitchEmote in Emotes)
            {
                returnString += "\n" + twitchEmote.ToString();
            }

            return returnString;
        }
    }
}
