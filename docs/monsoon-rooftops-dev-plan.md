# Monsoon Rooftops — Development Plan

> **For Claude Code:** This file is the master plan for building *Monsoon Rooftops*, a free-to-play, ad-supported 3D mobile puzzle game for Android (Google Play). Work through it **phase by phase**. Before starting any phase, read the whole phase, **ask the developer clarifying questions about anything ambiguous, and wait for answers before writing code.** At the end of each phase, check every acceptance criterion and report which pass.

---

## 1. Game Summary

**Pitch:** A relaxing-but-suspenseful 3D puzzle game set on the rooftops of a crowded Indian neighbourhood during the monsoon. In the **Prepare phase**, the player places gutters, pipes, tarps and junctions on a snapping grid. In the **Storm phase**, rain falls in waves and the player watches the water flow, tapping to clear clogs, open gates and drop sandbags. The goal is to fill the water tanks, keep homes from flooding, and help the neighbours.

**Core loop:**
```
Level select → PREPARE (place & rotate parts) → STORM (waves of rain, light tap interactions)
→ RESULTS (litres collected, homes saved, neighbour tasks, coins/gears) → Upgrades → next level
```

**Target audience:** Men and women aged 35–40. Busy, play in short gaps (commute, breaks, before bed).

**Design rules that follow from the audience (do not break these):**
- Levels take **2–5 minutes**. No long sessions required.
- **No reflex-based failure.** Storm-phase taps are helpful, not twitchy. Mistakes reduce the score; they rarely end the level.
- **Relaxed Score** instead of hard fail: the player can always finish a level. Stars (1–3) reward better solutions.
- Large, readable UI text (minimum 14sp equivalent), high contrast, clear icons with labels.
- Works **fully offline**. Ads load when online and are simply skipped when not.
- Warm, cosy art and sound. Rain should feel soothing, not stressful.

---

## 2. Monetization: Free with Ads

The game is free. Revenue comes from ads, placed so they never interrupt play.

| Ad type | Where | Rules |
|---|---|---|
| **Rewarded video** (main revenue) | Results screen: "Double your coins"; Prepare phase: "Get 1 extra sandbag / extra part"; Level fail-soft: "Retry this wave"; Hint button: "Show one correct pipe" | Always optional. The player chooses to watch. Reward granted only on completion callback. |
| **Interstitial** | Between levels only (after Results, before next level loads) | Never before level 5. Max 1 per 3 levels, minimum 3 minutes apart. Never during Prepare or Storm. |
| **Banner** | Main menu and level-select map only | Never on gameplay screens. |

Requirements:
- SDK: **Google Mobile Ads (AdMob) Unity plugin**, with mediation optional later. Verify the latest plugin version at build time.
- **Consent:** integrate Google's **User Messaging Platform (UMP)** for consent before requesting ads.
- All ad calls go through one `AdService` interface so the SDK can be swapped or mocked in tests and in the editor.
- Use **test ad unit IDs** in all development builds. Real IDs only in release config.
- Optional later: a one-time "Remove Ads" in-app purchase (keeps rewarded ads available as opt-in).

---

## 3. Tech Stack

| Area | Choice | Notes |
|---|---|---|
| Engine | **Unity 6 LTS** | Best Android tooling and ad SDK support. Confirm the exact LTS version with the developer. |
| Render pipeline | **URP** (Universal Render Pipeline) | Mobile-friendly. |
| Language | C# | |
| Input | Unity Input System | Touch: tap, drag, rotate (two-finger or rotate button). |
| Camera | Isometric-style perspective camera, pinch-zoom and pan, clamped | Matches the reference art. |
| Data | ScriptableObjects for parts and upgrades; JSON for level layouts | JSON lets levels be edited without recompiling. |
| Save | Local JSON save file (`Application.persistentDataPath`) | Offline-first. Cloud save is optional later. |
| Analytics | Firebase Analytics (optional, Phase 9) | Level start/complete, ad watched, drop-off points. |
| Ads | Google Mobile Ads Unity plugin + UMP | See section 2. |
| Version control | Git, with Git LFS for art/audio | |

**Orientation:** Landscape (matches the reference mockups). Confirm with the developer before Phase 1.

**Performance budget (mid/low-end Android, common in India):**
- Target 60 FPS, minimum stable 30 FPS on a 3–4 GB RAM device.
- Install size under 150 MB.
- Per level: under 100k triangles, under 100 draw calls (use static batching and texture atlases).
- Rain and splashes use pooled particle systems, never instantiate per drop.

---

## 4. Core Architecture

