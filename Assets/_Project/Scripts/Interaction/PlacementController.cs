using System;
using System.Linq;
using MonsoonRooftops.Grid;
using MonsoonRooftops.Parts;
using MonsoonRooftops.UI;
using UnityEngine;

namespace MonsoonRooftops.Interaction
{
    // Touch/mouse placement: drag from tray to grid, tap a part to rotate it, drag a part to move it,
    // drop a part on the tray to remove it.
    public class PlacementController : MonoBehaviour
    {
        private enum Mode { Idle, Ignore, TrayDrag, PartDrag }

        private const float DragThresholdPx = 20f;
        private static readonly Color Valid = new Color(0.3f, 0.9f, 0.4f);
        private static readonly Color Invalid = new Color(0.95f, 0.3f, 0.25f);
        private static readonly Color SelectedColor = new Color(1f, 0.9f, 0.2f);

        private Camera _camera;
        private LevelView _view;
        private PlacementModel _model;
        private PartsTrayUI _tray;

        private Mode _mode;
        private bool _wasPressed;
        private Vector2 _lastPos;
        private Vector2 _pressPos;
        private bool _moved;
        private PartType _dragType;
        private PartInstance _dragPart;
        private PartType? _armed;
        private bool _fromTray;

        private GameObject _ghostRoot;
        private GameObject _ghostSlab;
        private GameObject _selectionSlab;
        private bool _hasHover;
        private bool _hoverValid;
        private int _hoverX, _hoverZ;

        public PartInstance Selected { get; private set; }

        public string LastError { get; private set; }

        public string DebugInfo =>
            (LastError != null ? "ERR " + LastError + "\n" : "") + _mode + " armed:" + (_armed.HasValue ? _armed.Value.ToString() : "-") +
            " hover:" + (_hasHover ? _hoverX + "," + _hoverZ + (_hoverValid ? " ok" : " bad") : "none") +
            "\n" + _view.LastRayDebug;

        // True while the current press belongs to the UI or a part drag, so the camera must not pan.
        public bool BlockingCamera => _mode != Mode.Idle;

        public void Init(Camera cam, LevelView view, PlacementModel model, PartsTrayUI tray)
        {
            _camera = cam;
            _view = view;
            _model = model;
            _tray = tray;
            _selectionSlab = view.CreateSlab(SelectedColor);
            _selectionSlab.SetActive(false);
        }

        public event Action<PartType?> ArmedChanged;

        private void SetArmed(PartType? type)
        {
            _armed = type;
            _tray.SetArmed(type);
            ArmedChanged?.Invoke(type);
        }

        public void RotateSelected()
        {
            if (Selected != null) _model.Rotate(Selected);
        }

        private void Update()
        {
            if (_camera == null) return;
            int count = PointerInput.PressedCount;

            if (count >= 2)
            {
                if (_mode != Mode.Idle) DestroyGhost();
                _mode = Mode.Ignore;
                _wasPressed = true;
                return;
            }

            bool pressed = count == 1;
            try
            {
                if (pressed)
                {
                    var pos = PointerInput.Position;
                    if (!_wasPressed) OnDown(pos);
                    else OnDrag(pos);
                    _lastPos = pos;
                }
                else if (_wasPressed)
                {
                    OnUp(_lastPos);
                }
                UpdateSelectionMarker();
            }
            catch (Exception e)
            {
                // Never let one failure replay OnDown every frame; show it on screen for device debugging.
                LastError = e.GetType().Name + ": " + e.Message;
                Debug.LogException(e);
                if (!pressed) { DestroyGhost(); _mode = Mode.Idle; }
            }
            _wasPressed = pressed;
        }

