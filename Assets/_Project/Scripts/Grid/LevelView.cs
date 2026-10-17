using System.Collections.Generic;
using MonsoonRooftops.Parts;
using UnityEngine;

namespace MonsoonRooftops.Grid
{
    public class CellMarker : MonoBehaviour
    {
        public int X;
        public int Z;
    }

    // Greybox visuals. Shared materials are cloned from the primitive default so this works in both built-in and URP.
    public class LevelView : MonoBehaviour
    {
        public const float TierHeight = 0.75f;
        public const float GroundThickness = 0.2f;

        private readonly Dictionary<Color, Material> _materials = new Dictionary<Color, Material>();
        private readonly List<GameObject> _partObjects = new List<GameObject>();
        private readonly List<GameObject> _markers = new List<GameObject>();
        private GridSystem _grid;
        private PlacementModel _model;
        private Transform _partsRoot;
        private Material _baseMaterial;
        private readonly List<(int x, int z, Bounds box)> _cellBoxes = new List<(int, int, Bounds)>();

        public string LastRayDebug { get; private set; } = "";

        public static float TopY(int h) => GroundThickness + h * TierHeight;

        public void Build(GridSystem grid, PlacementModel model)
        {
            _grid = grid;
            _model = model;
            _partsRoot = new GameObject("Parts").transform;
            _partsRoot.SetParent(transform, false);

            foreach (var c in grid.Cells)
            {
                var cube = CreatePrimitive(PrimitiveType.Cube, transform, "Cell_" + c.x + "_" + c.z);
                float top = TopY(c.h);
                _cellBoxes.Add((c.x, c.z, new Bounds(new Vector3(c.x, top * 0.5f, c.z), new Vector3(0.96f, top, 0.96f))));
                cube.transform.localPosition = new Vector3(c.x, top * 0.5f, c.z);
                cube.transform.localScale = new Vector3(0.96f, top, 0.96f);
                SetColor(cube, SurfaceColor(c.surface, (c.x + c.z) % 2 == 0));
                var marker = cube.AddComponent<CellMarker>();
                marker.X = c.x;
                marker.Z = c.z;
            }

            foreach (var f in grid.FixedObjects)
            {
                if (f.type != "tank") continue;
                var tank = CreatePrimitive(PrimitiveType.Cylinder, transform, "Tank");
                float top = TopY(f.h);
                tank.transform.localPosition = new Vector3(f.x, top + 0.45f, f.z);
                tank.transform.localScale = new Vector3(0.8f, 0.45f, 0.8f);
                SetColor(tank, new Color(0.2f, 0.65f, 0.4f));
                Destroy(tank.GetComponent<Collider>());
            }

            _model.Changed += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            if (_model != null) _model.Changed -= Refresh;
        }

        public Vector3 CellTop(int x, int z)
        {
            _grid.TryGetCell(x, z, out var cell);
            return new Vector3(x, TopY(cell != null ? cell.h : 0), z);
        }

        // Picks the nearest cell box under the pointer by intersecting the camera ray with each cell's bounds.
        // Done by hand instead of Physics.Raycast so it does not depend on colliders or the physics module.
        public bool TryRaycastCell(Camera cam, Vector2 screen, out int x, out int z)
        {
            x = z = 0;
            var ray = cam.ScreenPointToRay(screen);
            float best = float.MaxValue;
            bool found = false;
            foreach (var cell in _cellBoxes)
            {
                if (cell.box.IntersectRay(ray, out var distance) && distance < best)
                {
                    best = distance;
                    x = cell.x;
                    z = cell.z;
                    found = true;
                }
            }
            LastRayDebug = "ray from " + ray.origin.ToString("F1") + " dir " + ray.direction.ToString("F2") +
                           (found ? " cell " + x + "," + z : " miss") + " cells:" + _cellBoxes.Count;
            return found;
        }

        // Rebuilds part visuals. Cheap at greybox scale; replaced by pooled prefabs in the art phase.
        public void Refresh()
        {
            foreach (var go in _partObjects) Destroy(go);
            foreach (var go in _markers) Destroy(go);
            _partObjects.Clear();
            _markers.Clear();
            foreach (var part in _model.Parts)
                _partObjects.Add(BuildPart(part));
        }

        public GameObject CreateMarker(PrimitiveType type, Color color)
        {
            var go = CreatePrimitive(type, null, "Marker");
            SetColor(go, color);
            Destroy(go.GetComponent<Collider>());
            return go;
        }

        public GameObject CreateSlab(Color color)
        {
            var slab = CreatePrimitive(PrimitiveType.Cube, null, "Slab");
            slab.transform.localScale = new Vector3(0.98f, 0.04f, 0.98f);
            SetColor(slab, color);
            Destroy(slab.GetComponent<Collider>());
            return slab;
        }

        public GameObject BuildPartVisual(PartType type, int rotation, Transform parent)
        {
            var root = new GameObject("PartVisual").transform;
            root.SetParent(parent, false);
            DrawPart(root, type, rotation, null);
            return root.gameObject;
        }

