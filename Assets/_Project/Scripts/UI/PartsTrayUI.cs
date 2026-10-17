using System;
using System.Collections.Generic;
using MonsoonRooftops.Interaction;
using MonsoonRooftops.Parts;
using UnityEngine;
using UnityEngine.UI;

namespace MonsoonRooftops.UI
{
    // Builds the HUD in code so the scene needs no hand-wired UI. Buttons are hit-tested here (not through the
    // EventSystem) so they behave identically for touch and mouse.
    public class PartsTrayUI : MonoBehaviour
    {
        private const float PanelWidth = 320f;
        private const float ItemHeight = 150f;
        private const float ItemGap = 16f;

        private class Item
        {
            public PartType Type;
            public RectTransform Rect;
            public Image Background;
            public Text Label;
        }

        private class ButtonEntry
        {
            public RectTransform Rect;
            public Text Label;
            public Action OnClick;
        }

        private readonly List<Item> _items = new List<Item>();
        private readonly List<ButtonEntry> _buttons = new List<ButtonEntry>();
        private RectTransform _safe;
        private RectTransform _panel;
        private RectTransform _resultsPanel;
        private RectTransform _barBackground;
        private RectTransform _barFill;
        private Text _status;
        private Text _resultsText;
        private Text _debug;

        public Func<string> DebugProvider;
        private PlacementModel _model;
        private Font _font;
        private PartType? _armed;
        private int _pressedButton = -1;
        private bool _wasPressed;
        private Vector2 _lastPos;

        public void Init(PlacementModel model)
        {
            _model = model;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvasGo = new GameObject("Canvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            // Keep controls out of notches and rounded corners.
            _safe = NewRect("SafeArea", canvasGo.transform);
            var sa = Screen.safeArea;
            _safe.anchorMin = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height);
            _safe.anchorMax = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height);
            _safe.offsetMin = _safe.offsetMax = Vector2.zero;

            _panel = NewRect("Tray", _safe);
            _panel.anchorMin = new Vector2(1f, 0f);
            _panel.anchorMax = new Vector2(1f, 1f);
            _panel.pivot = new Vector2(1f, 0.5f);
            _panel.sizeDelta = new Vector2(PanelWidth, 0f);
            _panel.anchoredPosition = Vector2.zero;
            _panel.gameObject.AddComponent<Image>().color = new Color(0.12f, 0.14f, 0.18f, 0.85f);

            int i = 0;
            foreach (var type in model.AvailableTypes)
            {
                var rect = NewRect("Item_" + type, _panel);
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.sizeDelta = new Vector2(PanelWidth - 40f, ItemHeight);
                rect.anchoredPosition = new Vector2(0f, -20f - i * (ItemHeight + ItemGap));
                var bg = rect.gameObject.AddComponent<Image>();
                var label = NewText("Label", rect, 34, TextAnchor.MiddleCenter);
                Stretch(label.rectTransform);
                _items.Add(new Item { Type = type, Rect = rect, Background = bg, Label = label });
                i++;
            }

            _status = NewText("Status", _safe, 40, TextAnchor.UpperLeft);
            _status.rectTransform.anchorMin = _status.rectTransform.anchorMax = new Vector2(0f, 1f);
            _status.rectTransform.pivot = new Vector2(0f, 1f);
            _status.rectTransform.sizeDelta = new Vector2(1100f, 160f);
            _status.rectTransform.anchoredPosition = new Vector2(30f, -20f);

            if (Debug.isDebugBuild) // tick Development Build to see touch/placement diagnostics on a device
            {
                // Development builds only: shows what the touch input reads, to diagnose device problems.
                _debug = NewText("Debug", _safe, 28, TextAnchor.UpperCenter);
                _debug.rectTransform.anchorMin = _debug.rectTransform.anchorMax = new Vector2(0.5f, 1f);
                _debug.rectTransform.pivot = new Vector2(0.5f, 1f);
                _debug.rectTransform.sizeDelta = new Vector2(1400f, 260f);
                _debug.rectTransform.anchoredPosition = new Vector2(-100f, -10f);
                _debug.color = Color.yellow;
            }

            _barBackground = NewRect("WaveBar", _safe);
            _barBackground.anchorMin = _barBackground.anchorMax = new Vector2(0f, 1f);
            _barBackground.pivot = new Vector2(0f, 1f);
            _barBackground.sizeDelta = new Vector2(700f, 24f);
            _barBackground.anchoredPosition = new Vector2(30f, -185f);
            _barBackground.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.5f);
            _barFill = NewRect("Fill", _barBackground);
            _barFill.anchorMin = Vector2.zero;
            _barFill.anchorMax = new Vector2(0f, 1f);
            _barFill.offsetMin = _barFill.offsetMax = Vector2.zero;
            _barFill.gameObject.AddComponent<Image>().color = new Color(0.3f, 0.8f, 0.4f);
            _barBackground.gameObject.SetActive(false);

            _resultsPanel = NewRect("Results", _safe);
            _resultsPanel.anchorMin = _resultsPanel.anchorMax = new Vector2(0.5f, 0.5f);
            _resultsPanel.pivot = new Vector2(0.5f, 0.5f);
            _resultsPanel.sizeDelta = new Vector2(900f, 520f);
            _resultsPanel.anchoredPosition = new Vector2(-100f, 40f);
            _resultsPanel.gameObject.AddComponent<Image>().color = new Color(0.1f, 0.12f, 0.16f, 0.92f);
            _resultsText = NewText("Text", _resultsPanel, 46, TextAnchor.MiddleCenter);
            Stretch(_resultsText.rectTransform);
            _resultsPanel.gameObject.SetActive(false);

