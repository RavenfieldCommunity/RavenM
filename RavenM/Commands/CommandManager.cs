using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
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
            //ban tp 
            Commands.Add(new Command(
                _name: "help",
                _global: false, 
                _reqArgs:null,
                _hostOnly: false, 
                scripted: true,
                allowInLobby: true,
                allowInGame: true,
                helpMessage:"Get help of specific command or get all available commands",
                syntaxMessage:"/help <command name>")
            );
            Commands.Add(new Command(
                _name: "tags",
                _global: true, 
                _reqArgs:null,
                _hostOnly: true, 
                scripted: true, 
                allowInLobby: true,
                allowInGame: true,
                helpMessage:"Enable nametags for global or not, or only for team",
                syntaxMessage:"/tags (on|off|team)")
            );
            Commands.Add(new Command(
                _name: "kill", 
                _global: true, 
                _reqArgs:null,
                _hostOnly: true, 
                scripted: true, 
                allowInLobby: false, 
                allowInGame: true,
                helpMessage:"Kill specific player",
                syntaxMessage:"/kill <player name>")
            );
            Commands.Add(new Command(
                _name: "ban", 
                _global: true,
                _reqArgs:null,
                _hostOnly: true,
                scripted: true,
                allowInLobby: true,
                allowInGame: true,
                helpMessage:"Ban player out of lobby",
                syntaxMessage:"/ban (<player steamid>|<player steam name>)")
            );
            Commands.Add(new Command(
                _name: "unban", 
                _global: true,
                _reqArgs:null,
                _hostOnly: true,
                scripted: true,
                allowInLobby: true,
                allowInGame: true,
                helpMessage:"Unban player ",
                syntaxMessage:"/unban <player steamid>")
            );
            /*Commands.Add(new Command(
                _name: "godi", 
                _global: true,
                _reqArgs:null,
                _hostOnly: true,
                scripted: true,
                allowInLobby: false,
                allowInGame: true,
                helpMessage:"Give specific player premission of using God Inspect if it is disabled",
                syntaxMessage:"/godi <player steamid>")
            );
            Commands.Add(new Command(
                _name: "ungodi", 
                _global: true,
                _reqArgs:null,
                _hostOnly: true,
                scripted: true,
                allowInLobby: false,
                allowInGame: true,
                helpMessage:"Cancel specific player's premission of using God Inspect",
                syntaxMessage:"/ungodi <player steamid>")
            );
            Commands.Add(new Command(
                _name: "tp", 
                _global: true,
                _reqArgs:null,
                _hostOnly: true,
                scripted: true,
                allowInLobby: true,
                allowInGame: true,
                helpMessage:"",
                syntaxMessage:"/ungodi <player steamid>")
            );*/
            Plugin.logger.LogInfo("CommandManager registered commands: " + Commands.Count);
        }
        public Command GetCommandFromName(string command)
        {
            return Commands.SingleOrDefault(x => string.Equals(x.CommandName, command, StringComparison.OrdinalIgnoreCase));
        }
        public bool ContainsCommand(string command)
        {
            foreach(Command cmd in Commands)
            {
                if(string.Equals(cmd.CommandName, command, StringComparison.OrdinalIgnoreCase))
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

        public bool HasRequiredArgs(Command cmd,object[] obj)
        {
            return true;
        }

        
        public Actor GetActorByName(string name)
        {
            foreach (Actor actor in ActorManager.instance.actors)
            {
                if (actor.name.ToLower() == name.ToLower())
                {
                    return actor;
                }
            }
            return null;
        }
        
        public bool HasPermission(Command command, ulong id,bool local)
        {
            Plugin.logger.LogInfo(id + " from packet " + " " + LobbySystem.instance.OwnerID.m_SteamID);
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
                Plugin.logger.LogInfo("ohhi");
                return true;
            }
            return false;
        }
    }
}
