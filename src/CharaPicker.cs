using System.Linq;
using HarmonyLib;
using Studio;
using UnityEngine;
using UnityEngine.UI;

namespace RandomCardPicker
{
    internal static class CharaPicker
    {
        public static void PickRandom()
        {
            // Male and female lists both exist, only the one on screen is visible
            var list = Object.FindObjectsOfType<CharaList>().FirstOrDefault(x => x.isActiveAndEnabled && x.isInit);
            if (list == null) return;

            var traverse = Traverse.Create(list);
            var sort = traverse.Field("charaFileSort").GetValue<CharaFileSort>();
            var count = sort.cfiList.Count;
            if (count == 0) return;

            var index = RandomIndex(count, sort.select);

            // Same as the user clicking the entry: marks it selected and enables the Load / Change buttons
            traverse.Method("OnSelectChara", new[] { typeof(int) }, new object[] { index }).GetValue();

            var image = traverse.Field("imageChara").GetValue<RawImage>();
            image.texture = PngAssist.LoadTexture(sort.cfiList[index].file);
            image.color = Color.white;

            ScrollTo(sort, index, count);
        }

        internal static int RandomIndex(int count, int exclude)
        {
            if (count == 1) return 0;
            if (exclude < 0) return Random.Range(0, count);

            // Pick from the other entries so pressing Random always changes the selection
            var index = Random.Range(0, count - 1);
            return index >= exclude ? index + 1 : index;
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
