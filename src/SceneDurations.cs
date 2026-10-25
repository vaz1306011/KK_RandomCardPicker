using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace RandomCardPicker
{
    /// <summary>
    /// Reads the Timeline plugin's duration from scene cards without loading them.
    /// Timeline stores an xml string (&lt;root duration="..."&gt;) in the card's ExtensibleSaveFormat data,
    /// which is appended after the scene itself behind a "KKEx" marker, so the file is searched backwards from its end.
    /// Results are cached in memory and in a file keyed by path, size and write time.
    /// </summary>
    internal static class SceneDurations
    {
        /// <summary>Duration of a card without any Timeline data.</summary>
        public const float NoTimeline = 0f;

        private const int ChunkSize = 256 * 1024;
        private static readonly byte[] DurationMarker = Encoding.ASCII.GetBytes("<root duration=\"");
        private static readonly byte[] ExtDataMarker = Encoding.ASCII.GetBytes("KKEx");

        private struct Entry
        {
            public long Length;
            public long WriteTicks;
            public float Duration;
        }

        private static readonly Dictionary<string, Entry> _cache = new Dictionary<string, Entry>();
        private static string _cacheFile;
        private static bool _dirty;

        public static void Load(string cacheFile)
        {
            _cacheFile = cacheFile;
            if (!File.Exists(cacheFile)) return;

            lock (_cache)
            {
                foreach (var line in File.ReadAllLines(cacheFile))
                {
                    var parts = line.Split('\t');
                    if (parts.Length != 4) continue;

                    long length, ticks;
                    float duration;
                    if (!long.TryParse(parts[1], out length) || !long.TryParse(parts[2], out ticks) ||
                        !float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out duration)) continue;

                    _cache[parts[0]] = new Entry { Length = length, WriteTicks = ticks, Duration = duration };
                }
            }
        }

        public static void Save()
        {
            if (_cacheFile == null) return;

            lock (_cache)
            {
                if (!_dirty) return;

                var sb = new StringBuilder();
                foreach (var kvp in _cache)
                    sb.Append(kvp.Key).Append('\t').Append(kvp.Value.Length).Append('\t').Append(kvp.Value.WriteTicks).Append('\t')
                      .Append(kvp.Value.Duration.ToString("R", CultureInfo.InvariantCulture)).Append('\n');

                File.WriteAllText(_cacheFile, sb.ToString());
                _dirty = false;
            }
        }

        /// <summary>
        /// Duration in seconds, 0 when the card has no Timeline data, null when unknown.
        /// Files that are not cached are only read when allowRead is set.
        /// </summary>
        public static float? Get(string path, bool allowRead, out bool didRead)
        {
            didRead = false;

            FileInfo info;
            try
            {
                info = new FileInfo(path);
                if (!info.Exists) return null;
            }
            catch (Exception)
            {
                return null;
            }

            var key = SubfolderPicker.Normalize(path);
            lock (_cache)
            {
                Entry entry;
                if (_cache.TryGetValue(key, out entry) && entry.Length == info.Length && entry.WriteTicks == info.LastWriteTimeUtc.Ticks)
                    return entry.Duration;
            }

            if (!allowRead) return null;

            didRead = true;
            float duration;
            try
            {
                duration = ReadDuration(path);
            }
            catch (Exception ex)
            {
                RandomCardPickerPlugin.Logger.LogWarning($"Failed to read duration of {path}: {ex.Message}");
                return null;
            }

            lock (_cache)
            {
                _cache[key] = new Entry { Length = info.Length, WriteTicks = info.LastWriteTimeUtc.Ticks, Duration = duration };
                _dirty = true;
            }

            return duration;
        }

        private static float ReadDuration(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                var length = stream.Length;
                // The number may start right at the end of a chunk, so read a bit past it
                var buffer = new byte[ChunkSize + 64];

                for (var start = Math.Max(0, length - ChunkSize); ; start -= ChunkSize)
                {
                    if (start < 0) start = 0;

                    stream.Position = start;
                    var read = ReadFully(stream, buffer);

                    var found = LastIndexOf(buffer, read, DurationMarker);
                    if (found >= 0) return ParseNumber(buffer, found + DurationMarker.Length, read);

                    // Timeline data comes after the marker, so a marker without a duration before it means no Timeline
                    if (LastIndexOf(buffer, read, ExtDataMarker) >= 0) return NoTimeline;

                    if (start == 0) return NoTimeline;
                }
            }
        }

        private static float ParseNumber(byte[] buffer, int start, int count)
        {
            var end = start;
            while (end < count && buffer[end] != (byte)'"') end++;

            float value;
            var text = Encoding.ASCII.GetString(buffer, start, end - start);
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) ? value : NoTimeline;
        }

        private static int ReadFully(Stream stream, byte[] buffer)
        {
            var total = 0;
            int read;
            while (total < buffer.Length && (read = stream.Read(buffer, total, buffer.Length - total)) > 0)
                total += read;
            return total;
        }

        private static int LastIndexOf(byte[] buffer, int count, byte[] pattern)
        {
            for (var i = count - pattern.Length; i >= 0; i--)
            {
                if (buffer[i] != pattern[0]) continue;

                var j = 1;
                while (j < pattern.Length && buffer[i + j] == pattern[j]) j++;
                if (j == pattern.Length) return i;
            }

            return -1;
        }

        public static string Format(float? duration)
        {
            if (duration == null) return "?";
            if (duration.Value <= NoTimeline) return "No timeline";

            var seconds = duration.Value;
            var text = seconds.ToString("0.##", CultureInfo.InvariantCulture) + "s";
            if (seconds < 60f) return text;

            var minutes = (int)(seconds / 60f);
            return string.Format(CultureInfo.InvariantCulture, "{0}:{1:00} ({2})", minutes, (int)(seconds - minutes * 60f), text);
        }
    }
}
