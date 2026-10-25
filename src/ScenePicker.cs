using System.Collections.Generic;
using HarmonyLib;
using Studio;
using UnityEngine;

namespace RandomCardPicker
{
    internal static class ScenePicker
    {
        private const int ThumbnailsPerPage = 12;

        public static void PickRandom()
        {
            var scene = Object.FindObjectOfType<SceneLoadScene>();
            if (scene == null) return;

            var traverse = Traverse.Create(scene);
            var paths = traverse.Field("listPath").GetValue<List<string>>();
            if (paths == null || paths.Count == 0) return;

            var index = CharaPicker.RandomIndex(paths.Count, traverse.Field("select").GetValue<int>());

            // Same as the user switching page and clicking the thumbnail: opens the Load / Import / Delete panel
            traverse.Method("SetPage", new[] { typeof(int) }, new object[] { index / ThumbnailsPerPage }).GetValue();
            scene.OnClickThumbnail(index % ThumbnailsPerPage);
        }
    }
}
