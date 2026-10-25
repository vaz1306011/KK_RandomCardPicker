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

        private static string _scopeRoot;
        private static string _lastPickedFolder;

        // Scan results per scope root, dropped whenever BrowserFolders refreshes so both stay in sync
        private static readonly Dictionary<string, List<string>> _fileCache = new Dictionary<string, List<string>>();

        internal static void InvalidateCache()
        {
            lock (_fileCache) _fileCache.Clear();
        }

        private static List<string> GetFiles(string root)
        {
            lock (_fileCache)
            {
                if (!_fileCache.TryGetValue(root, out var files))
                    _fileCache[root] = files = Directory.GetFiles(root, "*.png", SearchOption.AllDirectories).ToList();
                return files;
            }
        }

        /// <summary>
        /// Folder the next Random press picks from: the folder selected in BrowserFolders, or while the user keeps
        /// pressing Random the one they originally selected.
        /// </summary>
        public static string GetScopeRoot(object browser)
        {
            var treeView = Traverse.Create(browser).Property("TreeView").GetValue();
            if (treeView == null) return null;

            var current = Normalize(Traverse.Create(treeView).Property("CurrentFolder").GetValue<string>());
            // Pressing Random again keeps the original scope, only a folder change by the user starts a new one
            if (current != _lastPickedFolder || !Directory.Exists(_scopeRoot)) _scopeRoot = current;
            return _scopeRoot;
        }

        /// <param name="browser">The BrowserFolders browser instance that owns the pressed button.</param>
        /// <param name="selectedFile">Card currently selected in the game list, never picked again.</param>
        /// <param name="tryShow">Selects the file in the (refreshed) game list, returns false if it is not in it.</param>
        /// <param name="accept">Optional filter, files it rejects are skipped without counting as a try.</param>
        public static void Pick(object browser, string selectedFile, Func<string, bool> tryShow, Func<string, bool> accept = null)
        {
            var scopeRoot = GetScopeRoot(browser);
            if (scopeRoot == null) return;

            var folder = Traverse.Create(Traverse.Create(browser).Property("TreeView").GetValue()).Property("CurrentFolder");

            var selected = string.IsNullOrEmpty(selectedFile) ? null : Normalize(selectedFile);
            // Copy so tried files can be dropped without touching the cache
            var candidates = GetFiles(scopeRoot).ToList();

            var tries = 0;
            while (tries < MaxTries && candidates.Count > 0)
            {
                var i = UnityEngine.Random.Range(0, candidates.Count);
                var file = candidates[i];
                candidates.RemoveAt(i);

                if (selected != null && Normalize(file) == selected) continue;
                if (accept != null && !accept(file)) continue;
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

        internal static string Normalize(string path)
        {
            return Path.GetFullPath(path).Replace('/', '\\').TrimEnd('\\').ToLowerInvariant();
        }
    }
}
