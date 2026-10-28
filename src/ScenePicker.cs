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

        private const string HistoryKey = "scene";

        public static void PickRandom(object browser, bool back)
        {
            var scene = Object.FindObjectOfType<SceneLoadScene>();
            if (scene == null) return;

            var traverse = Traverse.Create(scene);

            System.Func<string, bool> tryShow = file =>
            {
                // The list is rebuilt when the folder changes, so read it again
                var paths = traverse.Field("listPath").GetValue<List<string>>();
                var index = paths?.FindIndex(x => SubfolderPicker.Normalize(x) == SubfolderPicker.Normalize(file)) ?? -1;
                if (index < 0) return false;

                // Same as the user switching page and clicking the thumbnail: opens the Load / Import / Delete panel
                traverse.Method("SetPage", new[] { typeof(int) }, new object[] { index / ThumbnailsPerPage }).GetValue();
                scene.OnClickThumbnail(index % ThumbnailsPerPage);
                return true;
            };

            if (back)
            {
                SubfolderPicker.Back(browser, HistoryKey, tryShow);
                return;
            }

            var minDuration = GetMinDuration();

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

            SubfolderPicker.Pick(browser, HistoryKey, GetSelectedPath(traverse), tryShow, accept);

            SceneDurations.Save();
        }

        /// <summary>Cards shorter than the list minimum are not in the list, so they can't be picked either.</summary>
        private static float GetMinDuration()
        {
            return Mathf.Max(RandomCardPickerPlugin.RandomMinDuration.Value, RandomCardPickerPlugin.ListMinDuration.Value);
        }

        /// <summary>Text next to the Random button: cards matching the minimum duration / all cards in the folder.</summary>
        public static string GetCountText(object browser)
        {
            var root = SubfolderPicker.GetScopeRoot(browser);
            if (root == null) return null;

            var scan = SceneScan.Request(root);
            var total = scan.Total;
            if (total < 0) return "...";

            var text = scan.CountAtLeast(GetMinDuration()) + "/" + total;
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

        public static void UpdateStar(SceneLoadScene scene)
        {
            var traverse = Traverse.Create(scene);
            Favorites.UpdateStar(traverse.Field("imageThumbnail").GetValue<RawImage>(), "scene", () => GetSelectedPath(traverse));
        }

        internal static string GetSelectedPath(Traverse traverse)
        {
            var paths = traverse.Field("listPath").GetValue<List<string>>();
            var select = traverse.Field("select").GetValue<int>();
            return paths != null && select >= 0 && select < paths.Count ? paths[select] : null;
        }

        /// <summary>Drops cards shorter than the list minimum from the load list, then rebuilds its page buttons.</summary>
        public static void FilterList(SceneLoadScene scene)
        {
            var minDuration = RandomCardPickerPlugin.ListMinDuration.Value;
            if (minDuration <= 0f) return;

            var traverse = Traverse.Create(scene);
            var paths = traverse.Field("listPath").GetValue<List<string>>();
            if (paths == null) return;

            // Cards that were never read have to be opened once, the result is cached after that
            paths.RemoveAll(x =>
            {
                bool read;
                var duration = SceneDurations.Get(x, true, out read);
                return duration == null || duration.Value < minDuration;
            });
            SceneDurations.Save();

            var count = paths.Count;
            var pageCount = count / ThumbnailsPerPage + (count % ThumbnailsPerPage != 0 ? 1 : 0);
            traverse.Field("thumbnailNum").SetValue(count);
            traverse.Field("pageNum").SetValue(pageCount);

            // Same as the end of the game's InitInfo, which built them for the unfiltered list
            var root = traverse.Field("transformRoot").GetValue<Transform>();
            for (var i = 0; i < root.childCount; i++) Object.Destroy(root.GetChild(i).gameObject);
            root.DetachChildren();

            var prefab = traverse.Field("prefabButton").GetValue<GameObject>();
            var pages = traverse.Field("dicPage").GetValue<Dictionary<int, StudioNode>>();
            pages.Clear();
            for (var n = 0; n < pageCount; n++)
            {
                var go = Object.Instantiate(prefab);
                go.transform.SetParent(root, false);

                var node = go.GetComponent<StudioNode>();
                node.active = true;
                var pageIndex = n;
                node.addOnClick = () => traverse.Method("OnClickPage", new[] { typeof(int) }, new object[] { pageIndex }).GetValue();
                node.text = (n + 1).ToString();
                pages.Add(n, node);
            }
        }

        /// <summary>Shows the duration in the corner of every thumbnail on the current page.</summary>
        public static void UpdateThumbnailLabels(SceneLoadScene scene)
        {
            var traverse = Traverse.Create(scene);
            var thumbnails = traverse.Field("buttonThumbnail").GetValue<ThumbnailNode[]>();
            var paths = traverse.Field("listPath").GetValue<List<string>>();
            if (thumbnails == null || paths == null) return;

            var read = false;
            for (var i = 0; i < thumbnails.Length; i++)
            {
                var label = GetOrCreateLabel(thumbnails[i].image, 15);

                var slot = i;
                Favorites.UpdateStar(thumbnails[i].image, "scene", () =>
                {
                    // The star outlives list changes, so read the list again instead of keeping the one from creation
                    var current = traverse.Field("listPath").GetValue<List<string>>();
                    var index = ThumbnailsPerPage * SceneLoadScene.page + slot;
                    return thumbnails[slot].interactable && current != null && index < current.Count ? current[index] : null;
                }, 24f);

                var index = ThumbnailsPerPage * SceneLoadScene.page + i;
                if (!thumbnails[i].interactable || index >= paths.Count)
                {
                    label.text = string.Empty;
                    continue;
                }

                bool didRead;
                label.text = SceneDurations.Format(SceneDurations.Get(paths[index], true, out didRead));
                read |= didRead;
            }

            if (read) SceneDurations.Save();
        }

        private static Text GetOrCreateLabel(RawImage image, int fontSize = 22)
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
            text.fontSize = fontSize;
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
