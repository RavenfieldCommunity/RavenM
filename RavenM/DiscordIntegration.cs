using System;
using System.Collections;
using System.IO;
using RavenM.DiscordGameSDK;
using Steamworks;
using UnityEngine;
using System.Runtime.InteropServices;

namespace RavenM
{
    public class DiscordIntegration : MonoBehaviour
    {
        public static DiscordIntegration instance;
        
        public Discord Discord;

        public long discordClientID = 1007054793220571247;

        public long startSessionTime;

        private ActivityManager _activityManager;

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern IntPtr LoadLibrary(string lpPathName);

        private void Start()
        {
            if (Environment.OSVersion.Platform == PlatformID.Win32NT && !Environment.Is64BitProcess)
            {
                LoadLibrary("BepInEx/plugins/lib/x86/discord_game_sdk");
            }
            // Non BepInEx Plugin dlls have no effect in systems that are not windows
            // copying them to ravenfield_Data/Plugins seems to fix it
            else if(Environment.OSVersion.Platform == PlatformID.Unix)
            {
                if (Directory.Exists("ravenfield_Data")) // Assume its the linux installation
                {
                    if (!File.Exists("ravenfield_Data/Plugins/discord_game_sdk.so"))
                    {
                        Plugin.logger.LogWarning("Linux Discord Library Not Found, Attempting to Copy it from lib folder");
                    
                        File.Copy("BepInEx/plugins/lib/discord_game_sdk.so","ravenfield_Data/Plugins/discord_game_sdk.so");
                    }
                }
                else if (Directory.Exists("ravenfield.app")) // Assume its the MacOS installation
                {
                    if (!File.Exists("ravenfield.app/Contents/Plugins/discord_game_sdk.dylib"))
                    {
                        Plugin.logger.LogWarning("MacOS Discord Library Not Found, Attempting to Copy it from lib folder");
                    
                        File.Copy("BepInEx/plugins/lib/discord_game_sdk.dylib","ravenfield.app/Contents/Plugins/discord_game_sdk.dylib");
                    }
                    if (!File.Exists("ravenfield.app/Contents/Plugins/discord_game_sdk.bundle"))
                    {
                        Plugin.logger.LogWarning("MacOS Discord Library Not Found, Attempting to Copy it from lib folder");
                    
                        File.Copy("BepInEx/plugins/lib/discord_game_sdk.bundle","ravenfield.app/Contents/Plugins/discord_game_sdk.bundle");
                    }
                }
                
            }

            try
            {
                Discord = new Discord(discordClientID, (UInt64)CreateFlags.NoRequireDiscord);
            }
            catch
            {
                Plugin.logger.LogWarning("Failed to initialize Discord pipe.");
                return;
            }
            
            Plugin.logger.LogInfo("Discord Instance created");
            startSessionTime = ((DateTimeOffset) DateTime.Now).ToUnixTimeSeconds();
            
            _activityManager = Discord.GetActivityManager();
            
            StartCoroutine(StartActivities());
            
            _activityManager.OnActivityJoin += secret =>
            {
                secret = secret.Replace("_join", "");
                
                Plugin.logger.LogInfo($"OnJoin {secret}");
                var LobbyID = new CSteamID(ulong.Parse(secret));

                if (_isInGame)
                {
                    GameManager.ReturnToMenu();
                }
                
                SteamMatchmaking.JoinLobby(LobbyID);
                LobbySystem.instance.InLobby = true;
                LobbySystem.instance.IsLobbyOwner = false;
                LobbySystem.instance.LobbyDataReady = false;
            };
            
            _activityManager.OnActivityJoinRequest += (ref User user) =>
            {
                // The Ask to join Button Doesnt even work rn (Discord's fault) try the right click Ask to join button instead
                Plugin.logger.LogInfo($"OnJoinRequest {user.Username} {user.Id}");
            };
        }
        
        IEnumerator StartActivities()
        {
            UpdateActivity(Discord, Activities.InitialActivity);
            yield return new WaitUntil(GameManager.IsInMainMenu);
            UpdateActivity(Discord, Activities.InMenu);
        }

