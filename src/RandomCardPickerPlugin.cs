using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace RandomCardPicker
{
    /// <summary>
    /// Adds a "Random" button to the BrowserFolders window in CharaStudio's character and scene lists.
    /// The button selects a random card from the list currently shown (i.e. the folder selected in BrowserFolders)
    /// but does not apply it, the user still presses Load / Import / Change themselves.
    /// </summary>
    [BepInPlugin(Guid, "Random Card Picker", Version)]
    [BepInDependency(BrowserFoldersGuid)]
    [BepInProcess("CharaStudio")]
    public class RandomCardPickerPlugin : BaseUnityPlugin
    {
        public const string Guid = "vaz1306011.KK_RandomCardPicker";
        public const string Version = "0.1.0";
        private const string BrowserFoldersGuid = "marco.FolderBrowser";

        internal static new ManualLogSource Logger { get; private set; }
        internal static ConfigEntry<float> MinSceneDuration { get; private set; }

        private void Awake()
        {
            Logger = base.Logger;

            MinSceneDuration = Config.Bind("Scene", "Minimum duration (seconds)", 0f,
                new ConfigDescription("Random only picks scene cards whose Timeline lasts at least this long. Cards without Timeline count as 0. Set to 0 to pick any scene.",
                                      new AcceptableValueRange<float>(0f, 3600f)));

            SceneDurations.Load(System.IO.Path.Combine(Paths.CachePath, "KK_RandomCardPicker.durations.tsv"));

            var target = AccessTools.Method("BrowserFolders.BaseFolderBrowser:DrawBaseControlButtons");
            if (target == null)
            {
                Logger.LogError("BrowserFolders.BaseFolderBrowser.DrawBaseControlButtons not found, is the installed BrowserFolders version supported?");
                return;
            }

            var harmony = new Harmony(Guid);
            harmony.Patch(target, postfix: new HarmonyMethod(typeof(Hooks), nameof(Hooks.DrawButtonsPostfix)));

            // Called by the Refresh thumbnails button and when BrowserFolders notices folder changes
            harmony.PatchAll(typeof(SceneHooks));

            var refresh = AccessTools.Method("BrowserFolders.FolderTreeView:ResetTreeCache");
            if (refresh != null)
                harmony.Patch(refresh, postfix: new HarmonyMethod(typeof(Hooks), nameof(Hooks.RefreshPostfix)));
            else
                Logger.LogWarning("BrowserFolders.FolderTreeView.ResetTreeCache not found, the card list will not refresh with BrowserFolders");
        }

        private void OnApplicationQuit()
        {
            SceneDurations.Save();
        }

        private static class SceneHooks
        {
            // Runs for both a normal click and the Random button, as it calls the same method
            [HarmonyPostfix]
            [HarmonyPatch(typeof(Studio.SceneLoadScene), nameof(Studio.SceneLoadScene.OnClickThumbnail))]
            internal static void OnClickThumbnailPostfix(Studio.SceneLoadScene __instance)
            {
                try
                {
                    ScenePicker.UpdateDurationLabel(__instance);
                }
                catch (System.Exception ex)
                {
                    Logger.LogError(ex);
                }
            }
        }

        private static class Hooks
        {
            // Parameter name must match the original method's parameter
            internal static void DrawButtonsPostfix(object instance)
            {
                var picker = GetPicker(instance);
                if (picker == null) return;

                GUILayout.Space(1);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Random"))
                {
                    try
                    {
                        picker(instance);
                    }
                    catch (System.Exception ex)
                    {
                        Logger.LogError(ex);
                    }
                }

                if (instance.GetType().FullName == SceneFoldersType)
                    GUILayout.Label(GetCountText(instance), GUILayout.ExpandWidth(false));
                GUILayout.EndHorizontal();
            }

            internal static void RefreshPostfix()
            {
                SubfolderPicker.InvalidateCache();
                SceneScan.Cancel();
            }

            private const string SceneFoldersType = "BrowserFolders.Studio.SceneFolders";

            private static string GetCountText(object instance)
            {
                try
                {
                    return ScenePicker.GetCountText(instance);
                }
                catch (System.Exception ex)
                {
                    Logger.LogError(ex);
                    return string.Empty;
                }
            }

            private static System.Action<object> GetPicker(object instance)
            {
                switch (instance?.GetType().FullName)
                {
                    case "BrowserFolders.Studio.StudioCharaFolders": return CharaPicker.PickRandom;
                    case SceneFoldersType: return ScenePicker.PickRandom;
                    default: return null;
                }
            }
        }
    }
}
