"""Generates Worlds 1-2 level JSON (levels 1-12) with stored reference solutions and balanced star goals.

Run:  python3 -I tools/make_levels.py
Writes Assets/_Project/Resources/Levels/*.json and index.json. Goals come from simulating the reference
solution (tools/levelsim.py mirrors the C# simulator). Unity's LevelSolvabilityTests re-checks every level."""
import json, math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from levelsim import Part, Level, can_place, run, stars_for, port_connected

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "_Project", "Resources", "Levels")
TAP_DELAY = 20  # a player clears leaves about 2 s after they land


class L:
    def __init__(self, id_, name, world, w, d):
        self.id, self.name, self.world, self.w, self.d = id_, name, world, w, d
        self.cells, self.fixed, self.parts, self.waves = {}, [], {}, []
        self.solution, self.tutorial, self.sandbags = [], [], 0

    def rect(self, x0, x1, z0, z1, h, surface="flat_roof"):
        for x in range(x0, x1 + 1):
            for z in range(z0, z1 + 1):
                self.cells[(x, z)] = {"x": x, "z": z, "h": h, "surface": surface}
        return self

    def hole(self, x0, x1, z0, z1):
        for x in range(x0, x1 + 1):
            for z in range(z0, z1 + 1):
                self.cells.pop((x, z), None)
        return self

    def tank(self, x, z, cap=2000, platform=None):
        if platform is not None:  # a stand for the tank at roof height
            self.cells[(x, z)] = {"x": x, "z": z, "h": platform, "surface": "ground"}
        h = self.cells[(x, z)]["h"]
        self.fixed.append({"type": "tank", "x": x, "z": z, "h": h, "capacityL": cap})
        return self

    def wave(self, sec, intensity, label, leaves=False):
        self.waves.append({"durationSec": sec, "intensity": intensity, "label": label,
                           "hazards": ["leaves"] if leaves else []})
        return self

    def sol(self, t, x, z, r=0):
        self.solution.append({"type": t, "x": x, "z": z, "rot": r})
        return self

    def tut(self, text, point="none", advance="none", cell=None):
        self.tutorial.append({"text": text, "point": point, "cell": cell or [], "advance": advance})
        return self


levels = []

# ---------------- World 1: The Outskirts ----------------
l = L("w1_l01", "The Basics", 1, 6, 3).rect(0, 4, 0, 2, 1).tank(5, 1, platform=1)
l.parts = {"gutter_straight": 5}
l.wave(20, 0.4, "Light Drizzle").wave(25, 0.6, "Steady Rain")
for x in range(1, 5): l.sol("gutter_straight", x, 1, 1)
l.tut("Tap the Straight Gutter in the tray", "tray:gutter_straight", "arm")
l.tut("Now tap the roof next to the tank", "cell", "place", [4, 1])
l.tut("Tap the gutter to turn it towards the tank", "cell", "rotate", [4, 1])
l.tut("Add more gutters in a line. Green dots = connected", "none", "place")
l.tut("Tap Test Drizzle to watch the rain flow (free)", "button:drizzle", "drizzle")
l.tut("Happy? Tap Start Wave", "button:start", "start")
levels.append(l)

l = L("w1_l02", "Around the Corner", 1, 5, 5).rect(0, 4, 0, 3, 1).tank(4, 4, platform=1)
l.parts = {"gutter_straight": 5, "elbow_90": 1}
l.wave(20, 0.4, "Light Drizzle").wave(25, 0.7, "Steady Rain")
for x in range(0, 4): l.sol("gutter_straight", x, 2, 1)
l.sol("elbow_90", 4, 2, 3).sol("gutter_straight", 4, 3, 0)
l.tut("The Elbow turns water round a corner", "tray:elbow_90", "place")
l.tut("Tap it to turn it until both ends join the gutters", "none", "rotate")
levels.append(l)

l = L("w1_l03", "Down the Wall", 1, 7, 4).rect(0, 3, 0, 3, 2).rect(4, 6, 0, 3, 0, "ground").tank(5, 1)
l.parts = {"gutter_straight": 5, "elbow_90": 1, "downpipe": 1}
l.wave(20, 0.4, "Light Drizzle").wave(30, 0.7, "Heavy Rain")
for x in range(0, 3): l.sol("gutter_straight", x, 2, 1)
l.sol("downpipe", 3, 2, 3).sol("gutter_straight", 4, 2, 1).sol("elbow_90", 5, 2, 2)
l.tut("Water can't jump off a roof. Use a Downpipe on the roof edge", "tray:downpipe", "place")
l.tut("Its brown end drops water to the ground. Turn it to face away from the roof", "none", "rotate")
levels.append(l)