### 4.1 Folder structure
```
Assets/
  _Project/
    Scripts/
      Core/          GameStateMachine, GameManager, ServiceLocator
      Grid/          GridSystem, GridCell, SnapController
      Parts/         PartDefinition (SO), PartInstance, PortConnector
      Water/         WaterSimulator, WaterNode, RainEmitter, FlowVisualizer
      Storm/         StormDirector, WaveDefinition, Hazards/
      Interaction/   TapInteractions (clog, gate, sandbag), DragRotate
      Neighbours/    NeighbourAgent, NeighbourRequest
      Economy/       CurrencyWallet, UpgradeDefinition (SO), UpgradeService
      Levels/        LevelLoader, LevelData (JSON schema), LevelSelectMap
      UI/            HUD, PartsTray, TaskBoard, ResultsScreen, UpgradeShop
      Ads/           IAdService, AdMobService, MockAdService, AdPolicy
      Save/          SaveService, SaveData
      Audio/         AudioService
    Art/  Audio/  Prefabs/  Scenes/  ScriptableObjects/
    Levels/          world1_level01.json ...
  Tests/
    EditMode/        WaterSimulator, Grid, Economy, AdPolicy tests
    PlayMode/
```

### 4.2 Game state machine
```
Boot → Consent(UMP) → MainMenu → LevelSelect → Prepare → Storm → Results → (Interstitial?) → LevelSelect
```
Each state is a class with `Enter()`, `Tick()`, `Exit()`. No game logic in UI scripts.

### 4.3 Water simulation (most important system)

**Do not use real fluid physics.** Use a **deterministic, grid-based flow simulation**, with visuals layered on top. This keeps it fast on low-end phones, makes levels solvable and testable, and gives the same result every time.

- The level is a 3D grid of cells. Each cell has a **height level** (roof tier) and a **surface type** (flat roof, sloped tile, tin sheet, ground, tank).
- **Rain** adds water to each exposed cell each tick, according to the current wave's intensity.
- Each **part** (gutter, elbow, T-junction, tarp, downpipe, splitter) defines **input ports** and **output ports** on its cell edges, plus a **capacity per tick**.
- Each simulation tick (fixed 10 Hz, independent of frame rate):
  1. Add rain to exposed cells and tarps (a tarp catches rain over its whole footprint).
  2. Sloped surfaces push water downhill to their lower edge.
  3. Parts move water from input ports to connected output ports, up to capacity. Splitters divide evenly. Excess becomes **overflow** in that cell.
  4. Water flows downward between tiers through downpipes or by spilling.
  5. Tanks accumulate water up to their capacity.
  6. Cells holding water above a threshold for N ticks become **Flooding** (red warning marker). Fragile tiles crack after M ticks.
- Expose events: `OnTankFilled`, `OnCellFlooding`, `OnCellDrained`, `OnPartOverflow`, `OnClogged`.
- **Visual layer** (`FlowVisualizer`) reads simulation state and drives water shaders, waterfall particles, puddle meshes and tank fill gauges, interpolated between ticks for smoothness.
- Must be **fully unit-testable without a scene**: given a level JSON plus placed parts plus a wave sequence, the result is deterministic.

### 4.4 Level data (JSON)
```json
{
  "id": "w2_l03",
  "world": 2,
  "name": "The Crowded Courtyard",
  "grid": { "width": 12, "depth": 10, "maxHeight": 4 },
  "cells": [ { "x": 0, "z": 0, "h": 2, "surface": "flat_roof" } ],
  "fixedObjects": [ { "type": "tank", "x": 3, "z": 8, "h": 0, "capacityL": 250 } ],
  "availableParts": { "gutter_straight": 6, "elbow_90": 3, "t_junction": 1, "tarp": 1 },
  "waves": [
    { "durationSec": 20, "intensity": 0.4, "label": "Light Drizzle" },
    { "durationSec": 25, "intensity": 1.0, "label": "Heavy Downpour", "hazards": ["leaves"] }
  ],
  "neighbourRequests": [ { "id": "laundry", "cell": [5, 2], "type": "keep_dry", "rewardCoins": 50 } ],
  "goals": { "litresTarget": 1000, "star2": 750, "star3": 1000, "maxFloodedCells": 2 }
}
```

---

## 5. Content Roadmap (Worlds and Levels)

| World | Levels | New mechanics | Hazards/twists |
|---|---|---|---|
| **1. The Outskirts** | 1–5 | Straight gutter, 90° elbow, downpipe, single tank | Gentle waves; small leaks on flat roofs |
| **2. The Crowded Courtyard** | 6–12 | Tarp (broad catchment), T-junction/splitter, multiple tanks | Leaves clog pipes (tap to clear during storm, or place a mesh guard in Prepare) |
| **3. The Mid-Rise Maze** | 13–20 | Height tiers, gravity drops, valve wheels (rotate during storm) | Fragile tiles crack if water pools too long; surge waves |
| **4. The Coastal Citadel** | 21–30 | Large maps, pinned tarps, sandbags | Wind gusts blow away unpinned tarps; sudden surge waves; more neighbours to protect |

