using System.Collections.Generic;
using MonsoonRooftops.Grid;
using MonsoonRooftops.Interaction;
using MonsoonRooftops.Levels;
using MonsoonRooftops.Parts;
using MonsoonRooftops.UI;
using MonsoonRooftops.Water;
using UnityEngine;

namespace MonsoonRooftops.Core
{
    // Prepare -> (Test Drizzle) -> Storm -> Results loop for one level. Kept as a simple enum for now; it moves
    // onto GameStateMachine when more screens (menu, level select, consent) exist.
    public class GameFlow : MonoBehaviour
    {
        private enum Phase { Prepare, Drizzle, Storm, Results }

        private const float DrizzleIntensity = 0.4f;
        private const int DrizzleRainTicks = 30;   // 3 s of light rain
        private const int DrizzleTotalTicks = 55;  // plus 2.5 s to watch the water travel
        private const float TapThresholdPx = 20f;
        private const float HintSeconds = 3f;

        private LevelData _level;
        private GridSystem _grid;
        private PlacementModel _model;
        private LevelView _view;
        private Camera _camera;
        private PlacementController _controller;
        private PartsTrayUI _tray;
        private WaterVisualizer _water;
        private List<int> _prepareOnlyButtons;
        private List<int> _resultsButtons;

        public event System.Action DrizzleStarted;
        public event System.Action StormStarted;
        public event System.Action ClogCleared;
        public event System.Action<SimResult> Finished;

        public int StartButton => _startButton;
        public int DrizzleButton => _drizzleButton;

        private WaterSimulator _sim;
        private Phase _phase;
        private int _waveIndex;
        private float _waveTime;
        private float _accumulator;
        private float _speed = 1f;
        private bool _paused;
        private int _drizzleTicks;
        private bool _sandbagArmed;
        private string _hint;
        private float _hintUntil;

        // Storm taps (clear leaves, turn a valve, drop a sandbag).
        private bool _wasPressed, _pressOnUI, _multiTouch;
        private Vector2 _pressPos, _lastPos;

        private int _startButton, _drizzleButton, _pauseButton, _speedButton, _sandbagButton, _backButton;

        public void Init(LevelData level, GridSystem grid, PlacementModel model, LevelView view, Camera cam,
            PlacementController controller, PartsTrayUI tray, WaterVisualizer water, List<int> prepareOnlyButtons,
            List<int> resultsButtons)
        {
            _level = level;
            _grid = grid;
            _model = model;
            _view = view;
            _camera = cam;
            _controller = controller;
            _tray = tray;
            _water = water;
            _prepareOnlyButtons = prepareOnlyButtons;
            _resultsButtons = resultsButtons;

            var bottomRight = new Vector2(1f, 0f);
            var wide = new Vector2(300f, 100f);
            _startButton = tray.AddButton("Start Wave", bottomRight, new Vector2(-350f, 30f), StartStorm, wide);
            _drizzleButton = tray.AddButton("Test Drizzle", bottomRight, new Vector2(-350f, 140f), StartDrizzle, wide);
            _pauseButton = tray.AddButton("Pause", bottomRight, new Vector2(-30f, 30f), TogglePause, wide);
            _speedButton = tray.AddButton("Speed 1x", bottomRight, new Vector2(-30f, 140f), ToggleSpeed, wide);
            _sandbagButton = tray.AddButton("Sandbag", Vector2.zero, new Vector2(30f, 140f), ToggleSandbag, new Vector2(400f, 100f));
            _backButton = tray.AddButton("Try Again", new Vector2(0.5f, 0.5f), new Vector2(-100f, -200f), EnterPrepare,
                new Vector2(320f, 100f));

            EnterPrepare();
        }

        private void SetPrepareButtons(bool visible)
        {
            foreach (var i in _prepareOnlyButtons) _tray.SetButtonVisible(i, visible);
            _tray.SetButtonVisible(_startButton, visible);
            _tray.SetButtonVisible(_drizzleButton, visible);
        }

