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
        private const int MaxHistory = 100;

        // Files picked by Random per list, the last one is the card Random put on screen
        private static readonly Dictionary<string, List<string>> _history = new Dictionary<string, List<string>>();

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
        /// <param name="historyKey">Which list the pick belongs to, <see cref="Back"/> only goes through picks with the same key.</param>
        /// <param name="selectedFile">Card currently selected in the game list, never picked again.</param>
        /// <param name="tryShow">Selects the file in the (refreshed) game list, returns false if it is not in it.</param>
        /// <param name="accept">Optional filter, files it rejects are skipped without counting as a try.</param>
        public static void Pick(object browser, string historyKey, string selectedFile, Func<string, bool> tryShow, Func<string, bool> accept = null)
        {
            var scopeRoot = GetScopeRoot(browser);
            if (scopeRoot == null) return;

            var folder = GetFolderProperty(browser);

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
                    Remember(historyKey, file);
                    ScrollTreeToFolder(browser);
                    return;
                }
            }
        }

        /// <summary>Shows the pick before the current one again, dropping the current one from the history.</summary>
        public static void Back(object browser, string historyKey, Func<string, bool> tryShow)
        {
            List<string> picks;
            if (!_history.TryGetValue(historyKey, out picks)) return;

            var folder = GetFolderProperty(browser);
            // The last entry is the card on screen, the first one has nothing before it
            while (picks.Count > 1)
            {
                picks.RemoveAt(picks.Count - 1);
                var file = picks[picks.Count - 1];
                if (!File.Exists(file)) continue;

                var dir = Normalize(Path.GetDirectoryName(file));
                if (dir != Normalize(folder.GetValue<string>())) folder.SetValue(dir);

                if (tryShow(file))
                {
                    _lastPickedFolder = dir;
                    ScrollTreeToFolder(browser);
                    return;
                }
            }
        }

        private static void Remember(string historyKey, string file)
        {
            List<string> picks;
            if (!_history.TryGetValue(historyKey, out picks)) _history[historyKey] = picks = new List<string>();

            picks.Add(file);
            if (picks.Count > MaxHistory) picks.RemoveAt(0);
        }

        /// <summary>The tree expands to the current folder on its own, this also scrolls it into view.</summary>
        private static void ScrollTreeToFolder(object browser)
        {
            var treeView = Traverse.Create(browser).Property("TreeView");
            var tree = treeView.GetValue();
            var current = treeView.Property("CurrentFolder").GetValue<string>();
            var root = treeView.Property("DefaultPath").GetValue<string>();
            // Favorite / History / Most Used are drawn as separate rows, there is no tree node to scroll to
            if (tree == null || string.IsNullOrEmpty(current) || string.IsNullOrEmpty(root) || !Normalize(current).StartsWith(Normalize(root))) return;

            Traverse.Create(tree).Method("ScrollListToSelected").GetValue();
        }

        private static Traverse GetFolderProperty(object browser)
        {
            return Traverse.Create(Traverse.Create(browser).Property("TreeView").GetValue()).Property("CurrentFolder");
        }

        internal static string Normalize(string path)
        {
            return Path.GetFullPath(path).Replace('/', '\\').TrimEnd('\\').ToLowerInvariant();
        }
    }
}