l = L("w1_l04", "Two Roofs", 1, 8, 4).rect(0, 2, 0, 3, 2).rect(3, 5, 0, 3, 1).rect(6, 7, 0, 3, 0, "ground").tank(7, 2)
l.parts = {"gutter_straight": 6, "elbow_90": 1, "downpipe": 2}
l.wave(20, 0.5, "Light Rain").wave(30, 0.8, "Heavy Rain")
l.sol("gutter_straight", 0, 2, 1).sol("gutter_straight", 1, 2, 1).sol("downpipe", 2, 2, 3)
l.sol("gutter_straight", 3, 2, 1).sol("gutter_straight", 4, 2, 1).sol("downpipe", 5, 2, 3)
l.sol("gutter_straight", 6, 2, 1)
levels.append(l)

l = (L("w1_l05", "Outskirts Showcase", 1, 9, 6).rect(0, 3, 0, 3, 2).rect(4, 6, 0, 3, 1)
     .rect(7, 8, 0, 3, 0, "ground").rect(0, 8, 4, 5, 0, "ground").tank(8, 4, 4000))
l.parts = {"gutter_straight": 7, "elbow_90": 3, "downpipe": 2}
l.wave(20, 0.5, "Light Rain").wave(25, 0.6, "Steady Rain").wave(20, 0.65, "Heavy Rain")
for x in range(0, 3): l.sol("gutter_straight", x, 1, 1)
l.sol("downpipe", 3, 1, 3).sol("gutter_straight", 4, 1, 1).sol("gutter_straight", 5, 1, 1)
l.sol("elbow_90", 6, 1, 3).sol("gutter_straight", 6, 2, 0).sol("downpipe", 6, 3, 2)
l.sol("elbow_90", 6, 4, 1).sol("gutter_straight", 7, 4, 1)
levels.append(l)

# ---------------- World 2: The Crowded Courtyard ----------------
l = L("w2_l01", "Under the Tarp", 2, 7, 5).rect(0, 5, 0, 4, 1).tank(6, 2, platform=1)
l.parts = {"tarp": 1, "gutter_straight": 4}
l.wave(25, 0.5, "Steady Rain").wave(25, 0.8, "Heavy Rain")
l.sol("tarp", 2, 2, 3)
for x in range(3, 6): l.sol("gutter_straight", x, 2, 1)
l.tut("A Tarp catches rain from the 3x3 area around it", "tray:tarp", "place")
l.tut("Turn it so its spout points at a gutter", "none", "rotate")
levels.append(l)

l = L("w2_l02", "Split the Flow", 2, 5, 5).rect(0, 4, 0, 4, 1).tank(1, 4, 300).tank(3, 4, 300)
l.parts = {"gutter_straight": 5, "t_junction": 1, "elbow_90": 2}
l.wave(25, 0.5, "Steady Rain").wave(25, 0.8, "Heavy Rain")
for z in range(0, 4): l.sol("gutter_straight", 2, z, 0)
l.sol("t_junction", 2, 4, 0)
# the T sits between the two tanks: (2,4) must be a roof cell
l.tut("Each tank holds 300 L. The T-Junction splits water between two sides", "tray:t_junction", "place")
levels.append(l)

l = L("w2_l03", "Falling Leaves", 2, 7, 4).rect(0, 5, 0, 3, 1).tank(6, 1, platform=1)
l.parts = {"gutter_straight": 6, "elbow_90": 2}
l.sandbags = 1
l.wave(15, 0.5, "Drizzle").wave(40, 0.7, "Leaf Storm", leaves=True)
for x in range(0, 6): l.sol("gutter_straight", x, 1, 1)
l.tut("Leaves fall in the second wave. Build your route, then Start Wave", "button:start", "start")
l.tut("Leaves block pipes! Tap the green block to clear it", "none", "clear")
levels.append(l)

l = (L("w2_l04", "The Courtyard", 2, 7, 6).rect(0, 6, 0, 5, 1).hole(2, 4, 2, 3)
     .rect(2, 4, 2, 3, 0, "ground").tank(3, 2))
l.parts = {"gutter_straight": 5, "elbow_90": 2, "downpipe": 2, "t_junction": 1}
l.wave(25, 0.6, "Steady Rain").wave(30, 0.9, "Downpour")
for x in range(0, 3): l.sol("gutter_straight", x, 5, 1)
l.sol("elbow_90", 3, 5, 2).sol("downpipe", 3, 4, 0).sol("gutter_straight", 3, 3, 0)
levels.append(l)

l = L("w2_l05", "Tarp and Split", 2, 7, 5).rect(0, 6, 0, 4, 1).tank(1, 3, 300).tank(5, 3, 300)
l.parts = {"tarp": 1, "gutter_straight": 4, "t_junction": 1, "elbow_90": 2}
l.wave(25, 0.6, "Steady Rain").wave(30, 0.9, "Downpour")
l.sol("tarp", 3, 1, 2).sol("gutter_straight", 3, 2, 0).sol("t_junction", 3, 3, 0)
l.sol("gutter_straight", 2, 3, 1).sol("gutter_straight", 4, 3, 1)
levels.append(l)

