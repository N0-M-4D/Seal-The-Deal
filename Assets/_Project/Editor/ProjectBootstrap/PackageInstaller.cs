using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace CloseTheDeal.Editor.ProjectBootstrap
{
    /// <summary>
    /// One-shot, headless package installer. Adds the pinned netcode stack through the
    /// Package Manager API so UPM resolves dependencies itself.
    /// Run from the shell with:
    ///   Unity.exe -batchmode -projectPath <repo> -executeMethod CloseTheDeal.Editor.ProjectBootstrap.PackageInstaller.Install
    /// Do not pass -quit: the request completes on later editor ticks and this script exits itself.
    /// </summary>
    public static class PackageInstaller
    {
        static readonly string[] PackagesToAdd =
        {
            // FishNet 4.7.3 (2 Sep 2026), installed from its repo's package folder.
            "https://github.com/FirstGearGames/FishNet.git?path=Assets/FishNet#4.7.3",
            // Steamworks.NET 2025.164.1 (2 Aug 2026): Valve's Steam API for C#.
            "https://github.com/rlabrecque/Steamworks.NET.git?path=/com.rlabrecque.steamworks.net#2025.164.1",
        };

        // FishySteamworks is not a Package Manager package: it ships no assembly definition, so
        // Unity never compiles it from Packages/. It is imported from its release .unitypackage
        // into Assets/FishNet/Plugins/FishySteamworks instead. This removes the dead entry.
        static readonly string[] PackagesToRemove =
        {
            "com.firstgeargames.fishysteamworks",
        };

        const double TimeoutSeconds = 600;

        static AddAndRemoveRequest _request;
        static double _deadline;

        public static void Install()
        {
            Debug.Log($"[PackageInstaller] Adding: {string.Join(", ", PackagesToAdd)}");
            _request = Client.AddAndRemove(PackagesToAdd, PackagesToRemove);
            _deadline = EditorApplication.timeSinceStartup + TimeoutSeconds;
            EditorApplication.update += Poll;
        }

        static void Poll()
        {
            if (_request == null) return;

            if (!_request.IsCompleted)
            {
                if (EditorApplication.timeSinceStartup > _deadline)
                {
                    EditorApplication.update -= Poll;
                    Debug.LogError("[PackageInstaller] Timed out waiting for UPM.");
                    EditorApplication.Exit(2);
                }
                return;
            }

            EditorApplication.update -= Poll;

            if (_request.Status == StatusCode.Success)
            {
                var names = _request.Result.Select(p => $"{p.name}@{p.version}");
                Debug.Log($"[PackageInstaller] Resolved: {string.Join(", ", names)}");
                EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError($"[PackageInstaller] Failed: {_request.Error?.message}");
                EditorApplication.Exit(1);
            }
        }
    }
}
