using System.Collections.Generic;
using MonsoonRooftops.Core;
using MonsoonRooftops.Grid;
using MonsoonRooftops.Interaction;
using MonsoonRooftops.Levels;
using MonsoonRooftops.Parts;
using UnityEngine;
using UnityEngine.UI;

namespace MonsoonRooftops.UI
{
    // One sentence plus a pulsing "TAP" marker, advancing when the player does the step's action.
    public class TutorialRunner : MonoBehaviour
    {
        private TutorialStepData[] _steps;
        private int _index;
        private Camera _camera;
        private LevelView _view;
        private PartsTrayUI _tray;
        private PlacementModel _model;
        private Dictionary<string, int> _buttons;

        private GameObject _canvas;
        private RectTransform _hand;
        private Text _text;
        private int _partCount;
        private int _rotationSignature;

        public void Init(TutorialStepData[] steps, Camera cam, LevelView view, PartsTrayUI tray,
            PlacementController controller, PlacementModel model, GameFlow flow)
        {
            _steps = steps;
            _camera = cam;
            _view = view;
            _tray = tray;
            _model = model;
            _buttons = new Dictionary<string, int> { { "start", flow.StartButton }, { "drizzle", flow.DrizzleButton } };
            BuildOverlay();

            _partCount = model.Parts.Count;
            _rotationSignature = RotationSignature();
            controller.ArmedChanged += type => { if (type.HasValue) Advance("arm"); };
            model.Changed += OnModelChanged;
            flow.DrizzleStarted += () => Advance("drizzle");
            flow.StormStarted += () => Advance("start");
            flow.ClogCleared += () => Advance("clear");
            flow.Finished += _ => Close();
            Show();
        }

        private void OnModelChanged()
        {
            int count = _model.Parts.Count;
            int signature = RotationSignature();
            if (count > _partCount) Advance("place");
            else if (count == _partCount && signature != _rotationSignature) Advance("rotate");
            _partCount = count;
            _rotationSignature = signature;
        }

        private int RotationSignature()
        {
            int sum = 0;
            foreach (var p in _model.Parts) sum = sum * 31 + p.Id * 4 + p.Rotation;
            return sum;
        }

        private void Advance(string action)
        {
            if (_canvas == null || _index >= _steps.Length || _steps[_index].advance != action) return;
            _index++;
            Show();
        }

        private void Show()
        {
            if (_index >= _steps.Length)
            {
                Close();
                return;
            }
            _text.text = _steps[_index].text;
        }

        private void Close()
        {
            if (_canvas != null) Destroy(_canvas);
            _canvas = null;
        }

        private void LateUpdate()
        {
            if (_canvas == null) return;
            bool visible = TryGetTarget(_steps[_index], out var screen);
            _hand.gameObject.SetActive(visible);
            if (!visible) return;
            _hand.position = screen;
            _hand.localScale = Vector3.one * (1f + 0.15f * Mathf.Sin(Time.time * 6f));
        }

        private bool TryGetTarget(TutorialStepData step, out Vector2 screen)
        {
            screen = default;
            var point = step.point ?? "none";
            if (point.StartsWith("tray:"))
                return PartCatalog.TryParse(point.Substring(5), out var type) && _tray.TryGetItemScreenCenter(type, out screen);
            if (point.StartsWith("button:"))
                return _buttons.TryGetValue(point.Substring(7), out var index) && _tray.TryGetButtonScreenCenter(index, out screen);
            if (point == "cell" && step.cell != null && step.cell.Length == 2)
            {
                var world = _view.CellTop(step.cell[0], step.cell[1]) + Vector3.up * 0.2f;
                var p = _camera.WorldToScreenPoint(world);
                screen = p;
                return p.z > 0f;
            }
            return false;
        }

        private void BuildOverlay()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _canvas = new GameObject("TutorialCanvas");
            _canvas.transform.SetParent(transform, false);
            var canvas = _canvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = _canvas.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var banner = NewRect("Banner", _canvas.transform);
            banner.anchorMin = banner.anchorMax = banner.pivot = new Vector2(0.5f, 1f);
            banner.anchoredPosition = new Vector2(-100f, -230f);
            banner.sizeDelta = new Vector2(1000f, 110f);
            var bg = banner.gameObject.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.05f, 0.1f, 0.8f);
            bg.raycastTarget = false;
            _text = NewRect("Text", banner).gameObject.AddComponent<Text>();
            _text.font = font;
            _text.fontSize = 38;
            _text.alignment = TextAnchor.MiddleCenter;
            _text.color = new Color(1f, 0.95f, 0.6f);
            _text.raycastTarget = false;
            var tr = _text.rectTransform;
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = new Vector2(20f, 0f);
            tr.offsetMax = new Vector2(-20f, 0f);

            // Marker: a yellow diamond with "TAP" under it. Never blocks touches.
            _hand = NewRect("Hand", _canvas.transform);
            _hand.sizeDelta = new Vector2(80f, 80f);
            var diamond = NewRect("Diamond", _hand);
            diamond.sizeDelta = new Vector2(56f, 56f);
            diamond.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var img = diamond.gameObject.AddComponent<Image>();
            img.color = new Color(1f, 0.85f, 0.1f, 0.85f);
            img.raycastTarget = false;
            var tap = NewRect("Tap", _hand).gameObject.AddComponent<Text>();
            tap.font = font;
            tap.fontSize = 30;
            tap.text = "TAP";
            tap.alignment = TextAnchor.MiddleCenter;
            tap.color = Color.white;
            tap.raycastTarget = false;
            tap.rectTransform.sizeDelta = new Vector2(120f, 40f);
            tap.rectTransform.anchoredPosition = new Vector2(0f, -60f);
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }
    }
}
