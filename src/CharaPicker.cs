using System.Linq;
using HarmonyLib;
using Studio;
using UnityEngine;
using UnityEngine.UI;

namespace RandomCardPicker
{
    internal static class CharaPicker
    {
        public static void PickRandom(object browser)
        {
            // Male and female lists both exist, only the one on screen is visible
            var list = Object.FindObjectsOfType<CharaList>().FirstOrDefault(x => x.isActiveAndEnabled && x.isInit);
            if (list == null) return;

            var traverse = Traverse.Create(list);
            var sort = traverse.Field("charaFileSort").GetValue<CharaFileSort>();

            SubfolderPicker.Pick(browser, sort.selectPath, file =>
            {
                // The list is rebuilt when the folder changes, so read it again
                var index = sort.cfiList.FindIndex(x => SubfolderPicker.Normalize(x.file) == SubfolderPicker.Normalize(file));
                if (index < 0) return false;

                // Same as the user clicking the entry: marks it selected and enables the Load / Change buttons
                traverse.Method("OnSelectChara", new[] { typeof(int) }, new object[] { index }).GetValue();

                var image = traverse.Field("imageChara").GetValue<RawImage>();
                image.texture = PngAssist.LoadTexture(file);
                image.color = Color.white;

                ScrollTo(sort, index, sort.cfiList.Count);
                return true;
            });
        }

        public static void UpdateStar(CharaList list)
        {
            var traverse = Traverse.Create(list);
            var kind = traverse.Field("sex").GetValue<int>() == 0 ? "male" : "female";
            Favorites.UpdateStar(traverse.Field("imageChara").GetValue<RawImage>(), kind,
                () => traverse.Field("charaFileSort").GetValue<CharaFileSort>().selectPath);
        }

        private static void ScrollTo(CharaFileSort sort, int index, int count)
        {
            var scroll = sort.root.GetComponentInParent<ScrollRect>();
            if (scroll == null) return;

            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = count > 1 ? 1f - index / (float)(count - 1) : 1f;
        }
    }
}
