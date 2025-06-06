using System;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Steamworks;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
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

        public static bool JoinedLobbyFromArgument = false;

        public static int currentGameBuildNumber = 0;
        public static Dictionary<string, string> Arguments = new Dictionary<string, string>();

        public static string BuildGUID
        {
            get
            {
                if (!changeGUID)
                {
                    return $"INDEV-EA{(GameManager.instance == null ? 0 : GameManager.instance.buildNumber)}-{MyPluginInfo.PLUGIN_VERSION.Replace(".","-")}-{Assembly.GetExecutingAssembly().ManifestModule.ModuleVersionId.ToString().Split('-').Last()}";
                }
                else
                {
                    return $"TESTMODE-EA{(GameManager.instance == null ? 0 : GameManager.instance.buildNumber)}-{MyPluginInfo.PLUGIN_VERSION.Replace(".","-")}-89a27d9e2fcb";
                }
            }
        }

        public static readonly int EXPECTED_BUILD_NUMBER = 32;

        private ConfigEntry<bool> configRavenMDevMod;
        private bool showBuildGUID;

        public static float chatWidth = 500f;
        public static float chatHeight = 200f;
        public static float chatYOffset = 370f;
        public static float chatXOffset = 10f;
        public static int chatFontSize = 0;
        public static bool allowClientDifference = false;
        public static bool changeChatFontSize = false;  //If need to change the font size
        
        private ConfigEntry<bool> configRavenMAddToBuiltInMutators;
        private ConfigEntry<string> configRavenMBuiltInMutatorsDirectory;
        private void Awake()
        {
            instance = this;
            logger = Logger;
            config = Config;

            string[] args = Environment.GetCommandLineArgs();

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-noravenm")
                {
                    Logger.LogWarning($"Plugin {MyPluginInfo.PLUGIN_GUID} is canceled to load!");
                    throw new Exception("Cancel load");
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
            showBuildGUID = Config.Bind("General.Toggles",
                "Show GUID",
                true,
                "Show GUID on screen.").Value;
            chatWidth = Config.Bind("General.ChatField",
                "Chat Width",
                500f,
                "Chat field width.").Value;
            chatHeight = Config.Bind("General.ChatField",
                "Chat Height",
                200f,
                "Chat field height.").Value;
            chatYOffset = Config.Bind("General.ChatField",
                "Chat YOffset",
                370f,
                "Chat field y-axis position.").Value;
            chatXOffset = Config.Bind("General.ChatField",
                "Chat XOffset",
                10f,
                "Chat field x-axis position.").Value;
            chatFontSize = Config.Bind("General.ChatField",
                "Chat Font Size",
                0,
                "Change the font size of chat field(0 is disable).").Value;
            allowClientDifference = Config.Bind("General.Toggles",
                "Allow client difference",
                false,
                "Allow host and players use plugins or game with different version.").Value;
            if (chatFontSize != 0)
                changeChatFontSize = true;
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

            foreach (var argument in args)
            {
                if (argument.Contains("="))
                {
                    string[] argumentVals = argument.Split('=');
                    string argumentName = argumentVals[0];
                    string argumentValue = argumentVals[1];
                    Arguments.Add(argumentName, argumentValue);
                }
                else
                {
                    Arguments.Add(argument, "");
                }
            }
        }
        private void OnGUI()
        {
            if (showBuildGUID) GUI.Label(new Rect(10, Screen.height - 20, 400, 40), $"RavenM ID: {BuildGUID}");
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
            }
            else if (!JoinedLobbyFromArgument && Arguments.ContainsKey("-ravenm-lobby"))
            {
                JoinLobbyFromArgument();
            }
            else if (showBuildGUID)
                this.enabled=false;
        }

        void JoinLobbyFromArgument()
        {
            JoinedLobbyFromArgument = true;
            CSteamID lobbyId = new CSteamID(ulong.Parse(Arguments["-ravenm-lobby"]));
            SteamMatchmaking.JoinLobby(lobbyId);
            LobbySystem.instance.InLobby = true;
            LobbySystem.instance.IsLobbyOwner = false;
            LobbySystem.instance.LobbyDataReady = false;
        }
    }
}
