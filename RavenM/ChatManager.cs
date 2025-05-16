using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using HarmonyLib;
using Steamworks;
using System.Collections;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using UnityEngine;
using Ravenfield.SpecOps;
using RavenM.Commands;

namespace RavenM
{
    /// <summary>
    /// Handles the display and backend for the Chat menu 
    /// </summary>
    public class ChatManager : MonoBehaviour
    {
        private string _currentChatMessage = string.Empty;
        public string CurrentChatMessage
        {
            get { return _currentChatMessage; }
            set { _currentChatMessage = value; }
        }
        private string _fullChatLink = string.Empty;
        
        /// <summary>
        /// The full chat transcript
        /// </summary>
        public string FullChatLink
        {
            get { return _fullChatLink; }
            set { _fullChatLink = value; }
        }
        private Vector2 _chatScrollPosition = Vector2.zero;
        public Vector2 ChatScrollPosition
        {
            get { return _chatScrollPosition; }
            set { _chatScrollPosition = value; }
        }
        private List<string> _chatPositionOptions = new List<string>
        {
            "Left",
            "Right"
        };
        public List<string> ChatPositionOptions
        {
            get { return _chatPositionOptions; }
        }
        private int _selectedChatPosition;
        public int SelectedChatPosition
        {
            get { return _selectedChatPosition; }
            set { _selectedChatPosition = value; }
        }
        private Texture2D _greyBackground = new Texture2D(1, 1);
        public Texture2D GreyBackground
        {
            get { return _greyBackground; }
            set { _greyBackground = value; }
        }
        private bool _justFocused = false;
        public bool JustFocused
        {
            get { return _justFocused; }
            set { _justFocused = value; }
        }
        private bool _typeIntention = false;
        public bool TypeIntention
        {
            get { return _typeIntention; }
            set { _typeIntention = value; }
        }
        private bool _chatMode = false;
        
        /// <summary>
        /// If true, chat message is global.
        /// If false, chat message is team only.
        /// </summary>
        public bool ChatMode
        {
            get { return _chatMode; }
            set { _chatMode = value; }
        }
        private CommandManager _commandManager;
        public CommandManager CommandManager
        {
            get { return _commandManager; }
            set { _commandManager = value; }
        }
        private KeyCode _globalChatKeybind = KeyCode.Y;
        public KeyCode GlobalChatKeybind
        {
            get { return _globalChatKeybind; }
            set { _globalChatKeybind = value; }
        }
        private KeyCode _teamChatKeybind = KeyCode.U;
        public KeyCode TeamChatKeybind
        {
            get { return _teamChatKeybind; }
            set { _teamChatKeybind = value; }
        }

        /// <summary>
        /// Client's steam id
        /// </summary>
        private CSteamID _steamId;
        public CSteamID SteamId
        {
            get { return _steamId; }
            set { _steamId = value; }
        }

        /// <summary>
        /// Client's steam username
        /// </summary>
        private string _steamUsername;
        public string SteamUsername
        {
            get { return _steamUsername; }
            private set { _steamUsername = value; }
        }

        public static ChatManager instance;

        private void Awake()
        {
            instance = this;

            GreyBackground.SetPixel(0, 0, Color.grey * 0.3f);
            GreyBackground.Apply();

            CommandManager = new CommandManager();

            SteamId = SteamUser.GetSteamID();
        }

        private void Start()
        {
            Callback<PersonaStateChange_t>.Create(OnPersonaStateChange);
            Callback<LobbyChatMsg_t>.Create(OnLobbyChatMessage);
            Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdate);
            SteamUsername = SteamFriends.GetFriendPersonaName(SteamId);
        }

        private void OnPersonaStateChange(PersonaStateChange_t pCallback)
        {
            if (SteamId == (CSteamID)pCallback.m_ulSteamID)
            {
                SteamUsername = SteamFriends.GetFriendPersonaName(SteamId);
            }
        }

