using System.Collections.Generic;
using HarmonyLib;
using Studio;
using UnityEngine;

namespace RandomCardPicker
{
    internal static class ScenePicker
    {
        private const int ThumbnailsPerPage = 12;

        public static void PickRandom(object browser)
        {
            var scene = Object.FindObjectOfType<SceneLoadScene>();
            if (scene == null) return;

            var traverse = Traverse.Create(scene);

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
            });
        }

        private static string GetSelectedPath(Traverse traverse)
        {
            var paths = traverse.Field("listPath").GetValue<List<string>>();
            var select = traverse.Field("select").GetValue<int>();
            return paths != null && select >= 0 && select < paths.Count ? paths[select] : null;
        }
    }
}
