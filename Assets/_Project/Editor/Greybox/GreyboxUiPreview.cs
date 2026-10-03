using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CloseTheDeal.Editor.Greybox
{
    /// <summary>
    /// Renders the game menu and HUD to PNGs at 1920×1080 in three states (start, hosting, in
    /// game) so the layout can be checked without entering Play Mode. Changes are made to the
    /// open scene in memory only and never saved. Shell use (with graphics, so not -nographics):
    ///   Unity.exe -batchmode -quit -projectPath &lt;repo&gt; -executeMethod CloseTheDeal.Editor.Greybox.GreyboxUiPreview.CaptureFromCommandLine -previewOut &lt;folder&gt;
    /// </summary>
    public static class GreyboxUiPreview
    {
        const string ScenePath = "Assets/_Project/Scenes/Greybox.unity";
        const int Width = 1920;
        const int Height = 1080;

        [MenuItem("Close the Deal/Greybox/Capture UI Preview")]
        public static void CaptureFromMenu()
        {
            string folder = Path.Combine(Path.GetTempPath(), "CloseTheDealUiPreview");
            Capture(folder);
            EditorUtility.RevealInFinder(folder);
        }

        public static void CaptureFromCommandLine()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            string folder = Path.Combine(Path.GetTempPath(), "CloseTheDealUiPreview");
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-previewOut")
                    folder = args[i + 1];
            }

            Capture(folder);
        }

        static void Capture(string folder)
        {
            Directory.CreateDirectory(folder);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject ui = GameObject.Find("GameUI");
            if (ui == null)
            {
                Debug.LogError("[UI Preview] No GameUI in the scene.");
                return;
            }

            var canvas = ui.GetComponent<Canvas>();
            var camGo = new GameObject("PreviewCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.62f, 0.65f, 0.68f); // Roughly a greybox wall, the hardest backdrop for the HUD.
            var rt = new RenderTexture(Width, Height, 24);
            cam.targetTexture = rt;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 1f;

            Transform root = ui.transform;
            ShowStart(root);
            Render(root, cam, rt, Path.Combine(folder, "1_menu_start.png"));
            ShowHosting(root);
            Render(root, cam, rt, Path.Combine(folder, "2_menu_hosting.png"));
            ShowLocal(root);
            Render(root, cam, rt, Path.Combine(folder, "3_menu_local.png"));
            ShowHud(root);
            Render(root, cam, rt, Path.Combine(folder, "4_hud.png"));

            Object.DestroyImmediate(camGo);
            Debug.Log("[UI Preview] Wrote previews to " + folder);
        }

        // ---- States (mirrors what GameMenu.Refresh shows at runtime) ------------------------

        static void ShowStart(Transform root)
        {
            Set(root, "Menu", true);
            Set(root, "Hud", false);
            Set(root, "Menu/Card/StartSection", true);
            Set(root, "Menu/Card/GameSection", false);
            Set(root, "Menu/Card/StartSection/JoinCodeGroup", true);
            Set(root, "Menu/Card/StartSection/JoinLocalButton", false);
            Set(root, "Menu/Card/StartSection/JoinCodeGroup/JoinError", true);
            Text(root, "Menu/Card/Mode", "Steam · signed in as Adam");
            Text(root, "Menu/Card/Status", "Ready. Host a game, or paste a lobby code to join one.");
            Text(root, "Menu/Card/StartSection/JoinCodeGroup/JoinError", "That isn't a lobby code. It's a long number the host copies from their menu.");
        }

        static void ShowHosting(Transform root)
        {
            Set(root, "Menu", true);
            Set(root, "Hud", false);
            Set(root, "Menu/Card/StartSection", false);
            Set(root, "Menu/Card/GameSection", true);
            Set(root, "Menu/Card/GameSection/CodeGroup", true);
            Set(root, "Menu/Card/GameSection/InviteButton", false);
            Set(root, "Menu/Card/GameSection/ResumeButton", true);
            Text(root, "Menu/Card/Status", "Hosting. Copy the lobby code and send it to your friend.");
            Text(root, "Menu/Card/GameSection/CodeGroup/CodeRow/CodeBox/CodeText", "109775241736487234");
            Text(root, "Menu/Card/GameSection/Players", "Players  1 / 2");
        }

        static void ShowLocal(Transform root)
        {
            ShowStart(root);
            Set(root, "Menu/Card/StartSection/JoinCodeGroup", false);
            Set(root, "Menu/Card/StartSection/JoinLocalButton", true);
            Text(root, "Menu/Card/Mode", "Local test mode · Steam isn't running");
            Text(root, "Menu/Card/Status", "Ready. Host a game, or join one running on this PC.");
        }

        static void ShowHud(Transform root)
        {
            Set(root, "Menu", false);
            Set(root, "Hud", true);
            Text(root, "Hud/NetworkReadout/Text", "Grounded  ·  6.0 m/s  ·  ping 38 ms\nHost corrections 4  ·  last 0.03 m");
        }

        // ---- Helpers -------------------------------------------------------------------------

        static void Render(Transform root, Camera cam, RenderTexture rt, string path)
        {
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
                text.ForceMeshUpdate(true);
            Canvas.ForceUpdateCanvases();
            Transform card = root.Find("Menu/Card");
            if (card != null)
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)card);
            Canvas.ForceUpdateCanvases();

            cam.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            image.Apply();
            RenderTexture.active = previous;

            File.WriteAllBytes(path, image.EncodeToPNG());
            Object.DestroyImmediate(image);
        }

        static void Set(Transform root, string path, bool active)
        {
            Transform t = root.Find(path);
            if (t == null)
                Debug.LogWarning("[UI Preview] Missing " + path);
            else
                t.gameObject.SetActive(active);
        }

        static void Text(Transform root, string path, string value)
        {
            Transform t = root.Find(path);
            TMP_Text text = t != null ? t.GetComponent<TMP_Text>() : null;
            if (text == null)
                Debug.LogWarning("[UI Preview] Missing text " + path);
            else
                text.text = value;
        }
    }
}
