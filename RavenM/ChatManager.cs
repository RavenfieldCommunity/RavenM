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
using System.Runtime.CompilerServices;

namespace RavenM
{
    /// <summary>
    /// Handles the display and backend for the Chat menu 
    /// </summary>
    public class ChatManager : MonoBehaviour
    {
        /// <summary>
        /// This client's text in chat input field
        /// </summary>
        public string CurrentChatMessage = string.Empty;

        /// <summary>
        /// Last chat message in `FullChatLink`, including from all client
        /// </summary>
        public string LastChatMessage = string.Empty;

        /// <summary>
        /// For command intelligencement and warning
        /// </summary>
        public string InteralMessageToAppend;
        /// <summary>
        /// Priority lower than above one, for marker and voice count
        /// </summary>
        public string InteralMessageToAppend2;

        /// <summary>
        /// The full chat transcript
        /// </summary>
        public string FullChatLink = string.Empty;
        public Vector2 ChatScrollPosition= Vector2.zero;
        private List<string> _chatPositionOptions = new List<string>
        {
            "Left",
            "Right"
        };
        public List<string> ChatPositionOptions
        {
            get { return _chatPositionOptions; }
        }
        public int SelectedChatPosition;
        public Texture2D GreyBackground = new Texture2D(1, 1);
        public bool JustFocused = false;
        /// <summary>
        /// Is user typing message?
        /// </summary>
        public bool TypeIntention = false;

        /// <summary>
        /// If true, chat message is global.
        /// If false, chat message is team only.
        /// </summary>
        public bool ChatMode = false;
        public CommandManager CommandManager;
        public KeyCode GlobalChatKeybind = KeyCode.Y;
        public KeyCode TeamChatKeybind = KeyCode.U;

        /// <summary>
        /// Client's steam id
        /// </summary>
        public CSteamID SteamId;

        /// <summary>
        /// Client's steam username
        /// </summary>
        public string SteamUsername;

        public static ChatManager instance;

        public float ChatFieldHiddenUntilTime;

        // configs
        public float chatWidth = 500f;
        public float chatHeight = 200f;
        public float chatYOffset = 160f;
        public float chatXOffset = 10f;
        public int chatFontSize = 10;
        public int chatFieldHiddenDelay = 0;

        public const ulong HASH_USER_NULL = 0;
        public const string HASH_COLOR_RED = "red";
        public const string HASH_CHAT_TEAM = "team:";

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

        private void OnGUI()
        {
            if (LobbySystem.instance.InLobby)
                CreateChatArea();
        }

        private void OnPersonaStateChange(PersonaStateChange_t pCallback)
        {
            if (SteamId == (CSteamID)pCallback.m_ulSteamID)
            {
                SteamUsername = SteamFriends.GetFriendPersonaName(SteamId);
            }
        }