        private void OnLobbyChatMessage(LobbyChatMsg_t pCallback)
        {
            ulong steamId = pCallback.m_ulSteamIDUser;
            var buf = new byte[4096];
            int len = SteamMatchmaking.GetLobbyChatEntry(LobbySystem.instance.ActualLobbyID, (int)pCallback.m_iChatID, out CSteamID user, buf, buf.Length, out EChatEntryType chatType);
            string chat = DecodeLobbyChat(buf, len);

            if (steamId != SteamId.m_SteamID)
            {
                if (chat.StartsWith("/") && user == LobbySystem.instance.OwnerID)
                {
                    ProcessLobbyChatCommand(chat, SteamId.m_SteamID, false);
                }
                else
                {
                    PushLobbyChatMessage(chat, SteamFriends.GetFriendPersonaName((CSteamID)steamId));
                }
            }

        }

        private void OnLobbyChatUpdate(LobbyChatUpdate_t pCallback)
        {
            // Anything other than a join...
            if ((pCallback.m_rgfChatMemberStateChange & (uint)EChatMemberStateChange.k_EChatMemberStateChangeEntered) == 0)
            {
                var id = new CSteamID(pCallback.m_ulSteamIDUserChanged);

                // ...means the owner left.
                if (LobbySystem.instance.OwnerID == id)
                {
                    LobbySystem.instance.NotificationText = "Lobby closed by host.";
                    SteamMatchmaking.LeaveLobby(LobbySystem.instance.ActualLobbyID);
                }
            }
            else
            {
                var id = new CSteamID(pCallback.m_ulSteamIDUserChanged);

                if (LobbySystem.instance.CurrentBannedMembers.Contains(id))
                {
                    SendLobbyChat($"/ban {id}");
                }
            }
        }

        /// <summary>
        /// Push message to chat transcript. Clients will not see messages here until sent
        /// </summary>
        /// <param name="actor"></param>
        /// <param name="message"></param>
        /// <param name="global"></param>
        /// <param name="team"></param>
        public void PushChatMessage(Actor actor, string message, bool global, int team)
        {
            string name;
            if (actor != null)
                name = actor.name;
            else
                name = "";
            if (!global && GameManager.PlayerTeam() != team)
                return;

            if (team == -1)
                FullChatLink += $"<color=#eeeeee>{message}</color>\n";
            else
            {
                string color = !global ? "green" : (team == 0 ? "blue" : "red");
                FullChatLink += $"<color={color}><b><{name}></b></color> {message}\n";
                RSPatch.RavenscriptEventsManagerPatch.events.onReceiveChatMessage.Invoke(actor, message);
            }

            _chatScrollPosition.y = Mathf.Infinity;
        }

        /// <summary>
        /// Add message to chat transcipt without determining the client's team. Clients will not see messages here until sent
        /// </summary>
        /// <seealso cref="SendLobbyChat(string)"/>
        /// <param name="message"></param>
        /// <param name="steamUsername"></param>
        public void PushLobbyChatMessage(string message, string steamUsername)
        {
            // Players have no team in lobby so everyone is the same color
            string color = "white";
            FullChatLink += $"<color={color}><b><{steamUsername}></b></color> {message}\n";

            _chatScrollPosition.y = Mathf.Infinity;
        }

        /// <summary>
        /// Sends a message without a username. Intended for messages directed at the player and not an actual chat message
        /// </summary>
        /// <param name="message"></param>
        public void PushLobbyChatMessage(string message)
        {
            FullChatLink += $"{message}\n";

            _chatScrollPosition.y = Mathf.Infinity;
        }

        /// <summary>
        /// Sends command result back to clients and displays in chat area
        /// </summary>
        /// <param name="message"></param>
        /// <param name="color"></param>
        /// <param name="teamOnly"></param>
        /// <param name="sendToAll"></param>
        public void PushLobbyCommandChatMessage(string message, Color color, bool teamOnly, bool sendToAll)
        {
            FullChatLink += $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{message}</color>\n";
            _chatScrollPosition.y = Mathf.Infinity;
            if (!sendToAll)
                return;
            SendLobbyChat(message);
        }

