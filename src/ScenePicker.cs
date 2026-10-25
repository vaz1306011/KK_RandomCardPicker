using System.Collections.Generic;
using HarmonyLib;
using Studio;
using UnityEngine;
using UnityEngine.UI;

namespace RandomCardPicker
{
    internal static class ScenePicker
    {
        private const int ThumbnailsPerPage = 12;
        // Cards that were never read need their file opened to know the duration, don't freeze the game on a huge folder
        private const int MaxReadsPerPick = 50;
        private const string DurationLabelName = "RandomCardPicker_Duration";

        public static void PickRandom(object browser)
        {
            var scene = Object.FindObjectOfType<SceneLoadScene>();
            if (scene == null) return;

            var traverse = Traverse.Create(scene);
            var minDuration = RandomCardPickerPlugin.MinSceneDuration.Value;

            var reads = 0;
            System.Func<string, bool> accept = null;
            if (minDuration > 0f)
            {
                accept = file =>
                {
                    bool read;
                    var duration = SceneDurations.Get(file, reads < MaxReadsPerPick, out read);
                    if (read) reads++;
                    return duration != null && duration.Value >= minDuration;
                };
            }

            SubfolderPicker.Pick(browser, GetSelectedPath(traverse), file =>
            {
                // The list is rebuilt when the folder changes, so read it again
                var paths = traverse.Field("listPath").GetValue<List<string>>();
                var index = paths?.FindIndex(x => SubfolderPicker.Normalize(x) == SubfolderPicker.Normalize(file)) ?? -1;
                if (index < 0) return false;

                // Same as the user switching page and clicking the thumbnail: opens the Load / Import / Delete panel
                traverse.Method("SetPage", new[] { typeof(int) }, new object[] { index / ThumbnailsPerPage }).GetValue();
                scene.OnClickThumbnail(index % ThumbnailsPerPage);
                return true;
            }, accept);

            SceneDurations.Save();
        }

        /// <summary>Text next to the Random button: cards matching the minimum duration / all cards in the folder.</summary>
        public static string GetCountText(object browser)
        {
            var root = SubfolderPicker.GetScopeRoot(browser);
            if (root == null) return null;

            var scan = SceneScan.Request(root);
            var total = scan.Total;
            if (total < 0) return "...";

            var text = scan.CountAtLeast(RandomCardPickerPlugin.MinSceneDuration.Value) + "/" + total;
            return scan.Finished ? text : text + " ...";
        }

        /// <summary>Shows the duration of the scene selected in the load panel in the corner of its preview image.</summary>
        public static void UpdateDurationLabel(SceneLoadScene scene)
        {
            var traverse = Traverse.Create(scene);
            var image = traverse.Field("imageThumbnail").GetValue<RawImage>();
            if (image == null) return;

            var label = GetOrCreateLabel(image);

            var path = GetSelectedPath(traverse);
            if (path == null)
            {
                label.text = string.Empty;
                return;
            }

            bool read;
            label.text = SceneDurations.Format(SceneDurations.Get(path, true, out read));
            if (read) SceneDurations.Save();
        }

        private static string GetSelectedPath(Traverse traverse)
        {
            var paths = traverse.Field("listPath").GetValue<List<string>>();
            var select = traverse.Field("select").GetValue<int>();
            return paths != null && select >= 0 && select < paths.Count ? paths[select] : null;
        }

        private static Text GetOrCreateLabel(RawImage image)
        {
            var existing = image.transform.Find(DurationLabelName);
            if (existing != null) return existing.GetComponent<Text>();

            var go = new GameObject(DurationLabelName, typeof(RectTransform));
            go.transform.SetParent(image.transform, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(6f, 4f);
            rect.offsetMax = new Vector2(-6f, -4f);

            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 22;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.LowerRight;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.color = Color.white;
            text.raycastTarget = false;

            var outline = go.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            return text;
        }
    }
}
