using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;

namespace RandomCardPicker
{
    /// <summary>
    /// A "Most Used" entry below History in the BrowserFolders tree. It is a real folder under UserData/MostUsed that
    /// holds copies of the cards used most often. The write time of each copy is set from its rank, so the list sorted
    /// by date shows the most used card first. Use counts are kept by file name in the BepInEx cache.
    /// </summary>
    internal static class MostUsed
    {
        internal const string FolderName = "MostUsed";
        internal const string Label = "Most Used";

        private class Entry
        {
            public int Count;
            public long LastUsed;
        }

        // kind -> file name -> usage
        private static readonly Dictionary<string, Dictionary<string, Entry>> _usage = new Dictionary<string, Dictionary<string, Entry>>();
        private static bool _loaded;

        private static string CacheFile
        {
            get { return Path.Combine(Paths.CachePath, "KK_RandomCardPicker.usage.tsv"); }
        }

        internal static string GetFolder(string kind)
        {
            return Path.Combine(Path.Combine(Path.Combine(Paths.GameRootPath, "UserData"), FolderName), kind);
        }

        /// <summary>Counts one more use of the card and updates the Most Used folder.</summary>
        internal static void Record(string kind, string file)
        {
            try
            {
                if (string.IsNullOrEmpty(file) || !File.Exists(file)) return;

                Load();
                if (!_usage.TryGetValue(kind, out var entries))
                    _usage[kind] = entries = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);

                var name = Path.GetFileName(file);
                if (!entries.TryGetValue(name, out var entry)) entries[name] = entry = new Entry();
                entry.Count++;
                entry.LastUsed = DateTime.Now.Ticks;

                Save();
                Rebuild(kind, entries, name, file);
            }
            catch (Exception ex)
            {
                RandomCardPickerPlugin.Logger.LogError(ex);
            }
        }

        /// <param name="usedName">Card just used, the only one that can newly enter the ranking, so its file is the one to copy.</param>
        private static void Rebuild(string kind, Dictionary<string, Entry> entries, string usedName, string usedFile)
        {
            var limit = RandomCardPickerPlugin.MostUsedLimit.Value;
            var ranking = entries.OrderByDescending(x => x.Value.Count).ThenByDescending(x => x.Value.LastUsed).Select(x => x.Key).ToList();
            if (limit > 0 && ranking.Count > limit) ranking.RemoveRange(limit, ranking.Count - limit);

            var folder = GetFolder(kind);
            Directory.CreateDirectory(folder);

            foreach (var file in new DirectoryInfo(folder).GetFiles("*.png"))
            {
                if (!ranking.Contains(file.Name, StringComparer.OrdinalIgnoreCase)) file.Delete();
            }

            // Older write time for lower ranks, far in the past so it never mixes up with real dates
            var top = new DateTime(2000, 1, 1);
            for (var i = 0; i < ranking.Count; i++)
            {
                var target = Path.Combine(folder, ranking[i]);
                if (!File.Exists(target))
                {
                    if (!string.Equals(ranking[i], usedName, StringComparison.OrdinalIgnoreCase)) continue;
                    File.Copy(usedFile, target);
                }
                File.SetLastWriteTime(target, top.AddMinutes(ranking.Count - i));
            }

            SubfolderPicker.InvalidateCache();
            SceneScan.Cancel();
        }

        private static void Load()
        {
            if (_loaded) return;
            _loaded = true;

            if (!File.Exists(CacheFile)) return;
            foreach (var line in File.ReadAllLines(CacheFile))
            {
                var parts = line.Split('\t');
                if (parts.Length != 4 || !int.TryParse(parts[2], out var count) || !long.TryParse(parts[3], out var ticks)) continue;

                if (!_usage.TryGetValue(parts[0], out var entries))
                    _usage[parts[0]] = entries = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
                entries[parts[1]] = new Entry { Count = count, LastUsed = ticks };
            }
        }

        private static void Save()
        {
            var lines = _usage.SelectMany(kind => kind.Value.Select(x => kind.Key + "\t" + x.Key + "\t" + x.Value.Count + "\t" + x.Value.LastUsed));
            Directory.CreateDirectory(Path.GetDirectoryName(CacheFile));
            File.WriteAllLines(CacheFile, lines.ToArray());
        }
    }
}
