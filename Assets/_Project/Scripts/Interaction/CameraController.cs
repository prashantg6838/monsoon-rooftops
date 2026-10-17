using MonsoonRooftops.Grid;
using UnityEngine;

namespace MonsoonRooftops.Interaction
{
    // Isometric-style perspective camera: one-finger pan, two-finger pinch zoom, clamped to the grid.
    public class CameraController : MonoBehaviour
    {
        private const float Pitch = 50f;
        private const float MinDistance = 6f;
        private const float MaxDistance = 24f;
        private const float PanPlaneY = 0.5f;

        private Camera _camera;
        private GridSystem _grid;
        private Vector3 _focus;
        private float _distance;
        private bool _panning;
        private Vector2 _lastScreen;
        private float _lastPinch;
        private float _yaw = 45f;
        private float _targetYaw = 45f;

        // Set by PlacementController when the current press belongs to a drag or the UI.
        public System.Func<bool> IsInputBlocked;

        public void Init(Camera cam, GridSystem grid)
        {
            _camera = cam;
            _grid = grid;
            _camera.orthographic = false;
            _camera.fieldOfView = 30f;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.55f, 0.65f, 0.75f);
            _focus = new Vector3((grid.Width - 1) * 0.5f, PanPlaneY, (grid.Depth - 1) * 0.5f);
            _distance = Mathf.Clamp(Mathf.Max(grid.Width, grid.Depth) * 1.9f, MinDistance, MaxDistance);
            Apply();
        }

        // Turns the view in quarter turns so every side of the level can be inspected.
        public void TurnView(int quarterTurns)
        {
            _targetYaw += 90f * quarterTurns;
        }

        private void LateUpdate()
        {
            if (_camera == null || _grid == null) return; // _grid is lost on a script reload during Play mode

            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.qKey.wasPressedThisFrame) TurnView(-1);
                if (keyboard.eKey.wasPressedThisFrame) TurnView(1);
            }
            _yaw = Mathf.LerpAngle(_yaw, _targetYaw, 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));

            if (PointerInput.TryGetPinch(out var pinch))
            {
                if (_lastPinch > 0f && pinch > 1f) _distance *= _lastPinch / pinch;
                _lastPinch = pinch;
                _panning = false;
            }
            else
            {
                _lastPinch = 0f;
                HandlePan();
                var scroll = PointerInput.ScrollDelta();
                if (Mathf.Abs(scroll) > 0.01f) _distance *= Mathf.Exp(-scroll * 0.002f);
            }
            Apply();
        }

        private void HandlePan()
        {
            bool pressed = PointerInput.PressedCount == 1;
            var screen = PointerInput.Position;
            if (!pressed || (IsInputBlocked != null && IsInputBlocked()))
            {
                _panning = false;
                return;
            }
            if (_panning && TryGroundPoint(_lastScreen, out var before) && TryGroundPoint(screen, out var now))
                _focus += before - now;
            _panning = true;
            _lastScreen = screen;
        }

        private bool TryGroundPoint(Vector2 screen, out Vector3 point)
        {
            var ray = _camera.ScreenPointToRay(screen);
            var plane = new Plane(Vector3.up, new Vector3(0f, PanPlaneY, 0f));
            if (plane.Raycast(ray, out var enter))
            {
                point = ray.GetPoint(enter);
                return true;
            }
            point = default;
            return false;
        }

        private void Apply()
        {
            _distance = Mathf.Clamp(_distance, MinDistance, MaxDistance);
            _focus.x = Mathf.Clamp(_focus.x, 0f, _grid.Width - 1);
            _focus.z = Mathf.Clamp(_focus.z, 0f, _grid.Depth - 1);
            var rotation = Quaternion.Euler(Pitch, _yaw, 0f);
            _camera.transform.SetPositionAndRotation(_focus - rotation * Vector3.forward * _distance, rotation);
        }
    }
}
