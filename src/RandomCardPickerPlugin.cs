using BepInEx;
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

        private void Awake()
        {
            Logger = base.Logger;

            var target = AccessTools.Method("BrowserFolders.BaseFolderBrowser:DrawBaseControlButtons");
            if (target == null)
            {
                Logger.LogError("BrowserFolders.BaseFolderBrowser.DrawBaseControlButtons not found, is the installed BrowserFolders version supported?");
                return;
            }

            new Harmony(Guid).Patch(target, postfix: new HarmonyMethod(typeof(Hooks), nameof(Hooks.DrawButtonsPostfix)));
        }

        private static class Hooks
        {
            // Parameter name must match the original method's parameter
            internal static void DrawButtonsPostfix(object instance)
            {
                var picker = GetPicker(instance);
                if (picker == null) return;

                GUILayout.Space(1);
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
            }

            private static System.Action<object> GetPicker(object instance)
            {
                switch (instance?.GetType().FullName)
                {
                    case "BrowserFolders.Studio.StudioCharaFolders": return CharaPicker.PickRandom;
                    case "BrowserFolders.Studio.SceneFolders": return ScenePicker.PickRandom;
                    default: return null;
                }
            }
        }
    }
}