            model.Changed += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            if (_model != null) _model.Changed -= Refresh;
        }

        // anchor: 0..1 corner/edge of the safe area the button hangs from; pos is the offset from it.
        public int AddButton(string text, Vector2 anchor, Vector2 pos, Action onClick, Vector2? size = null)
        {
            var rect = NewRect(text + "Button", _safe);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.sizeDelta = size ?? new Vector2(220f, 100f);
            rect.anchoredPosition = pos;
            rect.gameObject.AddComponent<Image>().color = new Color(0.2f, 0.55f, 0.4f, 1f);
            var label = NewText("Label", rect, 38, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform);
            label.text = text;
            _buttons.Add(new ButtonEntry { Rect = rect, Label = label, OnClick = onClick });
            return _buttons.Count - 1;
        }

        public void SetButtonLabel(int index, string text) => _buttons[index].Label.text = text;

        public void SetButtonVisible(int index, bool visible) => _buttons[index].Rect.gameObject.SetActive(visible);

        public void SetStatus(string text) => _status.text = text;

        public void SetProgress(float t, bool visible)
        {
            _barBackground.gameObject.SetActive(visible);
            _barFill.anchorMax = new Vector2(Mathf.Clamp01(t), 1f);
        }

        public void SetTrayVisible(bool visible) => _panel.gameObject.SetActive(visible);

        public void ShowResults(string text)
        {
            _resultsText.text = text;
            _resultsPanel.gameObject.SetActive(true);
        }

        public void HideResults() => _resultsPanel.gameObject.SetActive(false);

        public void SetArmed(PartType? type)
        {
            _armed = type;
            Refresh();
        }

        public bool TryHitItem(Vector2 screen, out PartType type)
        {
            type = default;
            if (!_panel.gameObject.activeInHierarchy) return false;
            foreach (var item in _items)
            {
                if (RectTransformUtility.RectangleContainsScreenPoint(item.Rect, screen, null))
                {
                    type = item.Type;
                    return true;
                }
            }
            return false;
        }

        public bool TryGetItemScreenCenter(PartType type, out Vector2 screen)
        {
            screen = default;
            if (!_panel.gameObject.activeInHierarchy) return false;
            foreach (var item in _items)
            {
                if (item.Type != type) continue;
                screen = item.Rect.TransformPoint(item.Rect.rect.center);
                return true;
            }
            return false;
        }

        public bool TryGetButtonScreenCenter(int index, out Vector2 screen)
        {
            var r = _buttons[index].Rect;
            screen = r.TransformPoint(r.rect.center);
            return r.gameObject.activeInHierarchy;
        }

        public bool IsOverTray(Vector2 screen) =>
            _panel.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(_panel, screen, null);

        public bool IsOverUI(Vector2 screen)
        {
            if (IsOverTray(screen)) return true;
            if (_resultsPanel.gameObject.activeInHierarchy &&
                RectTransformUtility.RectangleContainsScreenPoint(_resultsPanel, screen, null)) return true;
            return TryHitButton(screen, out _);
        }

        private bool TryHitButton(Vector2 screen, out int index)
        {
            for (int i = 0; i < _buttons.Count; i++)
            {
                var r = _buttons[i].Rect;
                if (r.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(r, screen, null))
                {
                    index = i;
                    return true;
                }
            }
            index = -1;
            return false;
        }

        private void Update()
        {
            int count = PointerInput.PressedCount;
            if (_debug != null)
                _debug.text = "touches " + count + "  pos " + PointerInput.Position + "  parts " + _model.Parts.Count +
                              "  overItem " + TryHitItem(PointerInput.Position, out _) + "  overUI " + IsOverUI(PointerInput.Position) +
                              "  screen " + Screen.width + "x" + Screen.height +
                              (DebugProvider != null ? "\n" + DebugProvider() : "");
            if (count >= 2) _pressedButton = -1;

            if (count == 1)
            {
                var pos = PointerInput.Position;
                if (!_wasPressed && TryHitButton(pos, out var hit)) _pressedButton = hit;
                _lastPos = pos;
            }
            else if (_wasPressed && _pressedButton >= 0)
            {
                if (TryHitButton(_lastPos, out var released) && released == _pressedButton)
                    _buttons[_pressedButton].OnClick();
                _pressedButton = -1;
            }
            _wasPressed = count >= 1;
        }

        private void Refresh()
        {
            foreach (var item in _items)
            {
                int left = _model.Remaining(item.Type);
                item.Label.text = PartCatalog.Get(item.Type).DisplayName + "\nx" + left;
                bool armed = _armed.HasValue && _armed.Value == item.Type && left > 0;
                item.Background.color = armed ? new Color(0.9f, 0.7f, 0.1f, 1f)
                    : left > 0 ? new Color(0.25f, 0.45f, 0.7f, 1f) : new Color(0.3f, 0.3f, 0.3f, 1f);
            }
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private Text NewText(string name, Transform parent, int size, TextAnchor anchor)
        {
            var rect = NewRect(name, parent);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = _font;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = Color.white;
            text.raycastTarget = false;
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
