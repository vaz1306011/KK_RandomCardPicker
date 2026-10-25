using System;
using System.Collections.Generic;
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

        private static readonly Dictionary<string, List<string>> _FileCache = new Dictionary<string, List<string>>();

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
            var candidates = new List<string>(GetFiles(_scopeRoot));

            for (var tries = 0; tries < MaxTries && candidates.Count > 0;)
            {
                var i = UnityEngine.Random.Range(0, candidates.Count);
                var file = candidates[i];
                candidates.RemoveAt(i);

                if (selected != null && Normalize(file) == selected) continue;
                tries++;

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

        /// <summary>
        /// Forget scanned files, called when BrowserFolders refreshes (Refresh button or file changes).
        /// </summary>
        internal static void ClearCache()
        {
            _FileCache.Clear();
        }

        private static List<string> GetFiles(string root)
        {
            if (!_FileCache.TryGetValue(root, out var files))
                _FileCache[root] = files = Directory.GetFiles(root, "*.png", SearchOption.AllDirectories).ToList();
            return files;
        }

        internal static string Normalize(string path)
        {
            return Path.GetFullPath(path).Replace('/', '\\').TrimEnd('\\').ToLowerInvariant();
        }
    }
}