        // Private Variables that makes me question my coding skills
        private TimedAction _timer = new TimedAction(5f);
        
        private string _gameMode = "Insert Game Mode";
        private void FixedUpdate()
        {
            if (Discord == null)
                return;

            Discord.RunCallbacks();

            if (_timer.TrueDone())
            {
                ChangeActivityDynamically();

                _timer.Start();
            }
        }

        private bool _isInGame;
        private bool _isInLobby;

        void ChangeActivityDynamically()
        {
            if (GameManager.instance == null) { return; }

            _isInGame = GameManager.instance.ingame;
            _isInLobby = LobbySystem.instance.InLobby;


            if (_isInGame && !_isInLobby)
            {
                UpdateActivity(Discord, Activities.InSinglePlayerGame, true,LobbySystem.instance.currentGameMode.ToString());
            }
            else if (_isInLobby)
            {
                int currentLobbyMembers = SteamMatchmaking.GetNumLobbyMembers(LobbySystem.instance.ActualLobbyID);
                int currentLobbyMemberCap = SteamMatchmaking.GetLobbyMemberLimit(LobbySystem.instance.ActualLobbyID);

                if (!_isInGame) // Waiting in Lobby
                {
                    UpdateActivity(Discord, Activities.InLobby, false , LobbySystem.instance.currentGameMode.ToString(), currentLobbyMembers, currentLobbyMemberCap, LobbySystem.instance.ActualLobbyID.ToString());
                }
                else // Playing in a Lobby
                {
                    UpdateActivity(Discord, Activities.InLobby, true ,_gameMode, currentLobbyMembers, currentLobbyMemberCap, LobbySystem.instance.ActualLobbyID.ToString());
                }
            }
            else // Left the lobby
            {
                UpdateActivity(Discord, Activities.InMenu);
            }
        }
        
        public void UpdateActivity(Discord discord, Activities activity, bool inGame = false, string gameMode = "None", int currentPlayers = 1, int maxPlayers = 2, string lobbyID = "None")
        {
            var activityManager = discord.GetActivityManager();
            var activityPresence = new Activity();
            
            switch (activity)
            {
                case Activities.InitialActivity:
                    activityPresence = new Activity()
                    {
                        State = "Just Started Playing",
                        Assets =
                        {
                            LargeImage = "rfimg_1_",
                            LargeText = "RavenM",
                        },
                        Instance = true,
                    };
                    break;
                case Activities.InMenu:
                    activityPresence = new Activity()
                    {
                        State = "Waiting In Menu",
                        Assets =
                        {
                            LargeImage = "rfimg_1_",
                            LargeText = "RavenM",
                        },
                        Instance = true,
                    };
                    break;
                case Activities.InLobby:
                    var state = inGame ? "Playing Multiplayer" : "Waiting In Lobby";
                    activityPresence = new Activity()
                    {
                        State = state,
                        Details = $"Game Mode: {gameMode}",
                        Timestamps =
                        {
                            Start = startSessionTime,
                        },
                        Assets =
                        {
                            LargeImage = "rfimg_1_",
                            LargeText = "RavenM",
                        },
                        Party = {
                            Id = lobbyID,
                            Size = {
                                CurrentSize = currentPlayers,
                                MaxSize = maxPlayers,
                            },
                        },
                        Secrets =
                        {
                            Join = lobbyID + "_join",
                        },
                        Instance = true,
                    };
                    break;
                case Activities.InSinglePlayerGame:
                    activityPresence = new Activity()
                    {
                        State = "Playing Singleplayer",
                        Timestamps =
                        {
                            Start = startSessionTime,
                        },
                        Assets =
                        {
                            LargeImage = "rfimg_1_",
                            LargeText = "RavenM",
                        },
                        Instance = true,
                    };
                    break;
                    
               
            }
            activityManager.UpdateActivity(activityPresence, result =>
            {
                if (result != Result.Ok)
                    Plugin.logger.LogWarning($"Update Discord Activity Err {result}");
            });
        }

        public enum Activities
        {
            InitialActivity,
            InMenu,
            InLobby,
            InSinglePlayerGame,
        }
    }
}
