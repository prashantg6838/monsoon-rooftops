using System;
using System.Collections.Generic;
using MonsoonRooftops.Grid;
using UnityEngine;

namespace MonsoonRooftops.Water
{
    // Greybox water: a blue slab on each part scaled by its fill, a gauge bar beside each tank, a pulsing red
    // marker on flooding cells, and markers for leaves, sandbags and the valve's open side.
    // Replaced by shaders and particles in the art phase.
    public class WaterVisualizer : MonoBehaviour
    {
        private static readonly Color WaterColor = new Color(0.2f, 0.55f, 1f);
        private static readonly Color FloodColor = new Color(1f, 0.15f, 0.1f);
        private static readonly Color LeafColor = new Color(0.4f, 0.55f, 0.1f);
        private static readonly Color SandbagColor = new Color(0.8f, 0.7f, 0.45f);
        private static readonly Color ValveColor = new Color(1f, 0.85f, 0.1f);

        private LevelView _view;
        private readonly List<CellState> _states = new List<CellState>();
        private readonly List<Dictionary<(int, int), GameObject>> _layers = new List<Dictionary<(int, int), GameObject>>();
        private Dictionary<(int, int), GameObject> _water, _flood, _leaf, _sandbag, _valve;

        public void Init(LevelView view)
        {
            _view = view;
            _water = NewLayer();
            _flood = NewLayer();
            _leaf = NewLayer();
            _sandbag = NewLayer();
            _valve = NewLayer();
        }

        private Dictionary<(int, int), GameObject> NewLayer()
        {
            var layer = new Dictionary<(int, int), GameObject>();
            _layers.Add(layer);
            return layer;
        }

        public void Render(WaterSimulator sim)
        {
            sim.GetStates(_states);
            float pulse = Mathf.PingPong(Time.time * 2f, 1f);
            foreach (var s in _states)
            {
                float ratio = s.Capacity > 0f ? Mathf.Clamp01(s.Water / s.Capacity) : 0f;
                var top = _view.CellTop(s.X, s.Z);
                var key = (s.X, s.Z);

                var water = Show(_water, key, PrimitiveType.Cube, WaterColor, s.Water > 0.05f);
                if (water != null)
                {
                    if (s.IsTank)
                    {
                        float h = Mathf.Max(0.02f, 1.2f * ratio);
                        water.transform.localScale = new Vector3(0.15f, h, 0.15f);
                        water.transform.position = top + new Vector3(0.6f, h * 0.5f, 0f);
                    }
                    else
                    {
                        float h = 0.03f + 0.12f * ratio;
                        water.transform.localScale = new Vector3(0.22f, h, 0.22f);
                        water.transform.position = top + new Vector3(0f, 0.17f + h * 0.5f, 0f);
                    }
                }

                var flood = Show(_flood, key, PrimitiveType.Sphere, FloodColor, s.Flooded);
                if (flood != null)
                {
                    flood.transform.localScale = Vector3.one * (0.3f + 0.12f * pulse);
                    flood.transform.position = top + new Vector3(0f, 0.8f, 0f);
                }

                var leaf = Show(_leaf, key, PrimitiveType.Cube, LeafColor, s.Clogged);
                if (leaf != null)
                {
                    leaf.transform.localScale = new Vector3(0.45f, 0.18f + 0.06f * pulse, 0.45f);
                    leaf.transform.position = top + new Vector3(0f, 0.3f, 0f);
                }

                var sandbag = Show(_sandbag, key, PrimitiveType.Cube, SandbagColor, s.Sandbagged);
                if (sandbag != null)
                {
                    sandbag.transform.localScale = new Vector3(0.6f, 0.14f, 0.6f);
                    sandbag.transform.position = top + new Vector3(0f, 0.5f, 0f);
                }

                var valve = Show(_valve, key, PrimitiveType.Sphere, ValveColor, s.IsValve);
                if (valve != null)
                {
                    valve.transform.localScale = Vector3.one * 0.2f;
                    valve.transform.position = top + new Vector3(s.ValveOutlet.OffsetX() * 0.44f, 0.3f, s.ValveOutlet.OffsetZ() * 0.44f);
                }
            }
        }

        public void Clear()
        {
            foreach (var layer in _layers)
            {
                foreach (var go in layer.Values) Destroy(go);
                layer.Clear();
            }
        }

        // Returns the marker when it should be visible, otherwise hides it and returns null.
        private GameObject Show(Dictionary<(int, int), GameObject> layer, (int, int) key, PrimitiveType shape, Color color, bool visible)
        {
            if (!layer.TryGetValue(key, out var go))
            {
                if (!visible) return null;
                layer[key] = go = _view.CreateMarker(shape, color);
            }
            go.SetActive(visible);
            return visible ? go : null;
        }
    }
}
