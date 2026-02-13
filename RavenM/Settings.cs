using BepInEx.Configuration;
using BepInEx;
using System;
using UnityEngine;
using RavenM.UI;
using System.Threading.Tasks;

namespace RavenM;

/// <summary>
/// Class to manage configs, no too much perfromance problem as the `.Value` is stored by the class `ConfigEntry` itself
/// </summary>
public static class Settings
{
    public static ConfigEntry<bool> debugMode;
    public static ConfigEntry<bool> showIngameUI;
    public static ConfigEntry<int> nameTagFontSize;
    public static ConfigEntry<bool> enableNameTagCustomColor;
    public static ConfigEntry<string> nameTagColorTeam;
    public static ConfigEntry<string> nameTagColorEnemy;
    public static ConfigEntry<float> voiceChatVolume;
    public static ConfigEntry<KeyboardShortcut> voiceChatKeybind;
    public static ConfigEntry<bool> useClassicKeybindHook;
    public static ConfigEntry<bool> forceRepeatConnecting;
    public static ConfigEntry<KeyboardShortcut> globalChatKeybind;
    public static ConfigEntry<KeyboardShortcut> teamChatKeybind;
    public static ConfigEntry<KeyboardShortcut> placeMarkerKeybind;
    public static ConfigEntry<float> chatWidth;
    public static ConfigEntry<float> chatHeight;
    public static ConfigEntry<float> chatYOffset;
    public static ConfigEntry<float> chatXOffset;
    public static ConfigEntry<int> chatFontSize;
    public static ConfigEntry<bool> showInLobbyMenuAtPauseMenu;
    public static ConfigEntry<string> tipsAnnoucement;
    public static ConfigEntry<string> lastGetTipsAnnoucementDate;
    public static ConfigEntry<int> markerAliveTime;
    public static ConfigEntry<int> chatFieldHiddenDelay;
    public static ConfigEntry<bool> showTipsAnnouncement;
    public static void Init()
    {
        var config = Plugin.config;
        showIngameUI = config.Bind("RavenM.IngameUI", "Show ingame UI", true, "Show all ingame UI including chat field, nametags and others");
        nameTagFontSize = config.Bind("RavenM.IngameUI.NameTags", "Nametag font size", 32, "");
        enableNameTagCustomColor = config.Bind("RavenM.IngameUI.NameTags", "Nametag custom color", false, "");
        nameTagColorTeam = config.Bind("RavenM.IngameUI.NameTags", "Nametag team color", "#1E90FF", "");
        nameTagColorEnemy = config.Bind("RavenM.IngameUI.NameTags", "Nametag enemy color", "#FFA500", "");
        voiceChatVolume = config.Bind("RavenM.Keybinds.Chat", "Voice chat volume", 1f, "");
        useClassicKeybindHook = config.Bind("RavenM.Keybinds.Chat", "Use classic keybind hook", false, "");
        voiceChatKeybind = config.Bind("RavenM.Keybinds.Chat", "Voice chat keybind", new KeyboardShortcut(KeyCode.CapsLock), "");
        globalChatKeybind = config.Bind("RavenM.Keybinds.Chat", "Global chat keybind", new KeyboardShortcut(KeyCode.Y), "");
        teamChatKeybind = config.Bind("RavenM.Keybinds.Chat", "Team chat keybind", new KeyboardShortcut(KeyCode.U), "");
        placeMarkerKeybind = config.Bind("RavenM.Keybinds.Chat", "Place marker keybind", new KeyboardShortcut(KeyCode.BackQuote), "");
        chatWidth = config.Bind("RavenM.IngameUI.Chat", "Chat Width", 500f, "Chat field width.");
        chatHeight = config.Bind("RavenM.IngameUI.Chat","Chat field height",200f,"Chat field height.");
        chatYOffset = config.Bind("RavenM.IngameUI.Chat","Chat field YOffset",370f,"Chat field y-axis position.");
        chatXOffset = config.Bind("RavenM.IngameUI.Chat","Chat field XOffset",10f,"Chat field x-axis position.");
        chatFontSize = config.Bind("RavenM.IngameUI.Chat","Chat field font size",0,"Change the font size of chat field(0 is disable).");
        showInLobbyMenuAtPauseMenu = config.Bind("RavenM.IngameUI.InLobbyMenu", "Show InLobbyMenu at pause menu", true, "Show InLobbyMenu at pause menu, otherwise at loadout ui");
        markerAliveTime = config.Bind("RavenM.Configs","Marker alive time",20,"How long the marker will keep showing by default(0 is disable to hide automatically).");
        chatFieldHiddenDelay = config.Bind("RavenM.Configs","Chat field hidden delay",0,"How long will chat field keep showing after delay when new message received(0 is disable to hide automatically).");
        showTipsAnnouncement = config.Bind("RavenM.Configs","Show tips announcement", true,"");
        forceRepeatConnecting = config.Bind("RavenM.Configs","Force repeat connecting", true,"Auto reepeat connecting to server when timeout");
        debugMode = config.Bind("RavenM.ZDebug", "Debug mode", false, "");
        tipsAnnoucement = config.Bind("RavenM.ZDebug.Data", "tipsAnnoucement", "Remember to check updates on our site often!\n---\nThank all testers in discord server for debugging!", "");
        lastGetTipsAnnoucementDate = config.Bind("RavenM.ZDebug.Data", "lastGetTipsAnnoucementDate", "", "");

        config.SettingChanged += (sender, arg) => { Task.Run(OnSettingUpdate); };
    }

    public static void OnSettingUpdate()
    {
        if (IngameNetManager.instance != null)
        {
            IngameNetManager.instance.VoiceChatVolume = voiceChatVolume.Value;
            IngameNetManager.instance.VoiceChatKeybind = voiceChatKeybind.Value.MainKey;
            IngameNetManager.instance.PlaceMarkerKeybind = placeMarkerKeybind.Value.MainKey;
        }
        if (ChatManager.instance != null)
        {
            ChatManager.instance.GlobalChatKeybind = globalChatKeybind.Value;
            ChatManager.instance.TeamChatKeybind = teamChatKeybind.Value;
            ChatManager.instance.chatWidth = chatWidth.Value;
            ChatManager.instance.chatHeight = chatHeight.Value;
            ChatManager.instance.chatXOffset = chatXOffset.Value;
            ChatManager.instance.chatYOffset = chatYOffset.Value;
            ChatManager.instance.chatFontSize = chatFontSize.Value;
            ChatManager.instance.chatFieldHiddenDelay = chatFieldHiddenDelay.Value;
        }
        if (GameUI.instance != null) { GameUI.instance.nameTagfontSize = nameTagFontSize.Value; }
    }
}