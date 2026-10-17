"""Python mirror of Assets/_Project/Scripts/Water/WaterSimulator.cs, used only to design and balance levels.
The C# simulator and its Unity tests are the source of truth; keep this in step when the C# rules change."""
import math

TICK = 0.1
RAIN = 3.0
CAP_TICK = 4.0
STORAGE = 10.0
FLOOD_TICKS = 30
DRAIN_TICKS = 100
LEAF_FIRST = 50
LEAF_INTERVAL = 100
TARP_REACH = 1
EPS = 1e-4
ONE_STAR_SHARE = 0.4  # relaxed score: 1 star for 40% of the target

DX = [0, 1, 0, -1]  # N E S W
DZ = [1, 0, -1, 0]
BASE_PORTS = {"gutter_straight": [0, 2], "elbow_90": [0, 1], "downpipe": [0, 2], "t_junction": [1, 2, 3],
              "tarp": [2], "valve": [1, 2, 3]}


def rot(d, r): return (d + r) % 4
def opp(d): return (d + 2) % 4


class Part:
    def __init__(self, t, x, z, r):
        self.type, self.x, self.z, self.rot = t, x, z, r % 4

    def ports(self): return [rot(p, self.rot) for p in BASE_PORTS[self.type]]
    def is_outlet(self, d): return self.type == "downpipe" and d == rot(2, self.rot)
    def valve_inlet(self): return rot(2, self.rot)


class Level:
    def __init__(self, data):
        self.data = data
        self.cells = {(c["x"], c["z"]): c for c in data["cells"]}
        self.fixed = {(f["x"], f["z"]): f for f in data["fixedObjects"]}

    def height(self, x, z):
        c = self.cells.get((x, z))
        return c["h"] if c else -1

    def has_tank(self, x, z):
        f = self.fixed.get((x, z))
        return f is not None and f["type"] == "tank"


def can_place(level, parts, t, x, z):
    if (x, z) not in level.cells or (x, z) in level.fixed: return False
    return all((p.x, p.z) != (x, z) for p in parts)


def part_at(parts, x, z):
    for p in parts:
        if p.x == x and p.z == z: return p
    return None


def heights_ok(ha, aout, hb, bout):
    if ha == hb: return not aout and not bout
    if ha > hb: return aout and not bout
    return bout and not aout


def port_connected(level, parts, p, d):
    nx, nz = p.x + DX[d], p.z + DZ[d]
    ha, hb = level.height(p.x, p.z), level.height(nx, nz)
    if hb < 0: return False
    other_out = False
    if not level.has_tank(nx, nz):
        nb = part_at(parts, nx, nz)
        if nb is None or opp(d) not in nb.ports(): return False
        other_out = nb.is_outlet(opp(d))
    return heights_ok(ha, p.is_outlet(d), hb, other_out)


class Node:
    def __init__(self, x, z, part=None, tank_cap=None):
        self.x, self.z, self.part = x, z, part
        self.is_tank = tank_cap is not None
        self.cap = tank_cap if self.is_tank else STORAGE
        self.water = self.pending = 0.0
        self.dist = -1
        self.full = 0
        self.flooded = self.reported = self.clogged = self.sandbag = False
        self.exits, self.next = [], []
        self.valve_out = rot(1, part.rot) if part and part.type == "valve" else None


