using IllusionPlugin;
using JetBrains.Annotations;
using NLog;
using NLog.Config;
using NLog.Targets;
using UnityEngine.SceneManagement;

namespace AsyncTwitch
{
    [UsedImplicitly]
    public class Plugin : IPlugin
    {
        public string Name => "Asynchronous Twitch Library";

        public string Version => "1.0.0";

        public void OnApplicationQuit()
        {  
        }

        public void OnApplicationStart()
        {
            LoggingConfiguration nLogConfig = new LoggingConfiguration();
            FileTarget logFile = new FileTarget("logfile") {FileName = "AsyncTwitchLog.txt"};
            ConsoleTarget logConsole = new ConsoleTarget("logconsole");

            nLogConfig.AddRule(LogLevel.Trace, LogLevel.Off, logFile);
            nLogConfig.AddRule(LogLevel.Trace, LogLevel.Off, logConsole);

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
