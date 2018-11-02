using IllusionPlugin;

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
        }

        public void OnFixedUpdate()
        {
        }

        public void OnLevelWasInitialized(int level)
        {
            if (level != 0) return;
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
