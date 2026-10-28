using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Studio;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RandomCardPicker
{
    /// <summary>
    /// Selects the female characters in the scene, grouped by name: every character sharing a name is selected together,
    /// and pressing the hotkey again moves on to the next name.
    /// </summary>
    internal static class FemaleSelector
    {
        private const int ScrollDelayFrames = 2;

        private static TreeNodeObject scrollTarget;
        private static int scrollDelay;

        public static void SelectNext()
        {
            if (IsTyping()) return;

            var studio = Singleton<Studio.Studio>.Instance;
            if (studio == null) return;

            var groups = studio.dicInfo.Values
                .OfType<OCIChar>()
                .Where(x => x.sex == 1)
                .OrderBy(x => x.oiCharInfo.dicKey)
                .GroupBy(x => GetName(x))
                .Select(g => g.ToList())
                .ToList();
            if (groups.Count == 0) return;

            var tree = studio.treeNodeCtrl;
            var current = new HashSet<TreeNodeObject>(tree.selectNodes);

            // Continue after the group that is currently selected, otherwise start from the first
            var index = groups.FindIndex(g => g.Count == current.Count && g.All(x => current.Contains(x.treeNodeObject)));
            var next = groups[(index + 1) % groups.Count];

            // _deselect: false, or selecting the only selected node again would deselect it
            tree.SelectSingle(next[0].treeNodeObject, false);
            var add = Traverse.Create(tree).Method("AddSelectNode", new[] { typeof(TreeNodeObject), typeof(bool) });
            foreach (var chara in next.Skip(1))
                add.GetValue(chara.treeNodeObject, true);

            // A character inside a folder or another object is hidden while its parent is collapsed
            var node = next[0].treeNodeObject;
            for (var p = node.parent; p != null; p = p.parent)
                p.treeState = TreeNodeObject.TreeState.Open;

            // The list needs a moment to lay out the newly opened nodes before their position is known
            scrollTarget = node;
            scrollDelay = ScrollDelayFrames;
        }

        /// <summary>Called every frame, scrolls the workspace to the selected character once the layout has settled.</summary>
        public static void Update()
        {
            if (scrollTarget == null || --scrollDelay > 0) return;

            var node = scrollTarget;
            scrollTarget = null;
            if (node == null || !node.gameObject.activeInHierarchy) return;

            var scrollRect = Traverse.Create(Singleton<Studio.Studio>.Instance.treeNodeCtrl).Field("scrollRect").GetValue<ScrollRect>();
            if (scrollRect == null || scrollRect.content == null) return;

            var content = scrollRect.content;
            var viewport = scrollRect.viewport ?? scrollRect.GetComponent<RectTransform>();
            Canvas.ForceUpdateCanvases();

            // Both in the content's local space, so the difference is how far the content has to move to put the node in the middle
            var offset = RectTransformUtility.CalculateRelativeRectTransformBounds(content, node.rectNode).center.y
                         - RectTransformUtility.CalculateRelativeRectTransformBounds(content, viewport).center.y;
            content.anchoredPosition -= new Vector2(0f, offset);
            scrollRect.verticalNormalizedPosition = Mathf.Clamp01(scrollRect.verticalNormalizedPosition);
        }

        // Characters without a name each count as their own group
        private static string GetName(OCIChar chara)
        {
            var name = chara.charInfo.chaFile.parameter.fullname.Trim();
            return name.Length > 0 ? name : "\0" + chara.oiCharInfo.dicKey;
        }

        private static bool IsTyping()
        {
            var selected = EventSystem.current?.currentSelectedGameObject;
            return selected != null && selected.GetComponent<InputField>() != null;
        }
    }
}