Each world ends with a **showcase level** combining all its mechanics. Each new mechanic is introduced in a level that teaches only that mechanic, with a short interactive tutorial (hand pointer plus one sentence).

---

## 6. Interactivity Features

### Prepare phase (planning)
- **Parts tray** (right side): scrollable, shows each part with its remaining count.
- **Drag from tray to grid**: a glowing snapping grid shows valid cells; ports glow green when they connect and red when they don't fit.
- **Rotate**: a tap on a placed part rotates it 90°; an on-screen rotate button sits beside the selected part.
- **Remove**: drag back to the tray.
- **Flow preview**: a "Test drizzle" button runs 3 seconds of light rain so the player can see the routing before committing. Free, unlimited.
- **Task board** (left panel): neighbour requests for this level (for example "Save my laundry!" or "Tanks are empty!").
- **Undo** (last 10 actions).

### Storm phase (relaxed suspense)
- Wave progress gauge at the top ("Wave 2/3: Heavy Downpour").
- **Light taps only:**
  - Tap a clogged pipe to clear it (leaf particle burst).
  - Tap a valve wheel to switch the flow direction.
  - Drag a sandbag (limited count) onto a flooding cell.
  - Tap a loose tarp to pin it (World 4).
- **Fast-forward** button (2x speed) for players who are confident in their setup.
- **Pause** is always available. The storm never punishes pausing.
- Tank gauges fill live with a satisfying sound; flooding cells show pulsing red markers.

### Neighbours (living community)
- Stylised characters on rooftops and balconies with idle animations (hanging laundry, waving, kids playing).
- Speech bubbles state their needs. Fulfilling them grants bonus coins and a happy reaction (cheer, wave).
- At Results, satisfied neighbours appear in a short "thank you" moment.

### Progression and economy
- **Coins:** earned from litres collected, stars, and neighbour tasks. Spent on upgrades.
- **Gears:** earned from 3-star levels and saving all homes. Spent on rarer upgrades.
- **Upgrades** (permanent):
  - High-capacity gutters (more litres per tick)
  - Reinforced tarps (resist wind)
  - Leaf guards (reduce clogs)
  - Bigger tanks
  - Extra sandbag slot
- **Rewarded ad hooks:** double coins, +1 sandbag, hint, retry wave.
- Economy values live in ScriptableObjects so they can be tuned without code changes.

---

## 7. Development Phases

Each phase ends with a playable build. **Ask clarifying questions before starting each phase.**

### Phase 0: Project setup
- Create the Unity 6 LTS URP project, folder structure, Git plus LFS, `.gitignore`.
- Set up Android build settings (IL2CPP, ARM64, target API level per current Google Play requirement), landscape orientation.
- Add the EditMode and PlayMode test assemblies.
- **Done when:** an empty scene builds and runs on an Android device, and tests run in CI or locally.

### Phase 1: Grid and part placement (greybox)
- `GridSystem` loaded from level JSON; greybox cubes for roofs at different heights.
- Isometric camera with pan, pinch-zoom and clamping.
- Parts tray; drag-and-drop, snapping, rotating, removing; port-connection highlighting; undo.
- **Done when:** a player can build a connected gutter path on a greybox level using touch only.

### Phase 2: Water simulation (core)
- `WaterSimulator` as described in 4.3: pure C#, no MonoBehaviour dependency.
- Rain, slopes, part flow, overflow, tiers, tanks, flooding detection.
- EditMode unit tests: straight path fills tank; disconnected path floods; splitter divides evenly; capacity overflow; determinism (same input gives same output).
- Basic debug visualiser (coloured cells, numbers).
- **Done when:** all simulation tests pass and greybox level 1 is solvable.

### Phase 3: Game loop and storm
- State machine: Prepare → Storm → Results.
- `StormDirector` runs waves from JSON; wave gauge HUD; pause; fast-forward; test drizzle.
- Results screen: litres, stars, flooded cells, coins earned.
- **Done when:** a full level can be played start to finish in greybox.

### Phase 4: Storm interactions and hazards
- Clogs (leaves), valve wheels, sandbags, fragile tiles, wind and pinned tarps.
- Each hazard has its own unit tests in the simulator.
- **Done when:** each hazard works in a dedicated test level.

