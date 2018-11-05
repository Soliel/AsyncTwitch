using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace AsyncTwitch
{
    [Serializable]
    public struct Config
    {
        public string Username;
        public string ChannelName;
        public string OauthKey;

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
            using(FileStream fs = new FileStream("UserData/AsyncTwitchConfig.json", FileMode.Create, FileAccess.Write))
            {
                byte[] Buffer = Encoding.ASCII.GetBytes(JsonUtility.ToJson(this, true));
                fs.Write(Buffer, 0, Buffer.Length);
            }
        }

        public static Config LoadFromJSON()
        {
            if (File.Exists("UserData/AsyncTwitchConfig.json"))
            {
                using (FileStream fs = new FileStream("UserData/AsyncTwitchConfig.json", FileMode.Open, FileAccess.Read))
                {
                    byte[] loadBytes = new byte[fs.Length];
                    fs.Read(loadBytes, 0, (int)fs.Length);
                    Config tempConfig = JsonUtility.FromJson<Config>(Encoding.ASCII.GetString(loadBytes));
                    if (!tempConfig.OauthKey.StartsWith("oauth:"))
                    {
                        if (tempConfig.OauthKey.Contains(':'))
                        {
                            string[] oauthSplit = tempConfig.OauthKey.Split(':');
                            tempConfig.OauthKey = "oauth:" + oauthSplit[1];
                        }
                        else
                        {
                            tempConfig.OauthKey = "oauth:" + tempConfig.OauthKey;
                        }
                    }

                    return tempConfig;
                }
            }
            else
            {
                CreateDefaultConfig();
                return new Config("","","");
            }
        }

        public override string ToString()
        {
            return "Username: " + Username + "\nChannel: " + ChannelName + "\nOauth Key: " + OauthKey;
        }
    }
}
