using System.Collections.Generic;
using System.IO;
using MonsoonRooftops.Grid;
using MonsoonRooftops.Levels;
using MonsoonRooftops.Parts;
using MonsoonRooftops.Save;
using MonsoonRooftops.Water;
using UnityEditor;
using UnityEngine;

namespace MonsoonRooftops.EditorTools
{
    // Monsoon > Level Editor: paint roof heights and surfaces, place tanks, set parts, waves and goals,
    // then save JSON into Resources/Levels. The reference solution is kept from the loaded file.
    public class LevelEditorWindow : EditorWindow
    {
        private const string Folder = "Assets/_Project/Resources/Levels";
        private static readonly string[] Surfaces = { "flat_roof", "sloped_tile", "tin_sheet", "ground" };
        private static readonly string[] BrushNames = { "Height", "Surface", "Tank", "Erase" };

        private LevelData _level;
        private string _loadId = "w1_l01";
        private int _brush;
        private int _brushHeight = 1;
        private int _surface;
        private int _tankCapacity = 1000;
        private Vector2 _scroll;
        private string _checkResult = "";

        [MenuItem("Monsoon/Level Editor")]
        private static void Open() => GetWindow<LevelEditorWindow>("Level Editor");

        [MenuItem("Monsoon/Reset Saved Progress")]
        private static void ResetProgress()
        {
            ProgressStore.ResetAll();
            Debug.Log("Saved progress deleted: all levels except the first are locked again.");
        }

        private void OnEnable()
        {
            if (_level == null) _level = NewLevel();
        }

        private static LevelData NewLevel() => LevelLoader.Parse(JsonUtility.ToJson(new LevelData
        {
            id = "new_level", name = "New Level", world = 1,
            grid = new GridInfo { width = 6, depth = 4, maxHeight = 4 },
            cells = new CellData[0],
            goals = new GoalsData { litresTarget = 300, star2 = 200, star3 = 300, maxFloodedCells = 1 },
            waves = new[] { new WaveData { durationSec = 30, intensity = 0.6f, label = "Steady Rain", hazards = new string[0] } }
        }));

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.BeginHorizontal();
            _loadId = EditorGUILayout.TextField("Level id to load", _loadId);
            if (GUILayout.Button("Load", GUILayout.Width(70))) Load();
            if (GUILayout.Button("New", GUILayout.Width(70))) { _level = NewLevel(); _checkResult = ""; }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            _level.id = EditorGUILayout.TextField("Id", _level.id);
            _level.name = EditorGUILayout.TextField("Name", _level.name);
            _level.world = EditorGUILayout.IntField("World", _level.world);
            int w = Mathf.Clamp(EditorGUILayout.IntField("Width", _level.grid.width), 1, 16);
            int d = Mathf.Clamp(EditorGUILayout.IntField("Depth", _level.grid.depth), 1, 16);
            if (w != _level.grid.width || d != _level.grid.depth) Resize(w, d);
            _level.sandbags = EditorGUILayout.IntField("Sandbags", _level.sandbags);

            EditorGUILayout.Space();
            _brush = GUILayout.Toolbar(_brush, BrushNames);
            if (_brush == 0) _brushHeight = EditorGUILayout.IntSlider("Height", _brushHeight, 0, 4);
            if (_brush == 1) _surface = EditorGUILayout.Popup("Surface", _surface, Surfaces);
            if (_brush == 2) _tankCapacity = EditorGUILayout.IntField("Tank litres", _tankCapacity);
            EditorGUILayout.HelpBox("Click cells to paint. Top row is north (+Z). Label: height, surface letter, T = tank.", MessageType.None);
            DrawGrid();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Parts in the tray", EditorStyles.boldLabel);
            DrawParts();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Waves", EditorStyles.boldLabel);
            DrawWaves();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Goals (litres)", EditorStyles.boldLabel);
            _level.goals.litresTarget = EditorGUILayout.IntField("Target", _level.goals.litresTarget);
            _level.goals.star2 = EditorGUILayout.IntField("2 stars", _level.goals.star2);
            _level.goals.star3 = EditorGUILayout.IntField("3 stars", _level.goals.star3);
            _level.goals.maxFloodedCells = EditorGUILayout.IntField("Max flooded for 3 stars", _level.goals.maxFloodedCells);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Reference solution: " + _level.solution.Length + " parts (kept on save)");
            if (GUILayout.Button("Check reference solution")) Check();
            if (_checkResult.Length > 0) EditorGUILayout.HelpBox(_checkResult, MessageType.Info);

            if (GUILayout.Button("Save to Resources/Levels", GUILayout.Height(32))) Save();
            EditorGUILayout.EndScrollView();
        }

