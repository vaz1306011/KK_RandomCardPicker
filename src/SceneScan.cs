using System;
using System.IO;
using System.Threading;

namespace RandomCardPicker
{
    /// <summary>
    /// Background read of the Timeline duration of every scene card under a folder, so the number of cards
    /// matching the minimum duration can be shown without freezing the game.
    /// </summary>
    internal sealed class SceneScan
    {
        private static SceneScan _current;

        private volatile bool _cancelled;
        private volatile float[] _durations;

        // Counting every frame is wasteful, the numbers only need to look live
        private float _countedMin = float.NaN;
        private int _countedDone = -1;
        private float _countedAt;
        private int _matching;

        public string Root { get; private set; }
        public volatile bool Finished;
        public volatile int Done;

        /// <summary>Number of scene cards, -1 until the folder has been listed.</summary>
        public int Total { get { var d = _durations; return d == null ? -1 : d.Length; } }

        /// <summary>Starts a scan of the folder unless one for it is already running or done.</summary>
        public static SceneScan Request(string root)
        {
            var scan = _current;
            if (scan != null && scan.Root == root && !scan._cancelled) return scan;

            Cancel();
            scan = _current = new SceneScan { Root = root };
            var thread = new Thread(scan.Run) { IsBackground = true, Name = "RandomCardPicker scene scan" };
            thread.Priority = ThreadPriority.BelowNormal;
            thread.Start();
            return scan;
        }

        /// <summary>Drops the current scan, called when BrowserFolders refreshes.</summary>
        public static void Cancel()
        {
            var scan = _current;
            if (scan != null) scan._cancelled = true;
            _current = null;
        }

        private void Run()
        {
            try
            {
                var files = Directory.GetFiles(Root, "*.png", SearchOption.AllDirectories);
                var durations = new float[files.Length];
                for (var i = 0; i < durations.Length; i++) durations[i] = float.NaN;
                _durations = durations;

                for (var i = 0; i < files.Length && !_cancelled; i++)
                {
                    bool read;
                    var duration = SceneDurations.Get(files[i], true, out read);
                    if (duration != null) durations[i] = duration.Value;
                    Done = i + 1;
                }

                if (!_cancelled) SceneDurations.Save();
            }
            catch (Exception ex)
            {
                RandomCardPickerPlugin.Logger.LogWarning("Scene duration scan failed: " + ex.Message);
            }
            finally
            {
                Finished = true;
            }
        }

        /// <summary>Number of cards known to last at least the given seconds.</summary>
        public int CountAtLeast(float minDuration)
        {
            var durations = _durations;
            if (durations == null) return 0;

            var now = UnityEngine.Time.realtimeSinceStartup;
            if (_countedMin == minDuration && (Finished && _countedDone == Done || now - _countedAt < 0.3f)) return _matching;

            var count = 0;
            for (var i = 0; i < durations.Length; i++)
            {
                // NaN (not read yet) compares false
                if (durations[i] >= minDuration) count++;
            }

            _countedMin = minDuration;
            _countedDone = Done;
            _countedAt = now;
            return _matching = count;
        }
    }
}
