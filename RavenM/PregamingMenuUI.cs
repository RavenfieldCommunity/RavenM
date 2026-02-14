using System;
using RavenM.DiscordGameSDK;
using Steamworks;
using UnityEngine;



namespace RavenM;

public class PregamingMenuUI : MonoBehaviour
{

    private bool hasOpenMainMenu = false;
    private bool hasEnableNomodsMode = false;
    void Awake()
    {
        DontDestroyOnLoad(this.gameObject);
    }

    void Update()
    {
        if (Plugin.instance.startupLobbyId != CSteamID.Nil && GameManager.instance != null && !hasEnableNomodsMode)
        {
            ModManager.instance.noContentMods = true;
            hasEnableNomodsMode = true;
            return;
        }
        if (Plugin.instance.startupLobbyId != CSteamID.Nil && !hasOpenMainMenu && GameManager.instance != null && ModManager.instance.contentHasFinishedLoading)
            {
                MainMenu.instance.OpenPageIndex(MainMenu.PAGE_INSTANT_ACTION);
                Plugin.logger.LogInfo("Open mainmenu before joining lobby");
                hasOpenMainMenu = true;
                return;
            }
        if (hasOpenMainMenu)
        {
            if (SteamMatchmaking.GetLobbyMemberLimit(Plugin.instance.startupLobbyId) != 0)
            {
                SteamMatchmaking.JoinLobby(Plugin.instance.startupLobbyId);
                LobbySystem.instance.InLobby = true;
                LobbySystem.instance.IsLobbyOwner = false;
                LobbySystem.instance.LobbyDataReady = false;
                Plugin.logger.LogInfo("Join lobby from args");
            }
            Destroy(this);
        }
    }
}