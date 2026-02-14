using System;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Steamworks;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using SimpleJSON;
using UnityEngine.SceneManagement;
namespace RavenM
{
    /// <summary>
    /// Disable mods that are NOT workshop mods.
    /// </summary>
    [HarmonyPatch(typeof(ModManager), nameof(ModManager.OnGameManagerStart))]
    public class NoCustommodsPatch
    {
        static bool Prefix(ModManager __instance)
        {
            string path = "NOT_REAL";
            if (Plugin.addToBuiltInMutators)
            {
                path = Plugin.customBuildInMutators;
                __instance.noContentMods = false;
                __instance.noWorkshopMods = true;
            }
            __instance.modStagingPathOverride = path;
            typeof(MapEditor.MapDescriptor).GetField("DATA_PATH", BindingFlags.Static | BindingFlags.Public).SetValue(null, path);
            return true;
        }
    }

    public class GuidComponent : MonoBehaviour
    {
        public int guid; //TODO: Replace with System.GUID?
    }

    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    [BepInDependency("RavenM.Updater", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {

        public bool FirstSteamworksInit = false;

        public bool HasGotGameBuildNumber = false;

        public static Plugin instance = null;

        public static BepInEx.Logging.ManualLogSource logger = null;
        public static ConfigFile config = null;

        public static bool changeGUID = false;

        public static bool addToBuiltInMutators = false;
        public static string customBuildInMutators;
        public static List<string> customMutatorsDirectories = new List<string>();

        public CSteamID startupLobbyId;

        public static int currentGameBuildNumber = 0;
        public static Dictionary<string, string> Arguments = new Dictionary<string, string>();
        
        public static string storageDirectoryPath = Application.persistentDataPath + "/RavenM/";
        
        public bool hasDetectedException = false;

        public static string BuildGUID
        {
            get
            {
                if (!changeGUID)
                {
                    return $"INDEV-EA{(GameManager.instance == null ? 0 : GameManager.instance.buildNumber)}-{MyPluginInfo.PLUGIN_VERSION.Replace(".", "-")}-{Assembly.GetExecutingAssembly().ManifestModule.ModuleVersionId.ToString().Split('-').Last()}";
                }
                else
                {
                    return $"TESTMODE-EA{(GameManager.instance == null ? 0 : GameManager.instance.buildNumber)}-{MyPluginInfo.PLUGIN_VERSION.Replace(".", "-")}-89a27d9e2fcb";
                }
            }
        }

        public static readonly int EXPECTED_BUILD_NUMBER = 32;

        private ConfigEntry<bool> configRavenMDevMod;
        private ConfigEntry<bool> configRavenMAddToBuiltInMutators;
        private ConfigEntry<string> configRavenMBuiltInMutatorsDirectory;

        private void InitLoadMessage()
        {
            DontDestroyOnLoad(new GameObject("InitMessageGUI", typeof(InitMessageGUI)));
        }

        private void Awake()
        {
            instance = this;
            logger = Logger;
            config = Config;

            // global error handle, if has error then save log to another path when quitting game so the log wnot lose
            Application.logMessageReceived += (string logString, string stackTrace, LogType type) =>
            {
                if ((type == LogType.Error || type == LogType.Exception) && hasDetectedException == false)
                    hasDetectedException = true;
            };

            Settings.Init();


            string[] args = Environment.GetCommandLineArgs();

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-noravenm")
                {
                    Logger.LogWarning($"Plugin {MyPluginInfo.PLUGIN_GUID} is canceled to load!");
                    InitLoadMessage();
                    InitMessageGUI.overwrittenStringToShow = "RavenM unloaded.";
                    Destroy(this);
                }
                if (args[i] == "+connect_lobby")
                {
                    try
                    {
                        startupLobbyId = new CSteamID(ulong.Parse(args[i + 1]));
                        logger.LogInfo("Startup lobby id: " + args[i + 1]);
                        Instantiate(new GameObject("RavenMPregamingObject", typeof(PregamingMenuUI)));
                    }
                    catch (Exception e)
                    {
                        logger.LogError(e);
                    }
                }
            }

            Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

            configRavenMDevMod = Config.Bind("General.Toggles",
                                                "Enable Dev Mode",
                                                false,
                                                "Change GUID to WARNING-TESTING-MODE-89a27d9e2fcb");
            configRavenMAddToBuiltInMutators = Config.Bind("General.Toggles",
                "Enable Custom Build In Mutators",
                false,
                "Add Directory in General.BuildInMutators");

            configRavenMBuiltInMutatorsDirectory = Config.Bind("General.BuildInMutators",
                                                                "Directory",
                                                                "",
                                                                "The mutators in the folder will be added automatically as Build In Mutators, this is for testing mutators without having to start the game with mods.");


            changeGUID = configRavenMDevMod.Value;
            addToBuiltInMutators = configRavenMAddToBuiltInMutators.Value;
            customBuildInMutators = configRavenMBuiltInMutatorsDirectory.Value;
            if (System.IO.Directory.Exists(customBuildInMutators))
            {
                Logger.LogInfo("Added Custom Build In Mutator Directory " + customBuildInMutators);
            }
            else
            {
                if (customBuildInMutators != "")
                {
                    Logger.LogError($"Directory {customBuildInMutators} could not be found.");
                }
                customBuildInMutators = "NOT_REAL";
            }
            var harmony = new Harmony("patch.ravenm");
            try
            {
                harmony.PatchAll(Assembly.GetAssembly(this.GetType()));
            }
            catch (Exception e)
            {
                Logger.LogError($"Failed to patch: {e}");
            }
            InitLoadMessage();

            // TODO: unfinished
            if (Environment.CommandLine.Contains("-nocontentmods") && !Environment.CommandLine.Contains(" -nointro") && false)
            {
                logger.LogInfo("Launch pre-gaming status");

                foreach (var gameObject in SceneManager.GetActiveScene().GetRootGameObjects())
                {
                    if (gameObject.GetComponent<Camera>() == null)
                        Destroy(gameObject);
                }
            }

            // get tips annoucement
            Task.Run(() =>
            {
                try
                {
                    var result = DateTime.TryParse(Settings.lastGetTipsAnnoucementDate.Value, out var lastTime);
                    if (Settings.showTipsAnnouncement.Value && result != false && DateTime.Now - lastTime < TimeSpan.FromDays(7))
                    {
                        Logger.LogInfo("Skip fetch tips annoucement");
                        return;
                    }
                    JSONNode GetJson(string url)
                    {
                        return JSON.Parse(new StreamReader(MakeRequest(url)).ReadToEnd());
                    }
                    JSONNode json = null;
                    try { json = GetJson("https://api.github.com/repos/RavenfieldCommunity/RavenM/releases/237069307"); }
                    catch (Exception e)
                    {
                        Logger.LogError(e);
                    }
                    if (json != null && json["tag_name"] == "tips") Settings.tipsAnnoucement.Value = json["body"];
                    else
                    {
                        try { json = GetJson("https://api.github.com/repos/RavenfieldCommunity/RavenM/releases/237069307"); }
                        catch (Exception e)
                        {
                            Logger.LogError(e);
                        }
                        var count = 0;
                        while (json != null && json[count] != null && count < 30)
                        {
                            if (json[count]["tag_name"] == "tips")
                            {
                                Settings.tipsAnnoucement.Value = json[count]["body"];
                                break;
                            }
                            count++;
                        }
                    }
                    Settings.lastGetTipsAnnoucementDate.Value = DateTime.Now.ToString();
                }
                catch (Exception e)
                {
                    Logger.LogError(e);
                }
            });
        }

        public void printConsole(string message)
        {
            Lua.ScriptConsole.instance.LogInfo(message);
        }
        void Update()
        {
            if (!SteamManager.Initialized)
                return;

            SteamAPI.RunCallbacks();
            if (!FirstSteamworksInit)
            {
                FirstSteamworksInit = true;

                var lobbyObject = new GameObject();
                lobbyObject.AddComponent<LobbySystem>();
                DontDestroyOnLoad(lobbyObject);

                var chatObject = new GameObject();
                chatObject.AddComponent<ChatManager>();
                DontDestroyOnLoad(chatObject);

                var netObject = new GameObject();
                netObject.AddComponent<IngameNetManager>();
                DontDestroyOnLoad(netObject);

                var discordObject = new GameObject();
                discordObject.AddComponent<DiscordIntegration>();
                DontDestroyOnLoad(discordObject);
                // Repush settings 
                Settings.OnSettingUpdate();
                if (startupLobbyId != CSteamID.Nil)
                {
                    SceneManager.LoadScene(1);
                }
            }
            this.enabled = false;
        }

        void OnDestory()
        {
            instance = null;
        }
        
        void OnApplicationQuit()
        {
            // TO DO: this is not completed yet, should we help player copy log without asking??
            /*
            if (hasDetectedException && Application.consoleLogPath != "")
            {
                var writer = File.CreateText(storageDirectoryPath + "ERROR_LOG_FILES_ARE_IN_THE_PARENT_FOLDER");
                writer.Write("They are named e.g. `Player_2025-03-02-15-47.log.txt`\nPAY ATTENTION TO THE DATE IN THE FILE NAMES!!!\nBETTER NOT TO USE `Playe.log` or `Playe-prev.log`!!!");
                writer.Close();
            */
        }
        
        private Stream MakeRequest(string url)
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "GET";
            req.Accept = "application/vnd.github+json";
            req.UserAgent = "Def-Not-RavenM";
            req.Timeout = 5000;

            return req.GetResponse().GetResponseStream();
        }
    }

    public class InitMessageGUI : MonoBehaviour
    {
        public float maxlifetime;
        public static string overwrittenStringToShow = null;
        public void Awake()
        {
            maxlifetime = Time.time + 30;
        }

        public void OnGUI()
        {
            if (maxlifetime < Time.time) Destroy(this);
            var rect = new Rect(10, Screen.height - 20, Screen.width, 40);
            if (overwrittenStringToShow == null)
            {
                if (GameManager.instance == null || GameManager.instance.buildNumber == Plugin.EXPECTED_BUILD_NUMBER)
                    GUI.Label(rect, "RavenM loaded, press `M` to show UI on Instant Actions Menu.");
                else
                    GUI.Label(rect, $"RavenM suggests using EA{Plugin.EXPECTED_BUILD_NUMBER}. press `M` to show UI on Instant Actions Menu.");
            }
            else
                GUI.Label(rect, $"{overwrittenStringToShow}");
        }
    }
                    
}
