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
    public static ConfigEntry<KeyboardShortcut> globalChatKeybind;
    public static ConfigEntry<KeyboardShortcut> teamChatKeybind;
    public static ConfigEntry<KeyboardShortcut> placeMarkerKeybind;
    public static void Init()
    {
        var config = Plugin.config;
        debugMode = config.Bind("RavenM.Debug", "Debug mode", false, "");
        showIngameUI = config.Bind("RavenM.IngameUI", "Show ingame UI", true, "");
        nameTagFontSize = config.Bind("RavenM.IngameUI.NameTags", "Nametag font size", 12, "");
        enableNameTagCustomColor = config.Bind("RavenM.IngameUI.NameTags", "Nametag custom color", false, "");
        nameTagColorTeam = config.Bind("RavenM.IngameUI.NameTags", "Nametag team color", "#1E90FF", "");
        nameTagColorEnemy = config.Bind("RavenM.IngameUI.NameTags", "Nametag enemy color", "#FFA500", "");
        voiceChatVolume = config.Bind("RavenM.Chat", "Voice chat volume", 1f, "");
        voiceChatKeybind = config.Bind("RavenM.Chat", "Voice chat keybind", new KeyboardShortcut(KeyCode.CapsLock), "");
        globalChatKeybind = config.Bind("RavenM.Chat", "Global chat keybind", new KeyboardShortcut(KeyCode.Y), "");
        teamChatKeybind = config.Bind("RavenM.Chat", "Team chat keybind", new KeyboardShortcut(KeyCode.U), "");
        placeMarkerKeybind = config.Bind("RavenM.Chat", "Place marker keybind", new KeyboardShortcut(KeyCode.BackQuote), "");
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
        if (IngameNetManager.instance != null)
        {
            ChatManager.instance.GlobalChatKeybind = globalChatKeybind.Value.MainKey;
            ChatManager.instance.TeamChatKeybind = teamChatKeybind.Value.MainKey;
        }
        if (GameUI.instance != null) { GameUI.instance.nameTagfontSize = nameTagFontSize.Value; }
    }
}