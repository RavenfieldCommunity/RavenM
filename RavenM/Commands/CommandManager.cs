using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace RavenM.Commands
{
    public class CommandManager
    {
        public static CommandManager Instance;
        private List<Command> Commands;

        public CommandManager()
        {
            Commands = new List<Command>();
            // dont push pure text message to remote chat, only after processing
            Commands.Add(new Command(
                _name: "help",
                _global: false,
                _reqArgs: null,
                _hostOnly: false,
                scripted: true,
                allowInLobby: true,
                allowInGame: true,
                helpMessage: "Get help of specific command or get all available commands",
                syntaxMessage: "/help <command name>")
            {
                Action = (string originalStringTrimed, bool isLocal) =>
                {
                    string[] commands = SplitSingleArgument(originalStringTrimed);
                    if (commands.Length == 1)
                    {
                        string availableCommandsText = "";
                        foreach (Command availableCommand in GetAllCommands())
                        {
                            availableCommandsText = availableCommand.CommandName + " " + availableCommandsText;
                        }
                        ChatManager.instance.AppendToChatLink(ChatManager.HASH_USER_NULL, $"All available commands, use `/help <command name>` for more details:\n  {availableCommandsText}");
                        return;
                    }

                    bool foundCommand = false;
                    string targetCommandName = commands[1];
                    foreach (Command command in GetAllCommands())
                    {
                        if (command.CommandName == targetCommandName)
                        {
                             ChatManager.instance.AppendToChatLink(ChatManager.HASH_USER_NULL, $"{command.SyntaxMessage}\n  {command.HelpMessage}");
                            foundCommand = true;
                            break;
                        }
                    }
                    if (!foundCommand)
                        ChatManager.instance.AppendToChatLink(ChatManager.HASH_USER_NULL, $"Command `{targetCommandName}` not found", ChatManager.HASH_COLOR_RED);
                }
            }
            );
            Commands.Add(new Command(
                _name: "tags",
                _global: true,
                _reqArgs: null,
                _hostOnly: true,
                scripted: true,
                allowInLobby: true,
                allowInGame: true,
                helpMessage: "Enable nametags for global or not, or only for team",
                syntaxMessage: "/tags (on|off|team)")
            {
                Action = (string originalStringTrimed, bool isLocal) =>
                {
                    string[] commands = SplitSingleArgument(originalStringTrimed);
                    string arg = commands[1];
                    if (!isLocal)
                    {
                        UI.GameUI.instance.ToggleNameTags();
                        ChatManager.instance.AppendToChatLink(ChatManager.HASH_USER_NULL, "Set nametags to " + arg);
                        return;
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
                    ChatManager.instance.AppendToChatLink(ChatManager.HASH_USER_NULL, "Set nametags to " + outputMessage != null ? outputMessage : arg);
                    UI.GameUI.instance.ToggleNameTags();
                }
            }
            );
            Commands.Add(new Command(
                _name: "kill",
                _global: true,
                _reqArgs: null,
                _hostOnly: true,
                scripted: true,
                allowInLobby: false,
                allowInGame: true,
                helpMessage: "Kill specific player or bot",
                syntaxMessage: "/kill <name>")
            {
                Action = (string originalStringTrimed, bool isLocal) =>
                {
                    string[] commands = SplitSingleArgument(originalStringTrimed);
                    string targetName = commands[1];
                    Actor targetActor = GetActor(targetName);
                    if (targetActor == null)
                    {
                        return;
                    }
                    targetActor.KillSilently();
                    ChatManager.instance.AppendToChatLink(ChatManager.HASH_USER_NULL, $"Killed actor {targetActor.name}");
                }
            }
            );
            Commands.Add(new Command(
                _name: "ban",
                _global: true,
                _reqArgs: null,
                _hostOnly: true,
                scripted: true,
                allowInLobby: true,
                allowInGame: true,
                helpMessage: "Ban player out of lobby",
                syntaxMessage: "/ban (<steam id>|<steam name>)")
            {
                needSendManually = true,
                Action = (string originalStringTrimed, bool isLocal) =>
                {
                    string[] commands = SplitSingleArgument(originalStringTrimed);
                    string targetNameString = commands[1];
                    if (!isLocal)
                    {
                        bool targetIsClient = false;
                        if (ulong.TryParse(targetNameString, out ulong memberIdI))
                        {
                            var member = new CSteamID(memberIdI);
                            Plugin.logger.LogInfo(ChatManager.instance.SteamId + " " + member);

                            if (member == ChatManager.instance.SteamId && !LobbySystem.instance.IsLobbyOwner)
                                targetIsClient = true;
                        }
                        else
                        {
                            //Turn space into `_` so that substringing's result wont be error 
                            var clientPlayerName = SteamFriends.GetFriendPersonaName(ChatManager.instance.SteamId).Replace(" ", "_");
                            if (targetNameString == clientPlayerName && !LobbySystem.instance.IsLobbyOwner)
                                targetIsClient = true;
                        }

                        if (targetIsClient)
                        {
                            LobbySystem.instance.NotificationText = "You were banned from the lobby!";
                            if (GameManager.IsIngame())
                                IngameMenuUi.instance.Menu();  // Unless this method, others will be blocked by RavenM itself lol
                            else
                                SteamMatchmaking.LeaveLobby(LobbySystem.instance.ActualLobbyID);
                        }
                    }
                    else
                    {
                        // TODO: zip the code
                        if (ulong.TryParse(targetNameString, out ulong memberIdUlong))
                        {
                            var memberIda = new CSteamID(memberIdUlong);
                            if (LobbySystem.instance.GetLobbyMembers().Contains(memberIda) && memberIda != LobbySystem.instance.OwnerID)
                            {
                                ChatManager.instance.AppendToChatLink(ChatManager.HASH_USER_NULL, $"Banned {SteamFriends.GetFriendPersonaName(memberIda)} ({memberIda})");
                                LobbySystem.instance.CurrentBannedMembers.Add(memberIda);
                                DelayCloseMemberConnection(memberIda);
                            }
                            else
                            {
                                ChatManager.instance.AppendToChatLink(ChatManager.HASH_USER_NULL, $"Player `{targetNameString}` is not exist or you are banning youeself", ChatManager.HASH_COLOR_RED);
                            }
                        }
                        else
                        {
                            bool targetFound = false;
                            foreach (var memberIdb in LobbySystem.instance.GetLobbyMembers())
                            {
                                if (targetNameString == SteamFriends.GetFriendPersonaName(memberIdb) && memberIdb != LobbySystem.instance.OwnerID)
                                {
                                    LobbySystem.instance.CurrentBannedMembers.Add(memberIdb);
                                    ChatManager.instance.AppendToChatLink(ChatManager.HASH_USER_NULL, $"Banned {SteamFriends.GetFriendPersonaName(memberIdb)} ({memberIdb})");
                                    // lol steam sometime wnot sync player's nickname, so sending the user id is better
                                    ChatManager.instance.SendLobbyChat($"/ban {memberIdb}");
                                    DelayCloseMemberConnection(memberIdb);
                                    targetFound = true;
                                    break;
                                }
                            }
                            if (!targetFound)
                            {
                                ChatManager.instance.AppendToChatLink(ChatManager.HASH_USER_NULL, $"Player `{targetNameString}` is not exist or you are banning youeself", ChatManager.HASH_COLOR_RED);
                            }
                        }
                    }
                }
            }
            );
            Commands.Add(new Command(
                _name: "unban",
                _global: true,
                _reqArgs: null,
                _hostOnly: true,
                scripted: true,
                allowInLobby: true,
                allowInGame: true,
                helpMessage: "Unban player, use `@a` to unban all",
                syntaxMessage: "/unban (<steamid>|<steam name>|@a)")
            {
                needSendManually = true,
                Action = (string originalStringTrimed, bool isLocal) =>
                {
                    string targetNameString = SplitSingleArgument(originalStringTrimed)[1];

                    if (targetNameString == "@a")
                    {
                        ChatManager.instance.AppendToChatLink(ChatManager.HASH_USER_NULL, "Unbanned all");
                        LobbySystem.instance.CurrentBannedMembers.Clear();
                        return;
                    }

                    if (ulong.TryParse(targetNameString, out ulong memberId))
                    {
                        var csteamID = new CSteamID(memberId);
                        if (LobbySystem.instance.CurrentBannedMembers.Contains(csteamID))
                        {
                            LobbySystem.instance.CurrentBannedMembers.Remove(csteamID);
                            ChatManager.instance.AppendToChatLink(ChatManager.HASH_USER_NULL, $"Unbanned {SteamFriends.GetFriendPersonaName(csteamID)} ({memberId})");
                        }
                        else
                        {
                            if (isLocal)
                                ChatManager.instance.AppendToChatLink(ChatManager.HASH_USER_NULL, $"Player `{targetNameString}` is not exist or you are unbanning youeself", ChatManager.HASH_COLOR_RED);
                        }
                    }
                    else
                    {
                        bool targetFound = false;
                        foreach (var memberIdI in LobbySystem.instance.CurrentBannedMembers)
                        {
                            if (SteamFriends.GetFriendPersonaName(memberIdI) == targetNameString)
                            {
                                LobbySystem.instance.CurrentBannedMembers.Remove(memberIdI);
                                ChatManager.instance.AppendToChatLink(ChatManager.HASH_USER_NULL, $"Unbanned {targetNameString} ({memberIdI})");
                            }
                        }
                        if (isLocal && !targetFound)
                            ChatManager.instance.AppendToChatLink(ChatManager.HASH_USER_NULL, $"Player `{targetNameString}` is not exist or you are unbanning youeself", ChatManager.HASH_COLOR_RED);
                    }

                }
            }
            );
            Commands.Add(new Command(
                _name: "tp",
                _global: false,
                _reqArgs: null,
                _hostOnly: true,
                scripted: true,
                allowInLobby: false,
                allowInGame: true,
                helpMessage: "transfer actor A to actor B's position. Remember to turn ` ` into `_`!",
                syntaxMessage: "/tp (<selector>|<nameA>|<steam idA>) (@s|<nameB>|<steam idB>)")
            {
                needSendManually = true,
                Action = (string originalStringTrimed, bool isLocal) =>
                {
                    Actor targetB;
                    string[] commands = originalStringTrimed.Split(' ');
                    string targetBString = commands[2];
                    if (ulong.TryParse(targetBString, out ulong memberId))
                    {
                        targetB = GetActor(memberId);
                    }
                    else
                    {
                        if (targetBString == "@s") targetB = FpsActorController.instance.actor;
                        else targetB = GetActor(targetBString);
                    }
                    if (targetB == null)
                    {
                        ChatManager.instance.AppendToChatLink(ChatManager.HASH_USER_NULL, "Target B not found", ChatManager.HASH_COLOR_RED);
                        throw new Exception("No target B");
                    }
                    Plugin.logger.LogInfo($"Target B to tp: {targetB.name}");

                    List<Actor> targetsA = new List<Actor>();
                    string targetsAString = commands[1];
                    if (targetsAString[0] == '@')
                    {
                        targetsA = GetActors(targetsAString);
                    }
                    else
                    {
                        if (ulong.TryParse(targetsAString, out ulong memberIdI))
                        {
                            targetsA.Add(GetActor(memberIdI));
                        }
                        else
                        {
                            targetsA.Add(GetActor(targetsAString));
                        }
                    }
                    if (targetsA.Count == 0)
                    {
                        ChatManager.instance.AppendToChatLink(ChatManager.HASH_USER_NULL, "Target A not found", ChatManager.HASH_COLOR_RED);
                        throw new Exception("No target A");
                    }
                    Plugin.logger.LogInfo($"Targets A to tp: {targetsA.Count}");

                    ChatManager.instance.AppendToChatLink(ChatManager.HASH_USER_NULL, $"Move {targetsAString} to {targetBString}");
                    foreach (var singleA in targetsA)
                    {
                        if (singleA != null && !(singleA.controller as NetActorController) & !singleA.dead && !singleA.IsSeated())
                            singleA.controller.Move(targetB.transform.position - singleA.transform.position);
                    }

                }
            }
            );
            Plugin.logger.LogInfo("CommandManager registered commands: " + Commands.Count);
        }


        public Command GetCommandFromName(string command)
        {
            return Commands.SingleOrDefault(x => string.Equals(x.CommandName, command, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Split the single command arg after remove `/`
        /// </summary>
        /// <param name="originalCommandTrimed"></param>
        /// <returns></returns>
        public string[] SplitSingleArgument(string originalCommandTrimed)
        {
            return originalCommandTrimed.Substring(1, originalCommandTrimed.Length - 1).Split([' '], 2);
        }
        public bool ContainsCommand(string command)
        {
            foreach (Command cmd in Commands)
            {
                if (string.Equals(cmd.CommandName, command, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
        public List<Command> GetAllCommands()
        {
            return Commands;
        }
        public List<Command> GetAllLobbyCommands()
        {
            return Commands.Where(command => command.AllowInLobby == true).ToList();
        }
        public List<Command> GetAllIngameCommands()
        {
            return Commands.Where(command => command.AllowInGame == true).ToList();
        }
        public void AddCustomCommand(Command cmd)
        {
            Commands.Add(cmd);
        }
        public int GetPlayerGuid(Actor actor)
        {
            GuidComponent guidComp = actor.GetComponent<GuidComponent>();
            if (guidComp != null)
            {
                return guidComp.guid;
            }
            return 0;
        }
        public string GetRequiredArgTypes(Command cmd)
        {
            if (cmd.reqArgs[0] == null)
            {
                return $"/{cmd.CommandName}";
            }
            string requiredTypes = $"/{cmd.CommandName}";
            for (int x = 0; x < cmd.reqArgs.Length; x++)
            {
                requiredTypes += $" <{cmd.reqArgs[x].GetType().ToString()}>";
            }
            return requiredTypes;
        }
        private void PrintNotEnoughArguments(Command cmd)
        {
            ChatManager.instance.AppendToChatLink(ChatManager.HASH_USER_NULL, $"Not enough Arguments for Command {cmd.CommandName}. \nUsage: {GetRequiredArgTypes(cmd)}.", ChatManager.HASH_COLOR_RED);
        }
        private void PrintCouldNotConvert(Command cmd)
        {
            ChatManager.instance.AppendToChatLink(ChatManager.HASH_USER_NULL, $"Could not convert Argument(s) for Command {cmd.CommandName}. \nUsage: {GetRequiredArgTypes(cmd)}.", ChatManager.HASH_COLOR_RED);
        }
        public bool HasRequiredArgs(Command cmd, string[] command)
        {
            // TODO: is args check needed? as there will be a bug when there are optional args, i think try and catch block is enough
            return true;
            /*
            // Shift Array by one to the right because command[0] would be the initCommand  - Chryses
            string[] args = new string[command.Length - 1];
            Array.Copy(command, 1, args, 0, command.Length - 1);
            // For Testing
            if (Plugin.changeGUID)
            {
                foreach (string arg in args)
                {
                    Plugin.logger.LogInfo("Arg: " + arg);
                }
                Plugin.logger.LogInfo("Size reqArgs " + cmd.reqArgs.Length + " Size command " + args.Length);
            }
            int reqArgsCount = cmd.reqArgs.Length;
            if ((reqArgsCount) != args.Length)
            {
                PrintNotEnoughArguments(cmd);
                return false;
            }
            int convertedArgCounter = 0;
            for (int x = 0; x < reqArgsCount; x++)
            {
                var arg = args[x];
                Plugin.logger.LogInfo("Trying to convert " + arg);
                if (string.IsNullOrEmpty(arg))
                    return false;
                if (arg.Equals(cmd.CommandName))
                    continue;
                Type type = cmd.reqArgs[x].GetType();
                object convertedArg;
                try
                {
                    convertedArg = Convert.ChangeType(arg, type);
                }
                catch (FormatException exe)
                {
                    Plugin.logger.LogError(exe.Message);
                    PrintCouldNotConvert(cmd);
                    return false;
                }
                if (convertedArg == null)
                {
                    return false;
                }
                if (type == convertedArg.GetType())
                {
                    convertedArgCounter++;
                }
            }
            if (convertedArgCounter == reqArgsCount)
                return true;
            return false;
            */
        }


        public Actor GetActor(string name)
        {

            foreach (var item in IngameNetManager.instance.ClientActors.Values)
            {
                // the `name` has benn already Replace(" ", "_")
                if (item.name.ToLower().Replace(" ", "_") == name.ToLower())
                {
                    return item;
                }
            }

            return null;
        }

        public void DelayCloseMemberConnection(CSteamID id)
        {
            Task.Run(() =>
            {
                Thread.Sleep(10*1000);  // 10s only to close connection forcely
                foreach (var connection in IngameNetManager.instance.ServerConnections)
                {
                    if (SteamNetworkingSockets.GetConnectionInfo(connection, out SteamNetConnectionInfo_t pInfo) && pInfo.m_identityRemote.GetSteamID() == id && LobbySystem.instance.CurrentBannedMembers.Contains(id))
                    {
                        SteamNetworkingSockets.CloseConnection(connection, 1000, null, false);
                    }
                }
            });
        }

        public Actor GetActor(ulong steamId)
        {
            var memberId = new CSteamID(steamId);
            foreach (Actor actor in IngameNetManager.instance.GetPlayers())
            {
                if (actor.name.ToLower().Replace(" ", "_") == SteamFriends.GetFriendPersonaName(memberId).ToLower().Replace(" ", "_"))
                {
                    return actor;
                }
            }
            return null;
        }

        public List<Actor> GetActors(string targetSelector)
        {
            bool botState = false; // whether select bot?
            bool isAll = false;  // whether select all?
            var targetState = targetSelector.Substring(targetSelector.Length - 1);
            if (targetState == "p") botState = false;
            else if (targetState == "b") botState = true;
            else isAll = true;

            string prefixRealSelector;
            if (isAll) prefixRealSelector = targetSelector;
            else prefixRealSelector = targetSelector.Replace(targetState, "");

            var list = new List<Actor>();
            var etor = IngameNetManager.instance.ClientActors.GetEnumerator();
            while (etor.MoveNext())
            {
                if (isAll | etor.Current.Value.aiControlled == botState)
                {
                    if (prefixRealSelector == "@a")
                    {
                        list.Add(etor.Current.Value);
                    }
                    else if (prefixRealSelector == "@e" && etor.Current.Value.team == 0)
                    {
                        list.Add(etor.Current.Value);
                    }
                    else if (prefixRealSelector == "@r" && etor.Current.Value.team == 1)
                    {
                        list.Add(etor.Current.Value);
                    }
                }
            }
            return list;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="command"></param>
        /// <param name="id">The steam id of the player who sent the command </param>
        /// <param name="local"></param>
        /// <returns></returns>
        public bool HasPermission(Command command, ulong id, bool local)
        {
            //Plugin.logger.LogInfo(id + " from packet " + " == " + LobbySystem.instance.OwnerID.m_SteamID);
            if (command.HostOnly)
            {
                if (id == LobbySystem.instance.OwnerID.m_SteamID)
                    return true;
            }
            else
            {
                return true;
            }
            if (!local)
            {
                return true;
            }
            return false;
        }
    }
}
