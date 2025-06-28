using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace RavenM.Commands
{
    public class Command
    {
        public string CommandName { get; set; }
        public object[] reqArgs { get; set; }

        /// <summary>
        /// Is the command will be applied to all the client?
        /// </summary>
        // TODO: merge with `needSendManually` ？
        public bool Global { get; set; }
        public bool HostOnly { get; set; }
        public bool Scripted { get; set; }
        public Action<string, bool> Action { get; set; }

        /// <summary>
        /// Does the command will send the command message by itself? 
        /// </summary>
        public bool needSendManually { get; set; } = false;

        /// <summary>
        /// The command is allowed to run before enter game?
        /// </summary>
        public bool AllowInLobby { get; set; }

        /// <summary>
        /// The command is allowed to run in game?
        /// </summary>
        public bool AllowInGame { get; set; }

        /// <summary>
        /// Summary of the command
        /// </summary>
        public string HelpMessage { get; set; }

        /// <summary>
        /// Syntax expression of the command, only for player but not argument check.
        /// 
        /// Refer: https://minecraft.fandom.com/wiki/Commands#Syntax
        /// </summary>
        public string SyntaxMessage { get; set; }

        public Command(string _name, object[] _reqArgs, bool _global, bool _hostOnly, bool scripted = false, bool allowInLobby = false, bool allowInGame = true, string helpMessage = "", string syntaxMessage = "")
        {
            CommandName = _name;
            Global = _global;
            HostOnly = _hostOnly;
            Scripted = scripted;
            AllowInLobby = allowInLobby;
            AllowInGame = allowInGame;
            HelpMessage = helpMessage;
            SyntaxMessage = syntaxMessage;
        }

    }

}