        private void DrawGrid()
        {
            for (int z = _level.grid.depth - 1; z >= 0; z--)
            {
                EditorGUILayout.BeginHorizontal();
                for (int x = 0; x < _level.grid.width; x++)
                {
                    var cell = FindCell(x, z);
                    bool tank = FindTank(x, z) != null;
                    string label = cell == null ? "" : cell.h + cell.surface.Substring(0, 1).ToUpper() + (tank ? " T" : "");
                    var old = GUI.backgroundColor;
                    GUI.backgroundColor = cell == null ? Color.black
                        : tank ? new Color(0.3f, 0.9f, 0.5f)
                        : cell.surface == "ground" ? new Color(0.6f, 0.8f, 0.6f)
                        : Color.Lerp(Color.white, new Color(0.9f, 0.5f, 0.3f), cell.h / 4f);
                    if (GUILayout.Button(label, GUILayout.Width(46), GUILayout.Height(40))) Paint(x, z);
                    GUI.backgroundColor = old;
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private void Paint(int x, int z)
        {
            var cells = new List<CellData>(_level.cells);
            var fixedObjects = new List<FixedObjectData>(_level.fixedObjects);
            var cell = FindCell(x, z);
            switch (_brush)
            {
                case 0:
                    if (cell == null) cells.Add(cell = new CellData { x = x, z = z, surface = "flat_roof" });
                    cell.h = _brushHeight;
                    foreach (var f in fixedObjects) if (f.x == x && f.z == z) f.h = cell.h;
                    break;
                case 1:
                    if (cell == null) cells.Add(cell = new CellData { x = x, z = z, h = 0 });
                    cell.surface = Surfaces[_surface];
                    break;
                case 2:
                    if (cell == null) break;
                    var tank = FindTank(x, z);
                    if (tank != null) fixedObjects.Remove(tank);
                    else fixedObjects.Add(new FixedObjectData { type = "tank", x = x, z = z, h = cell.h, capacityL = _tankCapacity });
                    break;
                case 3:
                    cells.RemoveAll(c => c.x == x && c.z == z);
                    fixedObjects.RemoveAll(f => f.x == x && f.z == z);
                    break;
            }
            _level.cells = cells.ToArray();
            _level.fixedObjects = fixedObjects.ToArray();
        }

        private void DrawParts()
        {
            var counts = new Dictionary<string, int>();
            foreach (var p in _level.availableParts) counts[p.type] = p.count;
            var result = new List<PartCountData>();
            foreach (var def in PartCatalog.All)
            {
                counts.TryGetValue(def.Key, out var n);
                n = Mathf.Max(0, EditorGUILayout.IntField(def.DisplayName, n));
                if (n > 0) result.Add(new PartCountData { type = def.Key, count = n });
            }
            _level.availableParts = result.ToArray();
        }

        private void DrawWaves()
        {
            var waves = new List<WaveData>(_level.waves);
            for (int i = 0; i < waves.Count; i++)
            {
                var wave = waves[i];
                EditorGUILayout.BeginHorizontal();
                wave.label = EditorGUILayout.TextField(wave.label, GUILayout.Width(140));
                wave.durationSec = EditorGUILayout.FloatField(wave.durationSec, GUILayout.Width(50));
                GUILayout.Label("s", GUILayout.Width(12));
                wave.intensity = EditorGUILayout.Slider(wave.intensity, 0f, 1.5f);
                bool leaves = wave.hazards != null && System.Array.IndexOf(wave.hazards, "leaves") >= 0;
                leaves = GUILayout.Toggle(leaves, "Leaves", GUILayout.Width(70));
                wave.hazards = leaves ? new[] { "leaves" } : new string[0];
                if (GUILayout.Button("X", GUILayout.Width(24))) { waves.RemoveAt(i); i--; }
                EditorGUILayout.EndHorizontal();
            }
            if (GUILayout.Button("Add wave"))
                waves.Add(new WaveData { durationSec = 20, intensity = 0.6f, label = "Rain", hazards = new string[0] });
            _level.waves = waves.ToArray();
        }

        private void Resize(int w, int d)
        {
            _level.grid.width = w;
            _level.grid.depth = d;
            _level.cells = new List<CellData>(_level.cells).FindAll(c => c.x < w && c.z < d).ToArray();
            _level.fixedObjects = new List<FixedObjectData>(_level.fixedObjects).FindAll(f => f.x < w && f.z < d).ToArray();
        }

        private CellData FindCell(int x, int z)
        {
            foreach (var c in _level.cells) if (c.x == x && c.z == z) return c;
            return null;
        }

        private FixedObjectData FindTank(int x, int z)
        {
            foreach (var f in _level.fixedObjects) if (f.x == x && f.z == z && f.type == "tank") return f;
            return null;
        }

        private void Load()
        {
            var path = Path.Combine(Folder, _loadId + ".json");
            if (!File.Exists(path))
            {
                EditorUtility.DisplayDialog("Level Editor", "No file at " + path, "OK");
                return;
            }
            _level = LevelLoader.Parse(File.ReadAllText(path));
            _checkResult = "";
        }

        private void Check()
        {
            var grid = new GridSystem(_level);
            var limits = new Dictionary<PartType, int>();
            foreach (var p in _level.availableParts)
                if (PartCatalog.TryParse(p.type, out var t)) limits[t] = p.count;
            var model = new PlacementModel(grid, limits);
            foreach (var s in _level.solution)
            {
                if (!PartCatalog.TryParse(s.type, out var t) || model.Place(t, s.x, s.z, s.rot) == null)
                {
                    _checkResult = "Reference part cannot be placed: " + s.type + " at " + s.x + "," + s.z;
                    return;
                }
            }
            var r = WaterSimulator.Run(grid, model, _level);
            _checkResult = "Without tapping leaves: " + Mathf.RoundToInt(r.Litres) + " L, " + r.Stars + " stars, " +
                           r.FloodedCells + " flooded";
        }

        private void Save()
        {
            var path = Path.Combine(Folder, _level.id + ".json");
            File.WriteAllText(path, JsonUtility.ToJson(_level, true));
            AssetDatabase.Refresh();
            Debug.Log("Saved " + path + ". Add the id to index.json to put it on the map.");
        }
    }
}