### Phase 5: Levels, World 1–2 content
- Level loader and level-select map with world progression and stars.
- Build levels 1–12 in JSON, with tutorials for each new mechanic.
- Add a simple **in-editor level editor tool** (place cells and fixed objects, export JSON) to speed up level creation.
- Add a **solvability check**: every shipped level must have a stored reference solution that reaches at least 1 star in the simulator test.
- **Done when:** levels 1–12 are playable and pass the solvability test.

### Phase 6: Neighbours, economy, upgrades, save
- Neighbour agents with requests and reactions.
- Coins, gears, upgrade shop, upgrade effects applied to the simulator.
- `SaveService`: progress, stars, currencies, upgrades, settings. Save on every Results screen.
- **Done when:** progress survives an app restart and upgrades change gameplay measurably.

### Phase 7: Art, animation, audio, polish
- Replace greybox with stylised low-poly art matching the reference images (warm lighting, tiled and tin roofs, green water tanks, laundry lines, satellite dishes).
- Water shaders, waterfalls, splashes, puddles, rain (pooled particles), tank fill gauges.
- Audio: soothing rain loop that scales with intensity, water trickles, tank fill chimes, neighbour voice blips, gentle music.
- Juice: part snap bounce, haptics on snap and tank full, star celebration.
- **Done when:** the game matches the target look and holds frame-rate targets on a low-end device.

### Phase 8: Ads and consent
- `IAdService` with `AdMobService` and `MockAdService`.
- UMP consent flow at first launch.
- Rewarded, interstitial and banner placements exactly as in section 2.
- `AdPolicy` class enforces frequency caps and "never before level 5" and "never during gameplay"; unit-tested.
- Graceful offline behaviour (rewarded buttons hidden or disabled when no ad is loaded).
- **Done when:** test ads show in every placement, caps are respected, and the game is fully playable offline.

### Phase 9: Analytics, settings, accessibility
- Settings: music/SFX volume, haptics toggle, text size option, colour-blind-friendly flooding markers (icon plus colour, not colour alone).
- Optional Firebase Analytics events: `level_start`, `level_complete`, `stars`, `ad_rewarded_complete`, `ad_interstitial_shown`, `quit_mid_level`.
- **Done when:** settings persist and analytics events appear in the debug view.

### Phase 10: Worlds 3–4 content
- Levels 13–30 using the level editor, each with a stored reference solution.
- Difficulty curve review: no two consecutive levels much harder than the previous.
- **Done when:** all 30 levels pass solvability tests and a full playthrough has been done.

### Phase 11: Release preparation
- App icon, feature graphic, screenshots (landscape), short and full store descriptions.
- Privacy policy (required because of ads), Play Console **Data safety** form, content rating questionnaire.
- Target audience in Play Console set to adults (not a children's app, which keeps ad rules simpler).
- Internal testing → closed testing (Google Play currently requires a closed test with a minimum number of testers for new personal developer accounts; check the current rule) → production.
- Signed release AAB, Play App Signing, real ad unit IDs.
- **Done when:** the app is approved and live on Google Play.

---

## 8. Testing Strategy
- **EditMode tests:** water simulator, grid snapping logic, economy, ad policy, save/load.
- **Level solvability tests:** every level JSON plus its reference solution must reach ≥1 star.
- **PlayMode smoke test:** load each level, run the storm at max speed, assert no exceptions.
- **Device testing:** at least one low-end Android (3–4 GB RAM) and one mid-range device each phase from Phase 7.
- **Playtesting with the target audience:** 5–10 people aged 35–40 after Phase 5 and after Phase 10. Watch for confusion points and where they stop playing.

---

## 9. Risks and Mitigations
| Risk | Mitigation |
|---|---|
| Water sim too slow or unpredictable | Grid-based deterministic sim at fixed tick, not physics. |
| Levels unsolvable or trivially easy | Reference solutions plus automated solvability tests; playtesting. |
| Ads annoy players and hurt retention | Strict ad policy class; rewarded-first; no ads during play. |
| Art takes too long | Use stylised low-poly asset packs from the Unity Asset Store as a base; greybox first. |
| Low-end device performance | Performance budget from Phase 7, pooled particles, texture atlases, profiling on real devices. |
| Scope creep | Ship Worlds 1–2 (12 levels) as a soft launch first if needed; add Worlds 3–4 as an update. |

---

## 10. Open Questions for the Developer (answer before Phase 0)
1. Confirm engine: Unity 6 LTS? (Alternative: Godot 4, but ad SDK support is weaker.)
2. Orientation: landscape (as in mockups) or portrait (easier one-handed play)?
3. Art source: buy asset packs, hire an artist, or generate placeholders first?
4. Is a soft launch with 12 levels acceptable, or must all 30 ship at once?
5. Do you want the optional "Remove Ads" purchase at launch or later?
6. Which Android device will be used for testing?
