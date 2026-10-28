using System.Linq;
using HarmonyLib;
using Studio;
using UnityEngine;
using UnityEngine.UI;

namespace RandomCardPicker
{
    internal static class CharaPicker
    {
        public static void PickRandom(object browser, bool back)
        {
            // Male and female lists both exist, only the one on screen is visible
            var list = Object.FindObjectsOfType<CharaList>().FirstOrDefault(x => x.isActiveAndEnabled && x.isInit);
            if (list == null) return;

            var traverse = Traverse.Create(list);
            var sort = traverse.Field("charaFileSort").GetValue<CharaFileSort>();
            var historyKey = "chara" + traverse.Field("sex").GetValue<int>();

            System.Func<string, bool> tryShow = file =>
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
            };

            if (back) SubfolderPicker.Back(browser, historyKey, tryShow);
            else SubfolderPicker.Pick(browser, historyKey, sort.selectPath, tryShow);
        }

        public static void UpdateStar(CharaList list, int previewIndex = -1)
        {
            var traverse = Traverse.Create(list);
            var kind = traverse.Field("sex").GetValue<int>() == 0 ? "male" : "female";
            var sort = traverse.Field("charaFileSort").GetValue<CharaFileSort>();

            string preview = null;
            if (previewIndex >= 0 && sort?.cfiList != null && previewIndex < sort.cfiList.Count)
                preview = sort.cfiList[previewIndex].file;

            Favorites.UpdateStar(traverse.Field("imageChara").GetValue<RawImage>(), kind,
                () => traverse.Field("charaFileSort").GetValue<CharaFileSort>().selectPath, previewFile: preview);
        }

        /// <summary>The game resets the sort to name whenever the list is rebuilt, the History and Most Used folders are only useful sorted by date.</summary>
        public static void SortUsageByTime(CharaList list)
        {
            var sort = Traverse.Create(list).Field("charaFileSort").GetValue<CharaFileSort>();
            if (sort?.cfiList == null || sort.cfiList.Count < 2) return;

            var folder = SubfolderPicker.Normalize(System.IO.Path.GetDirectoryName(sort.cfiList[0].file));
            var kind = Traverse.Create(list).Field("sex").GetValue<int>() == 0 ? "male" : "female";
            if (folder != SubfolderPicker.Normalize(History.GetFolder(kind)) && folder != SubfolderPicker.Normalize(MostUsed.GetFolder(kind))) return;

            const int timeSort = 1;
            sort.Sort(timeSort, false);
            // Which bool means descending is not obvious from the game code, so check the result
            var cfi = sort.cfiList;
            if (System.IO.File.GetLastWriteTime(cfi[0].file) < System.IO.File.GetLastWriteTime(cfi[cfi.Count - 1].file))
                sort.Sort(timeSort, true);
        }

        internal static string GetSelectedPath(CharaList list)
        {
            return Traverse.Create(list).Field("charaFileSort").GetValue<CharaFileSort>().selectPath;
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
