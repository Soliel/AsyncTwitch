using IllusionPlugin;
using UnityEngine.SceneManagement;

namespace AsyncTwitch
{
    class Plugin : IPlugin
    {
        public string Name => "Asynchronous Twitch Library";

        public string Version => "1.0.0";

        public void OnApplicationQuit()
        {  
        }

        public void OnApplicationStart()
        {
            TwitchConnection.OnLoad();
        }

        public void OnFixedUpdate()
        {
        }

        public void OnLevelWasInitialized(int level)
        {
            if(SceneManager.GetActiveScene().name != "Menu") return;
            TwitchConnection.OnLoad();
        }

        public void OnLevelWasLoaded(int level)
        {
        }

        public void OnUpdate()
        {
        }
    }
}