        /// <summary>
        /// This callback process all message
        /// </summary>
        /// <param name="pCallback"></param>
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
                    ProcessCommand(chat, steamId, false);
                }
                else
                {
                    if (chat.StartsWith(HASH_CHAT_TEAM))
                        AppendToChatLink(steamId, chat.Remove(0, HASH_CHAT_TEAM.Length), teamOnly: true);
                    else
                        AppendToChatLink(steamId, chat);
                }
            }

        }

        /// <summary>
        /// Used to process lobby states update
        /// </summary>
        /// <param name="pCallback"></param>
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
        /// Added a single message to chat field. Can also use it as to send message to user himself
        /// </summary>
        public void AppendToChatLink(ulong userId, string message, string colorString = "white", bool teamOnly = false)
        {
            string team = LobbySystem.instance.GetLobbyMemberData(new CSteamID(userId), "team");
            string clientTeam = LobbySystem.instance.GetLobbyMemberData(SteamId, "team");
            bool isUserRealEnemyTeam = team != clientTeam & team != LobbySystem.HASH_LOBBYDATA_TEAM_I;

            if (isUserRealEnemyTeam)
                return;
            if (teamOnly)
            {
                if (team == clientTeam) colorString = "green";
                else if (isUserRealEnemyTeam) colorString = "red";
            }

            string nameHeadProcessed = userId == HASH_USER_NULL ? "" : System.Text.RegularExpressions.Regex.Unescape(SteamFriends.GetFriendPersonaName(new CSteamID(userId)));

            FinalAppendToChatLink(nameHeadProcessed, message, colorString);
        }

        public void AppendToChatLink(string nameHead, string message, string colorString = "white")
        {
            // add team condition
            string nameHeadProcessed = nameHead;

            FinalAppendToChatLink(nameHead, message, colorString);
        }

        private void FinalAppendToChatLink(string nameHeadProcessed, string message, string colorString = "white")
        {
            LastChatMessage = $"{(nameHeadProcessed == "" ? "" : $"<color={colorString}><b><{nameHeadProcessed}></b></color> ")}{System.Text.RegularExpressions.Regex.Unescape(message)}\n";
            FullChatLink += LastChatMessage;

            ChatScrollPosition.y = Mathf.Infinity;
            if (chatFieldHiddenDelay != 0) ChatFieldHiddenUntilTime = Time.time + chatFieldHiddenDelay;
        }

        /*
        /// <summary>
        /// Process remote message from to chat transcript. Clients will not see messages here until sent. Used when it is team-only message, otherwise pls use `SendLobbyChat()`
        /// </summary>
        /// <param name="actor"></param>
        /// <param name="message"></param>
        /// <param name="global"></param>
        /// <param name="team"></param>

        public void PushChatMessage(ulong username, string message, bool global, int team)
        {
            name = SteamFriends.GetFriendPersonaName(new CSteamID(username));
            if (!global && GameManager.PlayerTeam() != team)
                return;

            string color = !global ? "green" : (team == -1 ? "white" : (team == 0 ? "blue" : "red"));
            AppendToChatLink(name, message, color);
            RSPatch.RavenscriptEventsManagerPatch.events.onReceiveChatMessage.Invoke(null, message);
        }

        /// <summary>
        /// Sends a message without a username. Intended for messages directed at the player and not an actual chat message
        /// </summary>
        /// //
        /// <param name="message"></param>
        [Obsolete]
        public void PushLobbyChatMessage(string message)
        {
            AppendToChatLink(HASH_USER_NULL,message);
        }

        
        /// <summary>
        /// Sends command result back to clients and displays in chat area
        /// </summary>
        /// <param name="message"></param>
        /// <param name="color"></param>
        /// <param name="teamOnly"></param>
        /// <param name="sendToAll"></param>
        [Obsolete]
        public void PushLobbyCommandChatMessage(string message, Color color, bool teamOnly, bool sendToAll)
        {
            FullChatLink += $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{message}</color>\n";
            ChatScrollPosition.y = Mathf.Infinity;
            if (!sendToAll)
                return;
            SendLobbyChat(message);
        }

        // i think shouldnt send message as udp connection is not stable
        /// <summary>
        /// Sends command result back to clients and displays in chat area
        /// </summary>
        /// <param name="message"></param>
        /// <param name="color"></param>
        /// <param name="teamOnly"></param>
        /// <param name="sendToAll"></param>
        [Obsolete]
        public void PushCommandChatMessage(string message, Color color, bool teamOnly, bool sendToAll)
        {

            PushLobbyCommandChatMessage(message, color, teamOnly, sendToAll);
            return;
            FullChatLink += $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{message}</color>\n";
            ChatScrollPosition.y = Mathf.Infinity;
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
        */
        

        /// <summary>
        /// Processes command, the command trigger is from the steam chat callback
        /// </summary>
        /// <param name="message"></param>
        /// <param name="id">User who send it to check access</param>
        /// <param name="local"></param>
        public void ProcessCommand(string message, ulong id, bool local, Actor actor = null)
        {
            string messageTrimed = message.Trim();
            string[] commands = CommandManager.SplitSingleArgument(messageTrimed);
            if (commands.Length < 1)
            {
                AppendToChatLink(HASH_USER_NULL, $"Syntax error", HASH_COLOR_RED);
                return;
            }
            
            string targetCommandName = commands[0];
            Command cmd = CommandManager.GetCommandFromName(targetCommandName);
            if (!CommandManager.ContainsCommand(targetCommandName))
            {
                AppendToChatLink(HASH_USER_NULL, $"Unknown command `{targetCommandName}`", HASH_COLOR_RED);
                return;
            }

            if (!(cmd.AllowInLobby && !GameManager.IsIngame()) && !(cmd.AllowInGame && GameManager.IsIngame()))
            {
                AppendToChatLink(HASH_USER_NULL, cmd.AllowInGame ? $"Command `{targetCommandName}` is disabled when not in gaming" : $"Command `{targetCommandName}` is disabled in gaming", HASH_COLOR_RED);
                return;
            }

            bool hasCommandPermission = CommandManager.HasPermission(cmd, id, local);
            if (!CommandManager.HasPermission(cmd, id, local))
            {
                AppendToChatLink(0, $"Access denied with command `{targetCommandName}`", HASH_COLOR_RED);
                return;
            }

            try 
            {
                if ( local | cmd.Global )  // filter the no-need-to-run command which from non-local
                    cmd.Action(messageTrimed, local);
                // TODO: Allow other mods to handle commands from the lobby
                Plugin.logger.LogInfo("Lobby onReceiveCommand " + targetCommandName);
                RSPatch.RavenscriptEventsManagerPatch.events.onReceiveCommand.Invoke(actor, commands, new bool[] { hasCommandPermission, true, !local });
            }
            catch (Exception e)
            {
                Plugin.logger.LogError(e.ToString());
                if (local) // if the command isnt from local, then no need to push message to chat field
                    AppendToChatLink(HASH_USER_NULL, cmd.SyntaxMessage, HASH_COLOR_RED);
            }

            if (cmd.Global == true && local == true && !cmd.needSendManually)
                SendLobbyChat(message);
        }

        /// <summary>
        /// Sends a message directly to Steam via `SteamMatchmaking.SendLobbyChatMsg()` before entering game (WAIT chat packet in gaming is sent by server socket, and before it is by this? WTF)
        /// </summary>
        /// <param name="message"></param>
        public void SendLobbyChat(string message)
        {
            var bytes = Encoding.UTF8.GetBytes(message);
            SteamMatchmaking.SendLobbyChatMsg(LobbySystem.instance.ActualLobbyID, bytes, bytes.Length);
        }

        /// <summary>
        /// For what received from other client's `SendLobbyChat()`
        /// </summary>
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
        private void InitializeChatArea()
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
                CurrentChatMessage = GUI.TextField(new Rect(chatXOffset, Screen.height - 160f, (chatWidth - 70f), 25f), CurrentChatMessage);
                GUI.FocusControl("chat");

                string color = !ChatMode ? "green" : (GameManager.PlayerTeam() == 0 ? "blue" : "red");
                string text = ChatMode ? "GLOBAL" : "TEAM";
                GUI.Label(new Rect(chatXOffset + (chatWidth - 60f), Screen.height - chatYOffset + chatHeight + 5, 70f, 25f), $"<color={color}><b>{text}</b></color>");

                if (Event.current.isKey && Event.current.keyCode == KeyCode.Escape && TypeIntention)
                {
                    TypeIntention = false;
                }

                if (Event.current.isKey && Event.current.keyCode == KeyCode.Return)
                {
                    if (!string.IsNullOrEmpty(CurrentChatMessage))
                    {
                        var currentChatMessageTrimed = CurrentChatMessage.Trim();
                        bool isCommand = currentChatMessageTrimed.StartsWith("/") | currentChatMessageTrimed.StartsWith("、") ? true : false;
                        if (isCommand)
                        {
                            ProcessCommand(CurrentChatMessage.Replace("、", "/"), SteamId.m_SteamID, true);
                            CurrentChatMessage = string.Empty;
                        }
                        else
                        {
                            AppendToChatLink(SteamId.m_SteamID, CurrentChatMessage);
                            SendLobbyChat($"{(ChatMode ? "" : HASH_CHAT_TEAM)}{CurrentChatMessage}");
                            /*
                            // Send message to users in lobby if not team chat
                            // TODO: Get messages sent from in game -> lobby
                            // just said that if some players arent entered the map yet but if we want them get message
                            // it is needed? idk
                            // now it is added
                            // if (ChatMode)
                            // {
                            //     SendLobbyChat(CurrentChatMessage);
                            // }
                            //
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
                            */
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

            if (Event.current.isKey && Event.current.keyCode == TeamChatKeybind && !TypeIntention)
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
        private void CreateChatArea(bool wordWrap = true, bool resetScrollPosition = true)
        {
            InitializeChatArea();

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
            // idk if rich text is important, but `\n` is
            if (chatFieldHiddenDelay == 0 | TypeIntention | Time.time < ChatFieldHiddenUntilTime)
            {
                if (chatFontSize != 0)
                    GUILayout.Label($"<size={chatFontSize}>{FullChatLink}\n{InteralMessageToAppend2}\n{InteralMessageToAppend}</size>", textStyle, GUILayout.Width(chatWidth - 30f));
                else
                    GUILayout.Label(FullChatLink, textStyle, GUILayout.Width(chatWidth - 30f));
            }
            GUILayout.EndScrollView();
            GUILayout.Space(10);
            GUILayout.EndVertical();
            GUILayout.EndArea();

            if (resetScrollPosition)
            {
                ChatScrollPosition.y = Mathf.Infinity;
            }
        }

        public void ResetChat()
        {
            FullChatLink = string.Empty;
        }
    }
}