        private GameObject BuildPart(PartInstance part)
        {
            var root = new GameObject("Part_" + part.Id).transform;
            root.SetParent(_partsRoot, false);
            root.position = CellTop(part.X, part.Z);
            DrawPart(root, part.Type, part.Rotation, part);
            return root.gameObject;
        }

        private void DrawPart(Transform root, PartType type, int rotation, PartInstance placed)
        {
            var def = PartCatalog.Get(type);
            var color = PartColor(type);

            if (type == PartType.Tarp)
            {
                // Sheet on a post: catches rain over the 3x3 cells around it and pours it out of its spout.
                Box(root, new Vector3(0f, 0.36f, 0f), new Vector3(0.95f, 0.04f, 0.95f), color);
                Box(root, new Vector3(0f, 0.18f, 0f), new Vector3(0.08f, 0.36f, 0.08f), color * 0.7f);
            }
            else if (def.IsVertical)
                Cylinder(root, new Vector3(0f, 0.2f, 0f), new Vector3(0.28f, 0.2f, 0.28f), color);
            else
                Box(root, new Vector3(0f, 0.08f, 0f), new Vector3(0.3f, 0.16f, 0.3f), color);

            if (type == PartType.Valve)
                Cylinder(root, new Vector3(0f, 0.2f, 0f), new Vector3(0.36f, 0.03f, 0.36f), new Color(0.85f, 0.15f, 0.15f));

            foreach (var basePort in def.BasePorts)
            {
                var dir = basePort.Rotate(rotation);
                float dx = dir.OffsetX(), dz = dir.OffsetZ();
                var size = new Vector3(dx != 0 ? 0.5f : 0.3f, 0.16f, dz != 0 ? 0.5f : 0.3f);
                Box(root, new Vector3(dx * 0.25f, 0.08f, dz * 0.25f), size, color);

                if (type == PartType.Downpipe && basePort == Direction.South)
                    Cylinder(root, new Vector3(dx * 0.44f, -0.15f, dz * 0.44f), new Vector3(0.22f, 0.3f, 0.22f), new Color(0.6f, 0.3f, 0.05f));

                if (placed != null)
                {
                    bool connected = _model.IsPortConnected(placed, dir);
                    var lamp = Sphere(root, new Vector3(dx * 0.44f, 0.2f, dz * 0.44f), 0.14f,
                        connected ? new Color(0.2f, 0.9f, 0.3f) : new Color(0.95f, 0.25f, 0.2f));
                    lamp.name = connected ? "PortOk" : "PortOpen";
                }
            }
        }

        private GameObject Box(Transform parent, Vector3 pos, Vector3 scale, Color color)
        {
            var go = CreatePrimitive(PrimitiveType.Cube, parent, "Box");
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            SetColor(go, color);
            Destroy(go.GetComponent<Collider>());
            return go;
        }

        private GameObject Cylinder(Transform parent, Vector3 pos, Vector3 scale, Color color)
        {
            var go = CreatePrimitive(PrimitiveType.Cylinder, parent, "Cyl");
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            SetColor(go, color);
            Destroy(go.GetComponent<Collider>());
            return go;
        }

        private GameObject Sphere(Transform parent, Vector3 pos, float diameter, Color color)
        {
            var go = CreatePrimitive(PrimitiveType.Sphere, parent, "Lamp");
            go.transform.localPosition = pos;
            go.transform.localScale = Vector3.one * diameter;
            SetColor(go, color);
            Destroy(go.GetComponent<Collider>());
            return go;
        }

        private GameObject CreatePrimitive(PrimitiveType type, Transform parent, string objName)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = objName;
            go.transform.SetParent(parent, false);
            if (_baseMaterial == null)
            {
                // Resources/GreyboxBase.mat references the shader so Android builds do not strip it (stripped = pink).
                _baseMaterial = Resources.Load<Material>("GreyboxBase");
                if (_baseMaterial == null) _baseMaterial = go.GetComponent<Renderer>().sharedMaterial;
            }
            return go;
        }

        public void SetColor(GameObject go, Color color)
        {
            if (!_materials.TryGetValue(color, out var mat))
            {
                mat = new Material(_baseMaterial) { color = color };
                _materials[color] = mat;
            }
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        private static Color SurfaceColor(string surface, bool alt)
        {
            float k = alt ? 1f : 0.93f;
            switch (surface)
            {
                case "flat_roof": return new Color(0.72f, 0.66f, 0.58f) * k;
                case "sloped_tile": return new Color(0.78f, 0.4f, 0.3f) * k;
                case "tin_sheet": return new Color(0.6f, 0.68f, 0.74f) * k;
                default: return new Color(0.45f, 0.58f, 0.4f) * k;
            }
        }

        private static Color PartColor(PartType type)
        {
            switch (type)
            {
                case PartType.GutterStraight: return new Color(0.25f, 0.45f, 0.8f);
                case PartType.Elbow90: return new Color(0.55f, 0.35f, 0.8f);
                case PartType.Downpipe: return new Color(0.9f, 0.55f, 0.15f);
                case PartType.TJunction: return new Color(0.2f, 0.7f, 0.7f);
                case PartType.Valve: return new Color(0.5f, 0.5f, 0.55f);
                default: return new Color(0.85f, 0.75f, 0.2f);
            }
        }
    }
}