        /// <summary>
        /// Sends command result back to clients and displays in chat area
        /// </summary>
        /// <param name="message"></param>
        /// <param name="color"></param>
        /// <param name="teamOnly"></param>
        /// <param name="sendToAll"></param>
        public void PushCommandChatMessage(string message, Color color, bool teamOnly, bool sendToAll)
        {
            FullChatLink += $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{message}</color>\n";
            _chatScrollPosition.y = Mathf.Infinity;
            if (!sendToAll)
                return;
            using MemoryStream memoryStream = new MemoryStream();
            var chatPacket = new ChatPacket
            {
                Id = ActorManager.instance.player.GetComponent<GuidComponent>().guid,
                Message = message,
                TeamOnly = teamOnly,
            };

            using (var writer = new ProtocolWriter(memoryStream))
            {
                writer.Write(chatPacket);
            }
            byte[] data = memoryStream.ToArray();

            IngameNetManager.instance.SendPacketToServer(data, PacketType.Chat, Constants.k_nSteamNetworkingSend_Reliable);
        }

        /// <summary>
        /// Processes commands for lobby chat
        /// </summary>
        /// <param name="message"></param>
        /// <param name="id"></param>
        /// <param name="local"></param>

        // FIXME: This method should be part of the Command class
        // and split so each command handles their own backend
        public void ProcessLobbyChatCommand(string message, ulong id, bool local, Actor actor=null)
        {
            Plugin.logger.LogInfo("plcc " + message + " " +local);
            string messageTrimed = message.Trim();
            string[] commands = messageTrimed.Substring(1, messageTrimed.Length-1).Split(' ');
            if (commands.Length < 1) {
                PushLobbyCommandChatMessage($"Syntax error", Color.red, false, false);
            }

            string initCommand = commands[0];
            Command cmd = CommandManager.GetCommandFromName(initCommand);
            if (!CommandManager.ContainsCommand(initCommand))
            {
                PushLobbyCommandChatMessage($"Unknown command", Color.red, false, false);
                return;
            }

            if (!(cmd.AllowInLobby && !GameManager.IsIngame()) && !(cmd.AllowInGame && GameManager.IsIngame()))
            {
                PushLobbyCommandChatMessage(cmd.AllowInGame ? "This command is disabled when not in gaming" : "This command is disabled in gaming", Color.red, true, false);
                return;
            }

            bool hasCommandPermission = CommandManager.HasPermission(cmd, id, local);
            if (!hasCommandPermission)
            {
                PushCommandChatMessage("Access Denied", Color.red, false, false);
                return;
            }

            try
            {
            switch (cmd.CommandName)
            {
                    case "tags":
                    if (!local)
                    {
                        UI.GameUI.instance.ToggleNameTags();
                        PushLobbyCommandChatMessage("Set nametags to " + commands[1], Color.white, false, false);
                        break;
                    }

                        bool needEnable = true;
                        bool isTeamOnly = false;
                        string outputMessage = null;
                        if (commands[1] == "off")
                            needEnable = false;
                        else if (commands[1] == "team")
                            isTeamOnly = true;
                        else if (commands[1] != "on")
                        {
                            needEnable = bool.Parse(commands[1]);
                            outputMessage = needEnable ? "on" : "off";
                            isTeamOnly = false;
                        }
                        
                        LobbySystem.instance.SetLobbyDataDedup("nameTags", needEnable.ToString());
                        LobbySystem.instance.SetLobbyDataDedup("nameTagsForTeamOnly", isTeamOnly.ToString());
                        PushLobbyCommandChatMessage("Set nametags to " + outputMessage != null ? outputMessage : commands[1], Color.white, false, false);
                        UI.GameUI.instance.ToggleNameTags();

                    break;
                case "help":
                        if (commands.Length < 2)
                        {
                            string availableCommandsText = "";
                            foreach (Command availableCommand in CommandManager.GetAllCommands())
                            {
                                availableCommandsText = availableCommand.CommandName + " " + availableCommandsText;
                            }
                            PushLobbyChatMessage($"All available commands, use `/help <command>` for more details:\n  {availableCommandsText}");
                            break;
                        }

                        bool foundCommand = false;
                        foreach (Command command in CommandManager.GetAllCommands())
                        {
                            if (command.CommandName == commands[1])
                            {
                                PushLobbyChatMessage($"{command.SyntaxMessage}\n {command.HelpMessage}");
                                foundCommand = true;
                                break;
                            }
                        }
                        if (!foundCommand)
                        {
                            PushLobbyCommandChatMessage($"Command not found", Color.red, false, false);
                    }
                    break;
                    case "ban":
                        if (!local)
                        {
                            bool targetIsClient = false;
                            if (ulong.TryParse(commands[1], out ulong memberIdI))
                    {
                        var member = new CSteamID(memberIdI);
                                Plugin.logger.LogInfo(SteamId+ " "+member);

                                if (member == SteamId && !LobbySystem.instance.IsLobbyOwner)
                                    targetIsClient = true;
                            }
                            else
                            {
                                //Turn space into `_` so that substringing's result wont be error 
                                var clientPlayerName = SteamFriends.GetFriendPersonaName(SteamId).Replace(" ", "_");
                                Plugin.logger.LogInfo(clientPlayerName+ " " + commands[1]);
                                if (commands[1] == clientPlayerName && !LobbySystem.instance.IsLobbyOwner)
                                    targetIsClient = true;
                            }

                                Plugin.logger.LogInfo(targetIsClient);

                            if (targetIsClient)
                        {
                                LobbySystem.instance.NotificationText = "You were banned from the lobby!";
                            SteamMatchmaking.LeaveLobby(LobbySystem.instance.ActualLobbyID);
                        }
                        }
                        else
                        {
                            if (ulong.TryParse(commands[1], out ulong memberIdUlong))
                            {
                                var memberIda = new CSteamID(memberIdUlong);
                                if (LobbySystem.instance.GetLobbyMembers().Contains(memberIda) && memberIda !=LobbySystem.instance.OwnerID)
                                {
                                    PushLobbyCommandChatMessage($"Banned {SteamFriends.GetFriendPersonaName(memberIda)} ({memberIda})", Color.white, false, true);
                                    LobbySystem.instance.CurrentBannedMembers.Add(memberIda);
                                }
                                else
                                {
                                    PushLobbyCommandChatMessage($"Player {commands[1]} is not exist or you are banning youeself", Color.red, false, false);
                                }
                            }
                            else
                            {
                                bool targetFound = false;
                                foreach (var memberIdb in LobbySystem.instance.GetLobbyMembers())
                                {
                                    if (commands[1] == SteamFriends.GetFriendPersonaName(memberIdb) && memberIdb != LobbySystem.instance.OwnerID)
                                    {
                                        LobbySystem.instance.CurrentBannedMembers.Add(memberIdb);
                                        PushLobbyCommandChatMessage($"Banned {SteamFriends.GetFriendPersonaName(memberIdb)} ({memberIdb})", Color.white, false, true);
                                        targetFound = true;
                                        break;
                                    }
                                }
                                if (!targetFound)
                                {
                                    PushLobbyCommandChatMessage($"Player {commands[1]} is not exist or you are banning youeself", Color.red, false, false);
                                }
                            }
                        }
                        break;
                    case "unban":
                        if (!local)
                            break;
                        var memberId = new CSteamID(ulong.Parse(commands[1]));
                        if (LobbySystem.instance.CurrentBannedMembers.Contains(memberId))
                        {
                            LobbySystem.instance.CurrentBannedMembers.Remove(memberId);
                            PushLobbyCommandChatMessage($"Unbanned {SteamFriends.GetFriendPersonaName(memberId)} ({memberId})", Color.white, false, true);
                        }
                        else
                        {
                            PushLobbyCommandChatMessage($"Player {commands[1]} is not exist or you are unbaning youeself", Color.red, false, true);
                        }
                    break;
                    case "kill":
                        string target = commands[1];
                        Actor targetActor = CommandManager.GetActorByName(target);
                        if (targetActor == null)
                        {
                            return;
                        }
                        targetActor.Kill(new DamageInfo(DamageInfo.DamageSourceType.FallDamage, actor, null));
                        if(!local)
                            PushCommandChatMessage($"Killed actor {targetActor.name}", Color.white, false, false);
                        break;
                default:
                    // TODO: Allow other mods to handle commands from the lobby
                    Plugin.logger.LogInfo("Lobby onReceiveCommand " + initCommand);
                        RSPatch.RavenscriptEventsManagerPatch.events.onReceiveCommand.Invoke(actor, commands, new bool[] { hasCommandPermission, true, !local });
                    break;
            }
            }
            catch (Exception e)
            {
                Plugin.logger.LogError(e.ToString());
                if (local)
                    PushCommandChatMessage($"{cmd.SyntaxMessage}",Color.red,false,false);
            }

            //if (cmd.Global == true && local == true)
            //SendLobbyChat(message);
        }