        private void OnDown(Vector2 pos)
        {
            _moved = false;
            _pressPos = pos;

            if (_tray.TryHitItem(pos, out var type))
            {
                if (_model.Remaining(type) > 0)
                {
                    _mode = Mode.TrayDrag;
                    _dragType = type;
                    _fromTray = true;
                    CreateGhost(type, 0);
                }
                else _mode = Mode.Ignore;
                return;
            }
            if (_tray.IsOverUI(pos))
            {
                _mode = Mode.Ignore;
                return;
            }
            if (_view.TryRaycastCell(_camera, pos, out var x, out var z))
            {
                var part = _model.PartAt(x, z);
                if (part == null && _armed.HasValue)
                {
                    // Tap-to-place: a part is armed from the tray, so this press places it.
                    _mode = Mode.TrayDrag;
                    _dragType = _armed.Value;
                    _fromTray = false;
                    _moved = true;
                    CreateGhost(_dragType, 0);
                    EvaluateHover(pos);
                    UpdateGhost(pos);
                    return;
                }
                if (part != null)
                {
                    SetArmed(null);
                    _mode = Mode.PartDrag;
                    _dragPart = part;
                    Selected = part;
                    CreateGhost(part.Type, part.Rotation);
                    _ghostRoot.SetActive(false);
                    return;
                }
            }
            Selected = null;
            _mode = Mode.Idle;
        }

        private void OnDrag(Vector2 pos)
        {
            if (_mode != Mode.TrayDrag && _mode != Mode.PartDrag) return;
            // A finger resting on the screen is not a drag: only count movement past the threshold.
            if (!_moved && Vector2.Distance(pos, _pressPos) < DragThresholdPx) return;
            _moved = true;
            EvaluateHover(pos);
            UpdateGhost(pos);
        }

        private void OnUp(Vector2 pos)
        {
            switch (_mode)
            {
                case Mode.TrayDrag:
                    EvaluateHover(pos);
                    if (_hasHover && _hoverValid)
                    {
                        Selected = _model.Place(_dragType, _hoverX, _hoverZ);
                        if (_model.Remaining(_dragType) <= 0) SetArmed(null);
                    }
                    else if (_fromTray && !_moved)
                    {
                        SetArmed(_armed == _dragType ? (PartType?)null : _dragType);
                    }
                    break;

                case Mode.PartDrag:
                    if (!_moved)
                    {
                        _model.Rotate(_dragPart);
                    }
                    else if (_tray.IsOverTray(pos))
                    {
                        _model.Remove(_dragPart);
                        Selected = null;
                    }
                    else
                    {
                        EvaluateHover(pos);
                        if (_hasHover && _hoverValid) _model.Move(_dragPart, _hoverX, _hoverZ);
                    }
                    break;
            }
            DestroyGhost();
            _mode = Mode.Idle;
            _dragPart = null;
        }

        private void EvaluateHover(Vector2 pos)
        {
            _hasHover = !_tray.IsOverTray(pos) && _view.TryRaycastCell(_camera, pos, out _hoverX, out _hoverZ);
            var type = _mode == Mode.PartDrag ? _dragPart.Type : _dragType;
            _hoverValid = _hasHover && _model.CanPlace(type, _hoverX, _hoverZ, _mode == Mode.PartDrag ? _dragPart : null);
        }

        private void CreateGhost(PartType type, int rotation)
        {
            DestroyGhost();
            _ghostRoot = new GameObject("Ghost");
            _view.BuildPartVisual(type, rotation, _ghostRoot.transform);
            _ghostSlab = _view.CreateSlab(Valid);
            _ghostSlab.transform.SetParent(_ghostRoot.transform, false);
            _ghostRoot.SetActive(false);
        }

        private void UpdateGhost(Vector2 pos)
        {
            if (_ghostRoot == null) return;
            _ghostRoot.SetActive(_hasHover);
            if (!_hasHover) return;
            _ghostRoot.transform.position = _view.CellTop(_hoverX, _hoverZ);
            _ghostSlab.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            _view.SetColor(_ghostSlab, _hoverValid ? Valid : Invalid);
        }

        private void DestroyGhost()
        {
            if (_ghostRoot != null) Destroy(_ghostRoot);
            _ghostRoot = null;
            _ghostSlab = null;
            _hasHover = false;
        }

        private void UpdateSelectionMarker()
        {
            bool show = Selected != null && _model.Parts.Contains(Selected);
            _selectionSlab.SetActive(show);
            if (show) _selectionSlab.transform.position = _view.CellTop(Selected.X, Selected.Z) + new Vector3(0f, 0.02f, 0f);
        }
    }
}