        private void EnterPrepare()
        {
            _phase = Phase.Prepare;
            _sim = null;
            _water.Clear();
            _controller.enabled = true;
            _tray.SetTrayVisible(true);
            _tray.HideResults();
            _tray.SetProgress(0f, false);
            SetPrepareButtons(true);
            _tray.SetButtonVisible(_pauseButton, false);
            _tray.SetButtonVisible(_speedButton, false);
            _tray.SetButtonVisible(_sandbagButton, false);
            _tray.SetButtonVisible(_backButton, false);
            foreach (var i in _resultsButtons) _tray.SetButtonVisible(i, false);
            _tray.SetStatus("Prepare: " + _level.name + "\nTarget: " + TargetLitres() + " L");
        }

        private WaterSimulator NewSim()
        {
            var sim = new WaterSimulator(_grid, _model, _level.sandbags);
            sim.OnClogged += (x, z) => ShowHint("Leaves! Tap the blocked pipe to clear it");
            sim.OnCellFlooding += (x, z) =>
                ShowHint(_level.sandbags > 0 ? "Flooding! Drop a sandbag on it" : "Flooding! Water is being lost");
            return sim;
        }

        // Free, unlimited 3-second preview: shows the routing without scoring or spending anything.
        private void StartDrizzle()
        {
            if (_phase != Phase.Prepare) return;
            _controller.enabled = false;
            SetPrepareButtons(false);
            _sim = NewSim();
            _phase = Phase.Drizzle;
            _drizzleTicks = 0;
            _accumulator = 0f;
            _tray.SetStatus("Test drizzle...\nWatch where the water goes");
            DrizzleStarted?.Invoke();
        }

        private void StartStorm()
        {
            if (_phase != Phase.Prepare) return;
            _controller.enabled = false;
            _tray.SetTrayVisible(false);
            SetPrepareButtons(false);
            _tray.SetButtonVisible(_pauseButton, true);
            _tray.SetButtonVisible(_speedButton, true);
            _tray.SetButtonVisible(_sandbagButton, _level.sandbags > 0);

            _sim = NewSim();
            _phase = Phase.Storm;
            _waveIndex = 0;
            _waveTime = 0f;
            _accumulator = 0f;
            _paused = false;
            _speed = 1f;
            _sandbagArmed = false;
            _hint = null;
            // The finger that pressed Start Wave is still down: don't treat its release as a storm tap.
            _wasPressed = true;
            _pressOnUI = true;
            _tray.SetButtonLabel(_pauseButton, "Pause");
            _tray.SetButtonLabel(_speedButton, "Speed 1x");
            UpdateSandbagLabel();
            StormStarted?.Invoke();
            if (_level.waves.Length == 0) FinishStorm();
        }

        private void TogglePause()
        {
            _paused = !_paused;
            _tray.SetButtonLabel(_pauseButton, _paused ? "Resume" : "Pause");
        }

        private void ToggleSpeed()
        {
            _speed = _speed > 1f ? 1f : 2f;
            _tray.SetButtonLabel(_speedButton, _speed > 1f ? "Speed 2x" : "Speed 1x");
        }

        private void ToggleSandbag()
        {
            if (_phase != Phase.Storm || _sim.SandbagsLeft <= 0) return;
            _sandbagArmed = !_sandbagArmed;
            UpdateSandbagLabel();
        }

        private void UpdateSandbagLabel()
        {
            int left = _sim != null ? _sim.SandbagsLeft : _level.sandbags;
            _tray.SetButtonLabel(_sandbagButton, _sandbagArmed ? "Tap a pipe..." : "Sandbag x" + left);
        }

        private void ShowHint(string text)
        {
            _hint = text;
            _hintUntil = Time.time + HintSeconds;
        }

        private void Update()
        {
            if (_phase == Phase.Drizzle)
            {
                UpdateDrizzle();
                return;
            }
            if (_phase != Phase.Storm) return;

            HandleStormInput();
            if (!_paused)
            {
                _accumulator += Time.deltaTime * _speed;
                int guard = 0;
                while (_accumulator >= WaterSimulator.TickSeconds && _phase == Phase.Storm && guard++ < 50)
                {
                    _accumulator -= WaterSimulator.TickSeconds;
                    StepOnce();
                }
            }
            if (_phase == Phase.Storm) UpdateHud();
            _water.Render(_sim);
        }

