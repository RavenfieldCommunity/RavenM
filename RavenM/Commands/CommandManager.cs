﻿using Steamworks;
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
        public string GetRequiredArgTypes(Command cmd)
        {
            if (cmd.reqArgs[0] == null)
            {
                return $"/{cmd.CommandName}";
            }
            string requiredTypes = $"/{cmd.CommandName}";
            for(int x = 0; x < cmd.reqArgs.Length; x++)
            {
                requiredTypes += $" <{cmd.reqArgs[x].GetType().ToString()}>";
            }
            return requiredTypes;
        }
        private void PrintNotEnoughArguments(Command cmd)
        {
            ChatManager.instance.PushCommandChatMessage($"Not enough Arguments for Command {cmd.CommandName}. \nUsage: {GetRequiredArgTypes(cmd)}.", Color.red,true, false); ;
        }
        private void PrintCouldNotConvert(Command cmd)
        {
            ChatManager.instance.PushCommandChatMessage($"Could not convert Argument(s) for Command {cmd.CommandName}. \nUsage: {GetRequiredArgTypes(cmd)}.", Color.red,true, false); ;
        }
        public bool HasRequiredArgs(Command cmd, string[] command)
        {
            return true;
            // Shift Array by one to the right because command[0] would be the initCommand  - Chryses
            string[] args = new string[command.Length - 1];
            Array.Copy(command, 1, args, 0, command.Length - 1);
            // For Testing
            if (Plugin.changeGUID)
            {
                foreach (string arg in args)
                {
                    Plugin.logger.LogInfo("Arg: " + arg );
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
            for(int x = 0;x < reqArgsCount; x++)
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
                }catch(FormatException exe)
                {
                    Plugin.logger.LogError(exe.Message);
                    PrintCouldNotConvert(cmd);
                    return false;
                }
                if(convertedArg == null)
                {
                    return false;
                }
                if (type == convertedArg.GetType())
                {
                    convertedArgCounter++;
                }
            }
            if(convertedArgCounter == reqArgsCount)
                return true;
            return false;
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
                Plugin.logger.LogInfo("ohhi");
                return true;
            }
            return false;
        }
    }
}
