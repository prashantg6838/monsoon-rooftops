using System.Collections.Generic;
using MonsoonRooftops.Grid;
using MonsoonRooftops.Interaction;
using MonsoonRooftops.Levels;
using MonsoonRooftops.Parts;
using MonsoonRooftops.Save;
using MonsoonRooftops.UI;
using MonsoonRooftops.Water;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace MonsoonRooftops.Core
{
    // Scene entry point: shows the level map, or builds the chosen level. Every screen is built in code.
    public class Phase1Bootstrap : MonoBehaviour
    {
        // Set a level id here to jump straight into it from the editor; leave empty to start on the map.
        [SerializeField] private string debugStartLevel = "";

        private void Start()
        {
            Application.targetFrameRate = 60;
            var index = LevelIndex.Load();
            var cam = EnsureCamera();
            EnsureLight();
            EnsureEventSystem();

            var selected = GameSession.SelectedLevel;
            if (selected == null && !string.IsNullOrEmpty(debugStartLevel)) selected = debugStartLevel;

            if (selected == null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.16f, 0.2f, 0.28f);
                new GameObject("LevelSelect").AddComponent<LevelSelectUI>().Init(index, OpenLevel);
                return;
            }
            BuildLevel(selected, index, cam);
        }

        private static void OpenLevel(string id)
        {
            GameSession.SelectedLevel = id;
            Reload();
        }

        private static void ShowMap()
        {
            GameSession.SelectedLevel = null;
            Reload();
        }

        private static void Reload() => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);

        private static void BuildLevel(string id, LevelIndex index, Camera cam)
        {
            var level = LevelLoader.LoadResource(id);
            var grid = new GridSystem(level);

            var limits = new Dictionary<PartType, int>();
            foreach (var p in level.availableParts)
            {
                if (PartCatalog.TryParse(p.type, out var type)) limits[type] = p.count;
                else Debug.LogWarning("Unknown part type in level: " + p.type);
            }
            var model = new PlacementModel(grid, limits);

            var view = new GameObject("Level").AddComponent<LevelView>();
            view.Build(grid, model);

            var cameraController = cam.gameObject.AddComponent<CameraController>();
            cameraController.Init(cam, grid);

            var tray = new GameObject("UI").AddComponent<PartsTrayUI>();
            tray.Init(model);

            var controller = new GameObject("Placement").AddComponent<PlacementController>();
            controller.Init(cam, view, model, tray);
            tray.DebugProvider = () => controller.DebugInfo;

            var bottomLeft = Vector2.zero;
            var centre = new Vector2(0.5f, 0.5f);
            var resultSize = new Vector2(320f, 100f);
            var prepareOnly = new List<int>
            {
                tray.AddButton("Undo", bottomLeft, new Vector2(30f, 30f), () => model.Undo()),
                tray.AddButton("Rotate", bottomLeft, new Vector2(270f, 30f), controller.RotateSelected),
                tray.AddButton("Map", new Vector2(0f, 1f), new Vector2(30f, -190f), ShowMap, new Vector2(200f, 90f))
            };
            tray.AddButton("View <", bottomLeft, new Vector2(510f, 30f), () => cameraController.TurnView(-1));
            tray.AddButton("View >", bottomLeft, new Vector2(750f, 30f), () => cameraController.TurnView(1));

            string next = index.NextAfter(id);
            int nextButton = tray.AddButton("Next Level", centre, new Vector2(260f, -200f), () => OpenLevel(next), resultSize);
            var resultsOnly = new List<int>
            {
                tray.AddButton("Map", centre, new Vector2(-460f, -200f), ShowMap, resultSize),
                nextButton
            };

            cameraController.IsInputBlocked = () => controller.BlockingCamera || tray.IsOverUI(PointerInput.Position);

            var water = new GameObject("Water").AddComponent<WaterVisualizer>();
            water.Init(view);
            var flow = new GameObject("GameFlow").AddComponent<GameFlow>();
            flow.Init(level, grid, model, view, cam, controller, tray, water, prepareOnly, resultsOnly);
            flow.Finished += result =>
            {
                ProgressStore.Record(id, result.Stars);
                tray.SetButtonVisible(nextButton, next != null && index.IsUnlocked(next));
            };

            // Tutorials play until the level has been passed once.
            if (level.tutorial.Length > 0 && ProgressStore.GetStars(id) == 0)
                new GameObject("Tutorial").AddComponent<TutorialRunner>()
                    .Init(level.tutorial, cam, view, tray, controller, model, flow);
        }

        private static Camera EnsureCamera()
        {
            var cam = Camera.main;
            if (cam != null) return cam;
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.AddComponent<AudioListener>();
            return camGo.AddComponent<Camera>();
        }

        private static void EnsureLight()
        {
            if (FindFirstObjectByType<Light>() != null) return;
            var light = new GameObject("Sun").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }
    }
}