        private void HandleStormInput()
        {
            int count = PointerInput.PressedCount;
            if (count >= 2) _multiTouch = true;

            if (count == 1)
            {
                var pos = PointerInput.Position;
                if (!_wasPressed)
                {
                    _pressPos = pos;
                    _pressOnUI = _tray.IsOverUI(pos);
                    _multiTouch = false;
                }
                _lastPos = pos;
            }
            else if (count == 0 && _wasPressed)
            {
                bool isTap = !_pressOnUI && !_multiTouch && Vector2.Distance(_lastPos, _pressPos) < TapThresholdPx;
                if (isTap) HandleStormTap(_lastPos);
            }
            _wasPressed = count >= 1;
        }

        private void HandleStormTap(Vector2 pos)
        {
            if (!_view.TryRaycastCell(_camera, pos, out var x, out var z)) return;

            if (_sandbagArmed)
            {
                if (_sim.PlaceSandbag(x, z)) _sandbagArmed = false;
                UpdateSandbagLabel();
                return;
            }
            if (_sim.ClearClog(x, z))
            {
                ClogCleared?.Invoke();
                return;
            }
            _sim.ToggleValve(x, z);
        }

        private void UpdateDrizzle()
        {
            _accumulator += Time.deltaTime;
            while (_accumulator >= WaterSimulator.TickSeconds && _phase == Phase.Drizzle)
            {
                _accumulator -= WaterSimulator.TickSeconds;
                _sim.Step(_drizzleTicks < DrizzleRainTicks ? DrizzleIntensity : 0f);
                if (++_drizzleTicks >= DrizzleTotalTicks)
                {
                    EnterPrepare(); // clears the preview water
                    return;
                }
            }
            _water.Render(_sim);
        }

        private void StepOnce()
        {
            var wave = _level.waves[_waveIndex];
            _sim.Step(wave.intensity, WaterSimulator.HazardsOf(wave));
            _waveTime += WaterSimulator.TickSeconds;
            if (_waveTime < wave.durationSec) return;

            _waveIndex++;
            _waveTime = 0f;
            if (_waveIndex >= _level.waves.Length) FinishStorm();
        }

        private void UpdateHud()
        {
            var wave = _level.waves[Mathf.Min(_waveIndex, _level.waves.Length - 1)];
            string status = "Wave " + (_waveIndex + 1) + "/" + _level.waves.Length + ": " + wave.label +
                            "\nTank: " + Mathf.RoundToInt(_sim.TankLitres) + " / " + TargetLitres() + " L";
            if (_hint != null && Time.time < _hintUntil) status += "\n" + _hint;
            _tray.SetStatus(status);
            _tray.SetProgress(wave.durationSec > 0f ? _waveTime / wave.durationSec : 1f, true);
        }

        private void FinishStorm()
        {
            // Rain stops but water already in the pipes keeps flowing for a few seconds.
            for (int i = 0; i < WaterSimulator.DrainTicks; i++) _sim.Step(0f);
            _water.Render(_sim);

            _phase = Phase.Results;
            _sandbagArmed = false;
            var result = _sim.BuildResult(_level.goals);
            _tray.SetButtonVisible(_pauseButton, false);
            _tray.SetButtonVisible(_speedButton, false);
            _tray.SetButtonVisible(_sandbagButton, false);
            _tray.SetButtonVisible(_backButton, true);
            foreach (var i in _resultsButtons) _tray.SetButtonVisible(i, true);
            _tray.SetProgress(0f, false);
            _tray.SetStatus(_level.name);
            _tray.ShowResults(
                (result.Stars > 0 ? "Well done!" : "Not enough water yet") +
                "\n\nStars: " + result.Stars + " / 3" +
                "\nLitres collected: " + Mathf.RoundToInt(result.Litres) + " / " + TargetLitres() +
                "\nFlooded spots: " + result.FloodedCells);
            Finished?.Invoke(result);
        }

        private int TargetLitres() => _level.goals != null ? _level.goals.litresTarget : 0;
    }
}