l = L("w2_l06", "Leaf Season", 2, 8, 5).rect(0, 4, 0, 4, 2).rect(5, 7, 0, 4, 0, "ground").tank(7, 2)
l.parts = {"gutter_straight": 7, "elbow_90": 2, "downpipe": 1, "tarp": 1}
l.sandbags = 2
l.wave(15, 0.5, "Drizzle").wave(30, 0.8, "Leaf Storm", leaves=True).wave(20, 1.0, "Downpour", leaves=True)
for x in range(0, 4): l.sol("gutter_straight", x, 2, 1)
l.sol("downpipe", 4, 2, 3).sol("gutter_straight", 5, 2, 1).sol("gutter_straight", 6, 2, 1)
levels.append(l)

l = (L("w2_l07", "Crowded Courtyard", 2, 9, 7).rect(0, 3, 0, 6, 2).rect(4, 8, 0, 6, 0, "ground")
     .tank(8, 1, 600).tank(8, 5, 600))
l.parts = {"gutter_straight": 10, "elbow_90": 3, "downpipe": 2, "t_junction": 1, "tarp": 1}
l.sandbags = 2
l.wave(20, 0.6, "Steady Rain").wave(30, 0.9, "Leaf Storm", leaves=True).wave(20, 1.0, "Downpour", leaves=True)
l.sol("tarp", 1, 3, 3).sol("gutter_straight", 2, 3, 1).sol("downpipe", 3, 3, 3).sol("t_junction", 4, 3, 1)
l.sol("gutter_straight", 4, 4, 0).sol("elbow_90", 4, 5, 1)
for x in range(5, 8): l.sol("gutter_straight", x, 5, 1)
l.sol("gutter_straight", 4, 2, 0).sol("elbow_90", 4, 1, 0)
for x in range(5, 8): l.sol("gutter_straight", x, 1, 1)
levels.append(l)


def to_json(l, goals):
    return {"id": l.id, "world": l.world, "name": l.name,
            "grid": {"width": l.w, "depth": l.d, "maxHeight": 4},
            "cells": [l.cells[k] for k in sorted(l.cells)], "fixedObjects": l.fixed,
            "availableParts": [{"type": k, "count": v} for k, v in l.parts.items()],
            "waves": l.waves, "neighbourRequests": [], "goals": goals, "sandbags": l.sandbags,
            "solution": l.solution, "tutorial": l.tutorial}


def check(l):
    data = to_json(l, None)
    level = Level(data)
    parts, used = [], {}
    for s in l.solution:
        assert can_place(level, parts, s["type"], s["x"], s["z"]), (l.id, "cannot place", s)
        used[s["type"]] = used.get(s["type"], 0) + 1
        assert used[s["type"]] <= l.parts.get(s["type"], 0), (l.id, "over the part limit", s["type"])
        parts.append(Part(s["type"], s["x"], s["z"], s["rot"]))
    loose = [(p.type, p.x, p.z, d) for p in parts for d in p.ports() if not port_connected(level, parts, p, d)]
    perfect = run(data, parts, TAP_DELAY)
    lazy = run(data, parts, None)
    star3 = math.floor(perfect.tank_litres * 0.9 / 10) * 10
    goals = {"litresTarget": star3, "star2": math.floor(perfect.tank_litres * 0.7 / 10) * 10,
             "star3": star3, "maxFloodedCells": 1}
    s_perfect = stars_for(perfect.tank_litres, perfect.ever_flooded, goals)
    s_lazy = stars_for(lazy.tank_litres, lazy.ever_flooded, goals)
    print(f"{l.id:7} {l.name:22} perfect {perfect.tank_litres:7.1f}L flood {perfect.ever_flooded}"
          f" -> {s_perfect}*   no-taps {lazy.tank_litres:7.1f}L flood {lazy.ever_flooded} -> {s_lazy}*"
          f"   goals {goals['star2']}/{goals['star3']}   open ports {len(loose)}")
    assert s_lazy >= 1, (l.id, "reference solution without taps must reach 1 star")
    assert s_perfect == 3, (l.id, "reference solution with taps should reach 3 stars")
    return goals


index = {"worlds": [{"name": "The Outskirts", "levels": []}, {"name": "The Crowded Courtyard", "levels": []}],
         "testLevels": ["test_tarp", "test_leaves", "test_valve", "test_sandbag"]}
for l in levels:
    goals = check(l)
    with open(os.path.join(OUT, l.id + ".json"), "w") as f:
        json.dump(to_json(l, goals), f, indent=1)
    index["worlds"][l.world - 1]["levels"].append(l.id)
with open(os.path.join(OUT, "index.json"), "w") as f:
    json.dump(index, f, indent=1)
print("wrote", len(levels), "levels")