        /// <summary>
        /// Processess commands for ingame chat
        /// </summary>
        /// <param name="message"></param>
        /// <param name="actor"></param>
        /// <param name="id"></param>
        /// <param name="local"></param>
        
        // FIXME: This method should be part of the Command class
        // and split so each command handles their own backend
        public void ProcessChatCommand(string message, Actor actor, ulong id, bool local)
        {
            ProcessLobbyChatCommand(message,id,local,actor);
                return;
        }

        /// <summary>
        /// Sends a message directly to Steam via SteamMatchmaking.SendLobbyChatMsg
        /// </summary>
        /// <param name="message"></param>
        public void SendLobbyChat(string message)
        {
            var bytes = Encoding.UTF8.GetBytes(message);
            SteamMatchmaking.SendLobbyChatMsg(LobbySystem.instance.ActualLobbyID, bytes, bytes.Length);
        }

        public string DecodeLobbyChat(byte[] bytes, int len)
        {
            // Don't want some a-hole crashing the lobby.
            try
            {
                return Encoding.UTF8.GetString(bytes, 0, len);
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Creates the events for interacting with the chat area
        /// </summary>
        /// <param name="isLobbyChat">If true, the chat message won't attempt to get the player's current team for their name colour. False by default</param>
        /// <param name="chatWidth">The width of the chat area. 500f by default</param>
        /// <param name="yOffset">Sets how far from the top of the screen the chat input box should be located. 160f by default</param>
        /// <param name="xOffset">Sets how far from the left side of the screen the chat input box should be located. 10f by default</param>
        public void InitializeChatArea(bool isLobbyChat = false, float chatWidth = 500f, float yOffset = 160f, float xOffset = 10f)
        {
            if (Event.current.isKey && Event.current.keyCode == KeyCode.None && JustFocused)
            {
                Event.current.Use();
                JustFocused = false;
                return;
            }

            if (Event.current.isKey && (Event.current.keyCode == KeyCode.Tab || Event.current.character == '\t'))
                Event.current.Use();

            if (TypeIntention)
            {
                GUI.SetNextControlName("chat");
                CurrentChatMessage = GUI.TextField(new Rect(xOffset, Screen.height - 160f, (chatWidth - 70f), 25f), CurrentChatMessage);
                GUI.FocusControl("chat");

                string color = !ChatMode ? "green" : (GameManager.PlayerTeam() == 0 ? "blue" : "red");
                string text = ChatMode ? "GLOBAL" : "TEAM";
                GUI.Label(new Rect(xOffset + (chatWidth - 60f), Screen.height - yOffset, 70f, 25f), $"<color={color}><b>{text}</b></color>");

                if (Event.current.isKey && Event.current.keyCode == KeyCode.Escape && TypeIntention)
                {
                    TypeIntention = false;
                }

                if (Event.current.isKey && Event.current.keyCode == KeyCode.Return)
                {
                    if (!string.IsNullOrEmpty(CurrentChatMessage))
                    {
                        var currentChatMessageTrimed = CurrentChatMessage.Trim();
                        bool isCommand = currentChatMessageTrimed.StartsWith("/") | currentChatMessageTrimed.StartsWith("、")  ? true : false;
                        if (isCommand)
                        {
                            ProcessLobbyChatCommand(CurrentChatMessage.Replace("、","/"), SteamId.m_SteamID, true);
                            CurrentChatMessage = string.Empty;
                        }
                        else
                        {
                            if (isLobbyChat)
                            {
                                Plugin.logger.LogInfo($"{CurrentChatMessage}{SteamUsername}");
                                PushLobbyChatMessage(CurrentChatMessage, SteamUsername);
                                SendLobbyChat(CurrentChatMessage);
                            }
                            else
                            {
                                PushChatMessage(ActorManager.instance.player, CurrentChatMessage, ChatMode, GameManager.PlayerTeam());

                                // Send message to users in lobby if not team chat
                                // TODO: Get messages sent from in game -> lobby
                                // if (ChatMode)
                                // {
                                //     SendLobbyChat(CurrentChatMessage);
                                // }
                                
                                using MemoryStream memoryStream = new MemoryStream();
                                var chatPacket = new ChatPacket
                                {
                                    Id = ActorManager.instance.player.GetComponent<GuidComponent>().guid,
                                    Message = CurrentChatMessage,
                                    TeamOnly = !ChatMode,
                                };

                                using (var writer = new ProtocolWriter(memoryStream))
                                {
                                    writer.Write(chatPacket);
                                }
                                byte[] data = memoryStream.ToArray();

                                IngameNetManager.instance.SendPacketToServer(data, PacketType.Chat, Constants.k_nSteamNetworkingSend_Reliable);
                            }

                            CurrentChatMessage = string.Empty;
                        }
                    }
                    TypeIntention = false;
                }
            }

            if (Event.current.isKey && Event.current.keyCode == GlobalChatKeybind && !TypeIntention)
            {
                TypeIntention = true;
                JustFocused = true;
                ChatMode = true;
            }

            if (Event.current.isKey && Event.current.keyCode == TeamChatKeybind && !TypeIntention && !isLobbyChat)
            {
                TypeIntention = true;
                JustFocused = true;
                ChatMode = false;
            }
        }

        /// <summary>
        /// Draws the chat area
        /// </summary>
        /// <param name="isLobbyChat">If true, the chat message won't attempt to get the player's current team for their name colour. False by default</param>
        /// <param name="chatWidth">The width of the chat area. 500f by default</param>
        /// <param name="chatHeight">The height of the chat area. 200f by default</param>
        /// <param name="chatYOffset">Sets how far from the top of the screen the chat area should be located. 370f by default</param>
        /// <param name="chatXOffset">Sets how far from the left side of the screen the chat area should be located. 10f by default</param>
        /// <param name="wordWrap">Sets whether text should wrap. True by default</param>
        /// <param name="resetScrollPosition">If false, the scroll position (if applicable) will be maintained when creating the chat area. True by default</param>
        public void CreateChatArea(bool isLobbyChat = false, float chatWidth = 500f, float chatHeight = 200f, float chatYOffset = 370f, float chatXOffset = 10f, bool wordWrap = true, bool resetScrollPosition = true)
        {
            InitializeChatArea(isLobbyChat, chatWidth, 160f, chatXOffset);

            var chatStyle = new GUIStyle();
            chatStyle.normal.background = GreyBackground;
            
            var textStyle = new GUIStyle();
            textStyle.wordWrap = wordWrap;
            textStyle.normal.textColor = Color.white;
            if (!wordWrap)
                textStyle.wordWrap = false;
                
            GUILayout.BeginArea(new Rect(chatXOffset, Screen.height - chatYOffset, chatWidth, chatHeight), string.Empty, chatStyle);
            GUILayout.BeginVertical();
            GUILayout.Space(10);
            ChatScrollPosition = GUILayout.BeginScrollView(ChatScrollPosition, GUILayout.Width(chatWidth), GUILayout.Height(chatHeight - 15f));
            // Any player can break the formatting by using Rich Text e.g. <color=abcd> <b> - Chai
            if (Plugin.changeChatFontSize)
                GUILayout.Label($"<size={Plugin.chatFontSize}>{FullChatLink}</size>", textStyle, GUILayout.Width(chatWidth - 30f));
            else
                GUILayout.Label(FullChatLink, textStyle, GUILayout.Width(chatWidth - 30f));
            GUILayout.EndScrollView();
            GUILayout.Space(10);
            GUILayout.EndVertical();
            GUILayout.EndArea();
            
            if (resetScrollPosition)
            {
                _chatScrollPosition.y = Mathf.Infinity;
            }
        }

        public void ResetChat()
        {
            FullChatLink = string.Empty;
        }
    }
}
