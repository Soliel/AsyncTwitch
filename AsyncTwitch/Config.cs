using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsyncTwitch
{
    public struct Config
    {
        public string Username { get; set; }
        public string ChannelName { get; set; }
        public string OauthKey { get; set; }

        public Config(string username, string channelName, string oauthKey)
        {
            Username = username.ToLower();
            ChannelName = channelName.ToLower();
            OauthKey = oauthKey;
        }

        public static void CreateDefaultConfig()
        {
            Config defaultConfig = new Config("Default Username", "Default Channel Name", "Default Oauth Key");
            defaultConfig.SaveJSON();
        }

        public void SaveJSON()
        {
            using(FileStream fs = new FileStream("Config/AsyncTwitchConfig.json", FileMode.Create, FileAccess.Write))
            {
                byte[] Buffer = Encoding.ASCII.GetBytes(JsonUtility.ToJson(this));
                fs.Write(Buffer, 0, Buffer.Length);
            }
        }

        public static Config LoadFromJSON()
        {
            if (File.Exists("Config/AsyncTwitchConfig.json"))
            {
                using (FileStream fs = new FileStream("Config/AsyncTwitchConfig.json", FileMode.Open, FileAccess.Read))
                {
                    byte[] loadBytes = new byte[fs.Length];
                    fs.Read(loadBytes, 0, (int)fs.Length);
                    return JsonUtility.FromJson(Encoding.ASCII.GetString(loadBytes), typeof(Config));
                }
            }
            else
            {
                CreateDefaultConfig();
                return new Config("","","");
            }
        }
    }
}
