using System;
using System.IO;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace RandomCardPicker
{
    /// <summary>
    /// Picks a random card from the selected BrowserFolders folder and all of its subfolders.
    /// The game lists only show one folder at a time, so when the pick is in another folder the
    /// BrowserFolders selection is switched to it, same as the user clicking that folder.
    /// </summary>
    internal static class SubfolderPicker
    {
        private const int MaxTries = 30;

        private static string _scopeRoot;
        private static string _lastPickedFolder;

        /// <param name="browser">The BrowserFolders browser instance that owns the pressed button.</param>
        /// <param name="selectedFile">Card currently selected in the game list, never picked again.</param>
        /// <param name="tryShow">Selects the file in the (refreshed) game list, returns false if it is not in it.</param>
        public static void Pick(object browser, string selectedFile, Func<string, bool> tryShow)
        {
            var treeView = Traverse.Create(browser).Property("TreeView").GetValue();
            if (treeView == null) return;

            var folder = Traverse.Create(treeView).Property("CurrentFolder");
            var current = Normalize(folder.GetValue<string>());

            // Pressing Random again keeps the original scope, only a folder change by the user starts a new one
            if (current != _lastPickedFolder || !Directory.Exists(_scopeRoot)) _scopeRoot = current;

            var selected = string.IsNullOrEmpty(selectedFile) ? null : Normalize(selectedFile);
            var candidates = Directory.GetFiles(_scopeRoot, "*.png", SearchOption.AllDirectories)
                                      .Where(x => Normalize(x) != selected)
                                      .ToList();

            for (var tries = 0; tries < MaxTries && candidates.Count > 0; tries++)
            {
                var i = UnityEngine.Random.Range(0, candidates.Count);
                var file = candidates[i];
                candidates.RemoveAt(i);

                var dir = Normalize(Path.GetDirectoryName(file));
                if (dir != Normalize(folder.GetValue<string>())) folder.SetValue(dir);

                // Not every png in a folder is a valid card, so the list may not contain it
                if (tryShow(file))
                {
                    _lastPickedFolder = dir;
                    return;
                }
            }
        }

        internal static string Normalize(string path)
        {
            return Path.GetFullPath(path).Replace('/', '\\').TrimEnd('\\').ToLowerInvariant();
        }
    }
}
