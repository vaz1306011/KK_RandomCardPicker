using System.IO;
using System.Linq;
using BepInEx;
using HarmonyLib;
using Studio;
using UnityEngine;

namespace RandomCardPicker
{
    /// <summary>
    /// A "Favorite" entry above the top folder of the BrowserFolders tree (same level as scene / female / male).
    /// It is a real folder under UserData/Favorite that holds copies of the favorited cards, so the game lists it
    /// like any other folder and Random works inside it.
    /// </summary>
    internal static class Favorites
    {
        private const string FolderName = "Favorite";
        private const float FeedbackSeconds = 1.5f;

        private static string _feedback;
        private static float _feedbackUntil;

        /// <summary>Which card type a tree / browser is for, null for trees that don't get a Favorite entry.</summary>
        private static string KindOfRoot(string defaultPath)
        {
            var path = SubfolderPicker.Normalize(defaultPath).Replace('\\', '/');
            if (path.EndsWith("/studio/scene")) return "scene";
            if (path.EndsWith("/chara/female")) return "female";
            if (path.EndsWith("/chara/male")) return "male";
            return null;
        }

        private static string GetFolder(string kind)
        {
            return Path.Combine(Path.Combine(Path.Combine(Paths.GameRootPath, "UserData"), FolderName), kind);
        }

        /// <summary>Called before BrowserFolders draws a tree node, draws the Favorite row above the top folder.</summary>
        internal static void DrawTreeRowPrefix(object __instance, int indent)
        {
            if (indent != 0) return;

            var traverse = Traverse.Create(__instance);
            var kind = KindOfRoot(traverse.Property("DefaultPath").GetValue<string>());
            if (kind == null) return;

            var folder = GetFolder(kind);
            var current = traverse.Property("CurrentFolder");
            var selected = SubfolderPicker.Normalize(current.GetValue<string>() ?? string.Empty) == SubfolderPicker.Normalize(folder);

            var color = GUI.color;
            if (selected) GUI.color = Color.cyan;
            GUILayout.BeginHorizontal();
            GUILayout.Space(20f);
            if (GUILayout.Button(FolderName, GUI.skin.label, GUILayout.ExpandWidth(true), GUILayout.MinWidth(100f)))
            {
                Directory.CreateDirectory(folder);
                current.SetValue(folder);
            }
            GUILayout.EndHorizontal();
            GUI.color = color;
        }

        /// <summary>Button next to Random, copies the selected card into the Favorite folder.</summary>
        internal static void DrawButton(object browser)
        {
            var label = Time.realtimeSinceStartup < _feedbackUntil ? _feedback : "Fav";
            if (!GUILayout.Button(label, GUILayout.ExpandWidth(false))) return;

            try
            {
                _feedback = AddSelected(browser);
                _feedbackUntil = Time.realtimeSinceStartup + FeedbackSeconds;
            }
            catch (System.Exception ex)
            {
                RandomCardPickerPlugin.Logger.LogError(ex);
            }
        }

        private static string AddSelected(object browser)
        {
            string kind, file;
            if (browser.GetType().FullName == "BrowserFolders.Studio.SceneFolders")
            {
                var scene = Object.FindObjectOfType<SceneLoadScene>();
                kind = "scene";
                file = scene == null ? null : ScenePicker.GetSelectedPath(Traverse.Create(scene));
            }
            else
            {
                var list = Object.FindObjectsOfType<CharaList>().FirstOrDefault(x => x.isActiveAndEnabled && x.isInit);
                if (list == null) return "None";
                kind = Traverse.Create(list).Field("sex").GetValue<int>() == 0 ? "male" : "female";
                file = Traverse.Create(list).Field("charaFileSort").GetValue<CharaFileSort>().selectPath;
            }

            if (string.IsNullOrEmpty(file) || !File.Exists(file)) return "None";

            var folder = GetFolder(kind);
            if (SubfolderPicker.Normalize(Path.GetDirectoryName(file)) == SubfolderPicker.Normalize(folder)) return "Added";

            Directory.CreateDirectory(folder);
            var target = Path.Combine(folder, Path.GetFileName(file));
            if (File.Exists(target)) return "Added";

            File.Copy(file, target);
            SubfolderPicker.InvalidateCache();
            SceneScan.Cancel();
            return "Added";
        }
    }
}
