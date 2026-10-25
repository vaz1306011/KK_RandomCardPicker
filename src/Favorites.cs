using System.IO;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace RandomCardPicker
{
    /// <summary>
    /// A "Favorite" entry above the top folder of the BrowserFolders tree (same level as scene / female / male).
    /// It is a real folder under UserData/Favorite that holds copies of the favorited cards, so the game lists it
    /// like any other folder and Random works inside it. Cards are added with a star on their preview image.
    /// </summary>
    internal static class Favorites
    {
        private const string FolderName = "Favorite";
        private const string StarName = "RandomCardPicker_Star";

        // Favorite cards unstarred while still shown in the list
        private static readonly System.Collections.Generic.Dictionary<string, byte[]> _removed = new System.Collections.Generic.Dictionary<string, byte[]>();

        private static Texture2D _starOff;
        private static Texture2D _starOn;

        /// <summary>Which card type a tree / browser is for, null for trees that don't get a Favorite entry.</summary>
        private static string KindOfRoot(string defaultPath)
        {
            var path = SubfolderPicker.Normalize(defaultPath).Replace('\\', '/');
            if (path.EndsWith("/studio/scene")) return "scene";
            if (path.EndsWith("/chara/female")) return "female";
            if (path.EndsWith("/chara/male")) return "male";
            return null;
        }

        private static string GetFolder(string kind)
        {
            return Path.Combine(Path.Combine(Path.Combine(Paths.GameRootPath, "UserData"), FolderName), kind);
        }

        /// <summary>Called before BrowserFolders draws a tree node, draws the Favorite row above the top folder.</summary>
        internal static void DrawTreeRowPrefix(object __instance, int indent)
        {
            if (indent != 0) return;

            var traverse = Traverse.Create(__instance);
            var kind = KindOfRoot(traverse.Property("DefaultPath").GetValue<string>());
            if (kind == null) return;

            var folder = GetFolder(kind);
            var current = traverse.Property("CurrentFolder");
            var selected = SubfolderPicker.Normalize(current.GetValue<string>() ?? string.Empty) == SubfolderPicker.Normalize(folder);

            var color = GUI.color;
            if (selected) GUI.color = Color.cyan;
            GUILayout.BeginHorizontal();
            GUILayout.Space(20f);
            if (GUILayout.Button(FolderName, GUI.skin.label, GUILayout.ExpandWidth(true), GUILayout.MinWidth(100f)))
            {
                Directory.CreateDirectory(folder);
                current.SetValue(folder);
            }
            GUILayout.EndHorizontal();
            GUI.color = color;
        }

        /// <summary>Star in the top right corner of the card preview image, filled when the card is in the Favorite folder.</summary>
        internal static void UpdateStar(RawImage image, string kind, System.Func<string> getFile, float size = 40f)
        {
            if (image == null) return;

            var star = image.transform.Find(StarName);
            if (star == null)
            {
                var go = new GameObject(StarName, typeof(RectTransform));
                go.transform.SetParent(image.transform, false);

                var rect = (RectTransform)go.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
                rect.sizeDelta = new Vector2(size, size);
                rect.anchoredPosition = new Vector2(-size * 0.15f, -size * 0.15f);

                var graphic = go.AddComponent<RawImage>();
                var button = go.AddComponent<Button>();
                button.targetGraphic = graphic;
                button.onClick.AddListener(() =>
                {
                    try
                    {
                        Toggle(kind, getFile());
                        SetStarTexture(graphic, IsFavorite(kind, getFile()));
                    }
                    catch (System.Exception ex)
                    {
                        RandomCardPickerPlugin.Logger.LogError(ex);
                    }
                });
                star = go.transform;
            }

            var file = getFile();
            // Nothing to favorite, e.g. an empty slot on the last page
            star.gameObject.SetActive(!string.IsNullOrEmpty(file));
            SetStarTexture(star.GetComponent<RawImage>(), IsFavorite(kind, file));
        }

        private static void SetStarTexture(RawImage graphic, bool favorite)
        {
            if (_starOff == null)
            {
                _starOff = CreateStar(new Color(0.6f, 0.6f, 0.6f, 0.9f));
                _starOn = CreateStar(new Color(1f, 0.8f, 0.1f, 1f));
            }
            graphic.texture = favorite ? _starOn : _starOff;
        }

        private static bool IsFavorite(string kind, string file)
        {
            return !string.IsNullOrEmpty(file) && File.Exists(Path.Combine(GetFolder(kind), Path.GetFileName(file)));
        }

        private static void Toggle(string kind, string file)
        {
            if (string.IsNullOrEmpty(file) || !File.Exists(file)) return;

            var folder = GetFolder(kind);
            // Viewing the Favorite copy itself: the card stays in the list until it is reloaded, so keep its
            // content to bring it back if the star is pressed again
            if (SubfolderPicker.Normalize(Path.GetDirectoryName(file)) == SubfolderPicker.Normalize(folder))
            {
                if (File.Exists(file))
                {
                    _removed[file] = File.ReadAllBytes(file);
                    File.Delete(file);
                }
                else if (_removed.TryGetValue(file, out var bytes))
                {
                    File.WriteAllBytes(file, bytes);
                    _removed.Remove(file);
                }

                SubfolderPicker.InvalidateCache();
                SceneScan.Cancel();
                return;
            }

            var target = Path.Combine(folder, Path.GetFileName(file));
            if (File.Exists(target))
            {
                File.Delete(target);
            }
            else
            {
                Directory.CreateDirectory(folder);
                File.Copy(file, target);
            }

            SubfolderPicker.InvalidateCache();
            SceneScan.Cancel();
        }

        private static Texture2D CreateStar(Color fill)
        {
            const int size = 64;
            const int samples = 3;
            var pixels = new Color[size * size];

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    float inner = 0f, outer = 0f;
                    for (var sy = 0; sy < samples; sy++)
                    {
                        for (var sx = 0; sx < samples; sx++)
                        {
                            var p = new Vector2((x + (sx + 0.5f) / samples) / size * 2f - 1f, (y + (sy + 0.5f) / samples) / size * 2f - 1f);
                            if (InStar(p, 0.95f)) outer += 1f;
                            if (InStar(p, 0.95f * 0.78f)) inner += 1f;
                        }
                    }
                    var n = samples * samples;
                    inner /= n;
                    outer /= n;

                    // Fill over a dark border over transparent
                    var border = new Color(0f, 0f, 0f, 0.85f * outer);
                    pixels[y * size + x] = Color.Lerp(border, new Color(fill.r, fill.g, fill.b, Mathf.Max(border.a, fill.a)), inner);
                }
            }

            var tex = new Texture2D(size, size, TextureFormat.ARGB32, false) { filterMode = FilterMode.Bilinear };
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>Five pointed star with its top point up, <paramref name="radius"/> is the distance of the points from the center.</summary>
        private static bool InStar(Vector2 p, float radius)
        {
            var points = new Vector2[10];
            for (var i = 0; i < 10; i++)
            {
                var angle = Mathf.PI / 2f + i * Mathf.PI / 5f;
                var r = i % 2 == 0 ? radius : radius * 0.4f;
                points[i] = new Vector2(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r - radius * 0.05f);
            }

            var inside = false;
            for (int i = 0, j = 9; i < 10; j = i++)
            {
                if ((points[i].y > p.y) != (points[j].y > p.y) &&
                    p.x < (points[j].x - points[i].x) * (p.y - points[i].y) / (points[j].y - points[i].y) + points[i].x)
                    inside = !inside;
            }
            return inside;
        }
    }
}
