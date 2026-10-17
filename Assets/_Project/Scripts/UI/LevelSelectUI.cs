using System;
using System.Collections.Generic;
using MonsoonRooftops.Interaction;
using MonsoonRooftops.Levels;
using MonsoonRooftops.Save;
using UnityEngine;
using UnityEngine.UI;

namespace MonsoonRooftops.UI
{
    // Level-select map: one row per world, a tile per level with its stars, locked tiles greyed out.
    public class LevelSelectUI : MonoBehaviour
    {
        private const float Tile = 200f;
        private const float Gap = 30f;
        private const float RowHeight = 330f;

        private readonly List<(RectTransform rect, Action onTap)> _tiles = new List<(RectTransform, Action)>();
        private Font _font;
        private int _pressed = -1;
        private bool _wasPressed;
        private Vector2 _lastPos;

        public void Init(LevelIndex index, Action<string> openLevel)
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvasGo = new GameObject("Canvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var safe = NewRect("SafeArea", canvasGo.transform);
            var sa = Screen.safeArea;
            safe.anchorMin = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height);
            safe.anchorMax = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height);
            safe.offsetMin = safe.offsetMax = Vector2.zero;

            var title = NewText(safe, "Monsoon Rooftops", 64, TextAnchor.UpperCenter);
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(1200f, 90f));

            float y = -150f;
            foreach (var world in index.Data.worlds)
            {
                AddRow(safe, world.name, world.levels, y, index, openLevel, true);
                y -= RowHeight;
            }

            // Hazard test levels are for development only.
            if ((Debug.isDebugBuild || Application.isEditor) && index.Data.testLevels.Length > 0)
                AddRow(safe, "Test levels (development builds only)", index.Data.testLevels, y, index, openLevel, false);
        }

        private void AddRow(RectTransform parent, string header, string[] ids, float y, LevelIndex index,
            Action<string> openLevel, bool numbered)
        {
            var label = NewText(parent, header, 40, TextAnchor.UpperLeft);
            Place(label.rectTransform, new Vector2(0f, 1f), new Vector2(60f, y), new Vector2(1400f, 60f));

            for (int i = 0; i < ids.Length; i++)
            {
                string id = ids[i];
                bool unlocked = index.IsUnlocked(id);
                int stars = ProgressStore.GetStars(id);

                var tile = NewRect("Tile_" + id, parent);
                Place(tile, new Vector2(0f, 1f), new Vector2(60f + i * (Tile + Gap), y - 60f), new Vector2(Tile, Tile));
                tile.gameObject.AddComponent<Image>().color = !unlocked ? new Color(0.3f, 0.3f, 0.32f)
                    : stars > 0 ? new Color(0.2f, 0.55f, 0.4f) : new Color(0.25f, 0.45f, 0.7f);

                string name = numbered ? index.Number(id).ToString() : id.Replace("test_", "");
                string footer = !unlocked ? "Locked" : StarText(stars);
                var text = NewText(tile, name + "\n" + footer, numbered ? 52 : 32, TextAnchor.MiddleCenter);
                Stretch(text.rectTransform);

                if (unlocked) _tiles.Add((tile, () => openLevel(id)));
            }
        }

        // Plain characters so it renders with any font: "**-" means 2 of 3 stars.
        private static string StarText(int stars) => new string('*', stars) + new string('-', 3 - stars);

        private void Update()
        {
            int count = PointerInput.PressedCount;
            if (count == 1)
            {
                var pos = PointerInput.Position;
                if (!_wasPressed) _pressed = HitTile(pos);
                _lastPos = pos;
            }
            else if (count == 0 && _wasPressed && _pressed >= 0)
            {
                if (HitTile(_lastPos) == _pressed) _tiles[_pressed].onTap();
                _pressed = -1;
            }
            if (count >= 2) _pressed = -1;
            _wasPressed = count >= 1;
        }

        private int HitTile(Vector2 screen)
        {
            for (int i = 0; i < _tiles.Count; i++)
                if (RectTransformUtility.RectangleContainsScreenPoint(_tiles[i].rect, screen, null)) return i;
            return -1;
        }

        private static void Place(RectTransform r, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            r.anchorMin = r.anchorMax = r.pivot = anchor;
            r.anchoredPosition = pos;
            r.sizeDelta = size;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private Text NewText(Transform parent, string content, int size, TextAnchor anchor)
        {
            var text = NewRect("Text", parent).gameObject.AddComponent<Text>();
            text.font = _font;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = Color.white;
            text.raycastTarget = false;
            text.text = content;
            return text;
        }

        private static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = r.offsetMax = Vector2.zero;
        }
    }
}
