using System;
using Steamworks;
using UnityEngine;

namespace CloseTheDeal.Net
{
    /// <summary>
    /// Owns the Steam API for the whole run of the game: starts it on launch, pumps its
    /// callbacks every frame and shuts it down on quit. Nothing else calls SteamAPI.Init.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SteamService : MonoBehaviour
    {
        /// <summary>True once Steam is up. False if Steam is not running or the app id is missing.</summary>
        public static bool IsReady { get; private set; }

        public static CSteamID LocalId => IsReady ? SteamUser.GetSteamID() : CSteamID.Nil;

        public static string LocalName => IsReady ? SteamFriends.GetPersonaName() : "(no Steam)";

        static SteamService _instance;

        void Awake()
        {
            if (_instance != null)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            Initialise();
        }

        void Initialise()
        {
            if (!Packsize.Test())
                Debug.LogError("[Steam] Steamworks.NET was built for a different platform. Check the plugin import settings.");

            if (!DllCheck.Test())
                Debug.LogError("[Steam] steam_api64.dll is the wrong version for this Steamworks.NET.");

            // TODO(app id): once we own a Steam app id, call SteamAPI.RestartAppIfNecessary(appId)
            // here so a build launched outside Steam relaunches through it.

            try
            {
                IsReady = SteamAPI.Init();
            }
            catch (DllNotFoundException e)
            {
                Debug.LogError("[Steam] steam_api64.dll could not be loaded. " + e.Message);
                IsReady = false;
            }

            // Not an error: the lobby falls back to local test mode without Steam.
            if (!IsReady)
                Debug.LogWarning("[Steam] Steam is not available, so the lobby uses local test mode. For Steam, start Steam and sign in, and keep steam_appid.txt next to the executable (or in the project root in the editor).");
            else
                Debug.Log($"[Steam] Ready as {SteamFriends.GetPersonaName()} ({SteamUser.GetSteamID()}).");
        }

        void Update()
        {
            if (IsReady)
                SteamAPI.RunCallbacks();
        }

        void OnDestroy()
        {
            if (_instance != this)
                return;

            if (IsReady)
                SteamAPI.Shutdown();

            IsReady = false;
            _instance = null;
        }
    }
}
