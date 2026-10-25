using System;
using System.IO;
using System.Linq;
using BepInEx;

namespace RandomCardPicker
{
    /// <summary>
    /// A "History" entry next to Favorite in the BrowserFolders tree. It is a real folder under UserData/History that
    /// holds copies of the cards that were loaded / imported / changed, stamped with the time of use so the newest
    /// is first when the list is sorted by date. Only the most recent cards are kept.
    /// </summary>
    internal static class History
    {
        internal const string FolderName = "History";

        internal static string GetFolder(string kind)
        {
            return Path.Combine(Path.Combine(Path.Combine(Paths.GameRootPath, "UserData"), FolderName), kind);
        }

        /// <summary>Copies the card to the History folder (or moves it to the top if it is already there).</summary>
        internal static void Record(string kind, string file)
        {
            try
            {
                if (string.IsNullOrEmpty(file) || !File.Exists(file)) return;

                var folder = GetFolder(kind);
                Directory.CreateDirectory(folder);

                var target = Path.Combine(folder, Path.GetFileName(file));
                if (SubfolderPicker.Normalize(target) != SubfolderPicker.Normalize(file))
                    File.Copy(file, target, true);
                File.SetLastWriteTime(target, DateTime.Now);

                Trim(folder);
                SubfolderPicker.InvalidateCache();
                SceneScan.Cancel();
            }
            catch (Exception ex)
            {
                RandomCardPickerPlugin.Logger.LogError(ex);
            }
        }

        private static void Trim(string folder)
        {
            var max = RandomCardPickerPlugin.HistoryLimit.Value;
            if (max <= 0) return;

            var old = new DirectoryInfo(folder).GetFiles("*.png")
                .OrderByDescending(x => x.LastWriteTime)
                .Skip(max);
            foreach (var file in old) file.Delete();
        }
    }
}