class Sim:
    def __init__(self, level, parts, sandbags=0):
        self.level, self.parts = level, parts
        self.nodes, self.by = [], {}
        for f in level.fixed.values():
            if f["type"] != "tank": continue
            n = Node(f["x"], f["z"], tank_cap=f["capacityL"]); self.by[(n.x, n.z)] = n; self.nodes.append(n)
        for p in parts:
            n = Node(p.x, p.z, part=p); self.by[(n.x, n.z)] = n; self.nodes.append(n)
        self.nodes.sort(key=lambda n: (n.x, n.z))
        self.total_rain = self.overflow = self.runoff = 0.0
        self.ever_flooded = self.clogs = self.leaf_ticks = 0
        self.sandbags = sandbags
        self.on_clog = []
        self._graph(); self._rain_targets()

    def _graph(self):
        incoming = {}
        for u in self.nodes:
            p = u.part
            if p is None: continue
            for d in p.ports():
                if p.type == "downpipe" and not p.is_outlet(d): continue
                if p.type == "valve" and d == p.valve_inlet(): continue
                if not port_connected(self.level, self.parts, p, d): continue
                v = self.by.get((u.x + DX[d], u.z + DZ[d]))
                if v is None: continue
                if v.part is not None:
                    if v.part.is_outlet(opp(d)): continue
                    if v.part.type == "valve" and opp(d) != v.part.valve_inlet(): continue
                u.exits.append((d, v)); incoming.setdefault(id(v), []).append(u)
        q = [n for n in self.nodes if n.is_tank]
        for n in q: n.dist = 0
        i = 0
        while i < len(q):
            v = q[i]; i += 1
            for u in incoming.get(id(v), []):
                if u.dist >= 0: continue
                u.dist = v.dist + 1; q.append(u)
        for u in self.nodes:
            if u.dist < 0: continue
            u.next = [v for (_, v) in u.exits if v.dist == u.dist - 1]

    def _rain_targets(self):
        self.targets = []
        tarps = [n for n in self.nodes if n.part and n.part.type == "tarp"]
        for key in sorted(self.level.cells):
            c = self.level.cells[key]
            if c["surface"] == "ground": continue
            own = self.by.get(key)
            if own:
                if not own.is_tank: self.targets.append(own)
                continue
            target = None
            for t in tarps:
                if abs(t.x - c["x"]) <= TARP_REACH and abs(t.z - c["z"]) <= TARP_REACH and self.level.height(t.x, t.z) == c["h"]:
                    target = t; break
            if target is None:
                for d in (0, 1, 2, 3):
                    n = self.by.get((c["x"] + DX[d], c["z"] + DZ[d]))
                    if n is None or n.is_tank or n.part.type in ("downpipe", "tarp"): continue
                    if self.level.height(n.x, n.z) != c["h"]: continue
                    target = n; break
            self.targets.append(target)

    def step(self, intensity, leaves=False):
        if leaves: self._leaves()
        rain = RAIN * intensity * TICK
        for t in self.targets:
            self.total_rain += rain
            if t is None: self.runoff += rain
            else: t.water += rain
        for n in self.nodes:
            if n.is_tank or n.clogged or n.water <= EPS: continue
            amt = min(n.water, CAP_TICK)
            if n.valve_out is not None:
                tgt = next((v for (d, v) in n.exits if d == n.valve_out and v.dist >= 0), None)
                if tgt is None: continue
                n.pending -= amt; tgt.pending += amt; continue
            if not n.next: continue
            share = amt / len(n.next)
            n.pending -= amt
            for v in n.next: v.pending += share
        for n in self.nodes:
            n.water += n.pending; n.pending = 0.0
        for n in self.nodes:
            if n.is_tank:
                if n.water > n.cap: self.overflow += n.water - n.cap; n.water = n.cap
                continue
            over = n.water > n.cap
            if over: self.overflow += n.water - n.cap; n.water = n.cap
            if n.sandbag: continue
            n.full = n.full + 1 if over else 0
            should = n.full >= FLOOD_TICKS
            if should and not n.flooded: n.flooded = True; self.ever_flooded += 1
            elif not should and n.flooded: n.flooded = False

    def _leaves(self):
        self.leaf_ticks += 1
        lt = self.leaf_ticks
        if not (lt == LEAF_FIRST or (lt > LEAF_FIRST and (lt - LEAF_FIRST) % LEAF_INTERVAL == 0)): return
        cands = [n for n in self.nodes if n.part and not n.clogged and n.dist >= 0
                 and n.part.type in ("gutter_straight", "elbow_90", "t_junction")]
        if not cands: return
        c = cands[(self.clogs * 3) % len(cands)]
        c.clogged = True; self.clogs += 1
        for cb in self.on_clog: cb(c)

    @property
    def tank_litres(self): return sum(n.water for n in self.nodes if n.is_tank)


def stars_for(litres, flooded, goals):
    if litres >= goals["star3"] - 0.01 and flooded <= goals["maxFloodedCells"]: return 3
    if litres >= goals["star2"]: return 2
    if litres >= goals["litresTarget"] * ONE_STAR_SHARE: return 1
    return 0


def run(data, parts, tap_delay=None):
    """tap_delay=None: nobody taps (the solvability test). Otherwise clogs are cleared that many ticks later."""
    level = Level(data)
    sim = Sim(level, parts, data.get("sandbags", 0))
    pending = []
    if tap_delay is not None:
        sim.on_clog.append(lambda n: pending.append([n, tap_delay]))
    for w in data["waves"]:
        leaves = "leaves" in (w.get("hazards") or [])
        for _ in range(int(round(w["durationSec"] / TICK))):
            sim.step(w["intensity"], leaves)
            for item in pending:
                item[1] -= 1
                if item[1] <= 0: item[0].clogged = False
            pending[:] = [i for i in pending if i[1] > 0]
    for _ in range(DRAIN_TICKS): sim.step(0.0)
    return sim
