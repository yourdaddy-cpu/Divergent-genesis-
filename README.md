# Divergent Genesis

An open-world, voxel-block survival adventure for **Android**, built in **Unity 6 + C#**.

A **140 × 140 km** continent, centred on the world origin, streamed in chunks as you walk.
The horizon never ends, but the game never loads the whole world either.

> **Status: Milestone 1 (playable core) is feature-complete and type-checks cleanly.
> The APK is produced by GitHub Actions — see [Building the APK](#building-the-apk).**

---

## Table of contents

- [What it is](#what-it-is)
- [How the 140 km world actually works](#how-the-140-km-world-actually-works)
- [Controls](#controls)
- [Performance & device targets](#performance--device-targets)
- [Project layout](#project-layout)
- [Running the type-check without Unity](#running-the-type-check-without-unity)
- [Running it in the Unity Editor](#running-it-in-the-unity-editor)
- [Building the APK](#building-the-apk)
- [Saves](#saves)
- [Design notes](#design-notes)
- [Roadmap](#roadmap)

---

## What it is

**Divergent Genesis** is a first-person open-world survival game on a single enormous,
deterministically generated landmass. There is no lobby, no loading screen between
biomes, and no "end" — you walk, and the world builds itself around you.

Milestone 1 ships the full playable loop:

| System | What you get |
| --- | --- |
| **World** | 140 × 140 km, seed-deterministic, 18 biomes, rivers, lakes, beaches, mountains, ore veins |
| **Streaming** | 32 m voxel chunks + 128/256/512/1024 m height-field LOD tiles, budgeted per frame |
| **Render distance** | Selectable **1 × 1 km** and **2 × 2 km** (plus 512 m and 4 km) from the settings panel |
| **Player** | Walk, sprint, crouch, jump, swim, gravity, fall damage, health/hunger/breath |
| **Touch controls** | Left virtual joystick, right look pad, jump / fly / sprint buttons |
| **Building** | Break and place any of 40 blocks, with a wireframe block highlight and break-progress overlay |
| **Inventory** | 9-slot hotbar + 27-slot backpack, drag & drop, 100 items, procedurally drawn 32×32 icons |
| **Blocks** | 43 block types (40 placeable — bedrock, water and lava are world-only) |
| **Crafting** | 33 shaped recipes, crafting tables, furnaces, smelting |
| **Tools** | Pickaxe / axe / shovel / sword across wood → stone → iron → diamond tiers |
| **Decor** | Instanced trees, rocks, cacti, flowers and grass, density per biome |
| **Sky** | Full day/night cycle, sun & moon, stars, animated clouds, colour-graded fog, day/night tinting |
| **World border** | 420 m energy wall at ±70 km with proximity warnings |
| **Audio** | 100 % procedural — footsteps, block break/place, jumps, water, ambience, music stingers. No asset downloads |
| **Saving** | Seed, player state, inventory, every block edit and time of day, as JSON |

Nothing in this list is a placeholder. There are no greybox cubes standing in for
final art — the meshes, materials, icons, UI and audio are all generated at runtime
in code from the block palette.

---

## How the 140 km world actually works

Loading 140 km of voxels is impossible on a 4 GB phone, and it would be pointless —
you can only see a few hundred metres. So the world is stored as a **seed**, and
rebuilt around the player as they move.

**Generation is a pure function of `(seed, x, z)`.** No world data is ever saved
except the blocks *you* changed. Walk 5 km away, come back, and the mountains are
still exactly where you left them.

Streaming uses two representations of the same world, swapped by distance:

```
      32 m        128 m        256 m        512 m       1024 m      4 km
  ┌──────────┬───────────┬───────────┬───────────┬───────────┬─────────┐
  │  voxels  │ height-   │ height-   │ height-   │ height-   │ height- │
  │ (full    │ field     │ field     │ field     │ field     │ field   │
  │ blocks,  │ 16×16     │ 8×8       │ 4×4       │ 2×2       │ 1×1     │
  │ caves,   │           │           │           │           │         │
  │ water    │           │           │           │           │         │
  │ volume)  │           │           │           │           │         │
  └──────────┴───────────┴───────────┴───────────┴───────────┴─────────┘
   break /  visual LOD only - smoother silhouettes, no block edits
   place
```

- **Near field (voxels).** 32 × 32 × 160 block chunks with full 3D data. Caves,
  overhangs, water volumes and every player edit live here. The chunk *is* the
  collision and interaction geometry.
- **Far field (height field).** The same terrain sampled down to a single height
  column per vertex, with a smooth-min between neighbours so LOD seams don't
  stair-step. Flat-shaded, vertex-coloured, unlit-detail. Cheap enough to draw
  4 km.
- **Budgeted.** Generation runs on background worker threads; only a few meshes are
  uploaded per frame (`MaxChunksBuiltPerFrame` in the quality profile), so streaming
  never causes a frame spike while you walk.
- **Upgrades and downgrades.** Walk towards a far tile and it silently upgrades to
  a real chunk. Walk away and it downgrades to a height tile. Edits are mirrored
  into LOD tiles where they fall inside the near field.
- **Deterministic fallback.** If you somehow reach a block that isn't loaded,
  `TerrainGenerator` recomputes it from the seed. There is no "missing chunk".

`ChunkManager.RaycastBlocks` is a voxel DDA (Amanatides & Woo), so the block
under your crosshair is exact and instant regardless of streaming state.

---

## Controls

### Touch (default on Android)

| Control | Action |
| --- | --- |
| Left thumb zone | Virtual joystick — move |
| Right side drag | Look |
| **JUMP** | Jump / swim up |
| **FLY** | Toggle creative flight (also disables fall damage) |
| **RUN** | Hold to sprint |
| Tap a hotbar slot | Select it |
| Crosshair + drag | Break blocks (hold) / place blocks (tap) |
| ⬆ button | Open inventory, crafting and settings |

### Keyboard / mouse (Editor only, `EditorInput.cs`)

`WASD` move · `Space` jump / fly up · `Shift` sprint · `Ctrl` crouch / fly down ·
`Mouse 1` break (hold) · `Mouse 2` place · `1`–`9` hotbar · `E` inventory ·
`F` fly · `F3` debug overlay

---

## Performance & device targets

The floor is **4 GB RAM, MediaTek Dimensity 6500-class** (and Helio G-series
equivalents). `DeviceProbe` reads system memory, core count, the GPU name and
whether Vulkan is in use, scores them, and picks the starting tier — a 4 GB
Dimensity 6500 lands on **Low**. You can override it in Settings → Graphics and
the choice is remembered. If you want a guaranteed starting point, set
`QualityTierOverride` on the `GameBootstrap` object in the scene.

| Profile | View distance | Target | Voxel radius | Decor | Workers | For |
| --- | --- | --- | --- | --- | --- | --- |
| Potato | 1 km | 30 fps | 2 chunks (64 m) | trees only, no grass | 1 | Battery saver / very low end |
| Low | 1 km | 30 fps | 3 chunks (96 m) | 40 % | 2 | **4 GB, Dimensity 6500** |
| Medium | 2 km | 30 fps | 4 chunks (128 m) | 70 % | 2 | 6 GB, mid range |
| High | 2 km | 60 fps | 5 chunks (160 m) | 100 % | 3 | 8 GB, Dimensity 9000+ |
| Ultra | 4 km | 60 fps | 6 chunks (192 m) | 120 % | 4 | 12–16 GB flagships |

A 32 × 32 × 160 chunk is **160 KB** of `byte` block ids — a whole 160 m cube of
the world costs less than a single 4K texture, and the game ships no textures at
all. Meshing is greedy-ish per face with
face culling against neighbours, and only *visible* faces are ever written, so a
solid stone chunk is thousands of times cheaper to mesh than a forested one.

Scales up cleanly: the same streaming code with a bigger `LevelMaxDist` array
covers 16 GB devices, because the cost of the far field is bounded by tile count,
not by world size.

---

## Project layout

```
divergent-genesis/
├── Assets/
│   ├── Editor/
│   │   ├── DGBuildScript.cs        Android build entry point for GameCI
│   │   └── DGProjectSetup.cs       creates Main.unity, normalises ProjectSettings
│   ├── Resources/Shaders/          DGTerrain, DGWater, DGFoliage, DGSky
│   ├── Scenes/Main.unity           one GameObject, one component: GameBootstrap
│   └── Scripts/
│       ├── Core/                   WorldConfig, deterministic hashing/PRNG, noise
│       ├── World/                  biomes, blocks, edits, coordinates, terrain,
│       │                           chunk data, mesher, worker pool, streaming
│       ├── Render/                 MaterialLibrary (runtime shader ownership)
│       ├── Decor/                  primitive geometry, instanced props, grass
│       ├── Items/                  item database, inventory + recipe model
│       ├── Player/                 input, controller, stats, camera, interaction
│       ├── Audio/                  procedural sound synthesis
│       ├── Environment/            sky, day/night, fog, world border
│       ├── UI/                     UGUI factory, touch controls, HUD, panels
│       ├── Save/                   JSON world + player persistence
│       └── GameBootstrap.cs        the one component that assembles everything
├── ProjectSettings/                Unity 6000.0.23f1, Android IL2CPP ARM64
├── Packages/manifest.json          UGUI only — no URP, no HDRP, no package bloat
├── Tools/                         headless type-check + project validator
└── .github/workflows/              GameCI v4 → APK artifact
```

**Built-in Render Pipeline** with four hand-written shaders under `Resources/`.
No URP assets, no render-feature plumbing, no post-processing stack — the shaders
do HDR-friendly tone mapping, hemispheric ambient, per-vertex biome tinting and
distance fog themselves, which is both faster and more predictable on mobile
than stacking pipeline assets.

**All UI is code-built.** There isn't a single prefab or Canvas asset. `UIFactory`
constructs every rect, label, button and panel in C#, which keeps the repository
free of binary scene/prefab drift and makes the whole HUD diff-able in git.

---

## Running the type-check without Unity

The repository contains a small harness that compiles **every gameplay and editor
script** against a hand-written Unity API stub, plus a validator for the
hand-maintained Unity YAML that a C# compile cannot see. It needs only the .NET 8
SDK and Python 3 — no Unity, no licence, a few seconds.

```bash
./Tools/verify.sh
```

That runs two gates:

1. **Type-check** — `dotnet build Tools/CompileCheck/DgCheck.csproj`.
2. **Structure** — `python3 Tools/validate_project.py`, which verifies that
   `Main.unity` still points at the real `GameBootstrap` GUID, that every field
   the scene serialises still exists on the script (and vice versa), that
   `EditorBuildSettings` matches the scene's `.meta`, that no `.asset` file has a
   duplicate YAML key, and that every asset has a `.meta` with no orphans.

This is also the **first gate in CI**, so a signature mismatch or a broken
reference is caught in seconds instead of after a 40-minute Android build. Every
stub signature is written to match real Unity 6; if the stub and Unity ever
disagree, the stub is the bug.

`Tools/normalize_assets.py` fixes the class of bug the structure check looks for
(duplicate keys in `ProjectSettings.asset`) and is safe to re-run.

---

## Running it in the Unity Editor

1. Open **Unity Hub → Install → Unity 6000.0.23f1** (the exact version in
   `ProjectSettings/ProjectVersion.txt`).
2. **Open →** select this folder.
3. The first import resolves packages and imports the four shaders. Wait for it.
4. Open `Assets/Scenes/Main.unity` and press **Play**.

`Assets/Scenes/Main.unity` is committed and already contains the single
`GameBootstrap` GameObject, with the inspector fields you can tune:

| Field | Meaning |
| --- | --- |
| `DefaultSeed` | Seed for a brand new world. `0` picks one at random. Ignored when a save exists. |
| `QualityTierOverride` | `-1` detects from the device, `0`–`4` forces Potato → Ultra. |
| `AutosaveSeconds` | Seconds between autosaves (minimum 5). |
| `AutosaveOnPause` | Save when the app is backgrounded or loses focus. |
| `SaveOnQuit` | Save on application quit. |
| `ShowLoadingScreen` | Off = hold the player still until the first tiles are meshed, instead of showing the loading bar. |
| `EditorControls` | Keyboard/mouse input. Editor builds only. |

`DGProjectSetup` still runs on import and again from the build method: it
re-applies every Android player setting and rebuilds the scene from scratch if
it has gone missing, so a fresh clone is always in a buildable state.

---

## Building the APK

Pushing to `main` (or any of `main` / `master` / `development`, or hitting
**Run workflow**) triggers `.github/workflows/android-apk.yml`:

1. **`typecheck`** — `./Tools/CompileCheck/verify.sh`, ~10 s, no licence needed.
2. **`build-android`** — GameCI v4 installs Unity 6000.0.23f1, activates the
   licence, and calls `DivergentGenesis.Editor.DGBuildScript.BuildAndroid`.
3. The APK is uploaded as the **`divergent-genesis-apk`** artifact
   (retained 30 days).

**Download it from GitHub → your repo → Actions → the green run → Artifacts →
`divergent-genesis-apk` → `divergent-genesis.apk`.** Transfer it to the phone
and allow "install from unknown sources".

You can also pick the export type per run: `androidPackage` (APK, for sideloading)
or `androidAppBundle` (AAB, for the Play Store).

### Required repository secrets

| Secret | Required? | Notes |
| --- | --- | --- |
| `UNITY_LICENSE` | **Yes** | Personal (free) licence. One-time activation via <https://license.unity3d.com/manual>, then paste the `.ulf` contents into the secret. |
| `UNITY_EMAIL` | Personal only | Account email. |
| `UNITY_PASSWORD` | Personal only | Account password. |
| `UNITY_SERIAL` | Professional only | `XXXX-XXXX-XXXX-XXXX` serial, if your licence requires it. |

### Build settings baked into the APK

| Setting | Value |
| --- | --- |
| Unity | 6000.0.23f1 |
| Scripting backend | IL2CPP, **ARM64 only** (32-bit ARM doubles build time for a target we dropped) |
| Minimum SDK | 24 (Android 7.0) |
| Target SDK | 35 (Android 15) |
| Graphics API | Unity default — OpenGLES3, Vulkan where available |
| Orientation | **Landscape only**; autorotation off |
| Package name | `com.divergentgenesis.game` |
| Color space | Linear, `vSyncCount 0` (frame pacing is driven by `Application.targetFrameRate`) |
| GC | Incremental |
| Texture compression | Irrelevant — the game ships **no textures**; every surface is vertex-coloured |

---

## Saves

Written as JSON under `Application.persistentDataPath` (on Android,
`/storage/emulated/0/Android/data/com.divergentgenesis.game/files/`):

```json
{
  "version": 1,
  "seed": 123456789,
  "timeOfDay": 0.37,
  "player": { "x": -1204.5, "y": 71.0, "z": 88.25, "yaw": 1.9, "pitch": -0.1,
              "health": 100, "hunger": 100, "breath": 100, "onGround": true },
  "inventory": [ { "slot": 0, "item": 84, "count": 1, "durability": 59 } ],
  "edits": [ { "x": -1204, "y": 70, "z": 88, "block": 11 } ]
}
```

`edits` only ever contains blocks **you** changed. Everything else is recomputed
from `seed`, which is why a save file for a 140 km world stays a few hundred KB
after hours of building. Autosave runs every 45 s and on pause/quit.

---

## Design notes

**Why the world is procedural rather than stored.** 140 km × 140 km × 160 blocks
of voxels is ~3.1 trillion blocks. At one byte each that's 3.1 TB. The only
sane design is "the world is a function of the seed", with player edits as a
sparse overlay — which is what this does.

**Why two LOD representations instead of one.** Pure voxel chunks at 32 m would
need ~5,000 draw calls to see 2 km. Pure height fields can't be mined. Splitting
into a near voxel field and a far height field means you get *exact* block
interaction where you can actually reach, and cheap silhouettes everywhere else.
The two are generated from the same column sampler, so they always agree.

**Why no URP.** On a Dimensity 6500, every extra render-feature pass is frame
time. A single surface shader that does lighting, fog and tone mapping in one pass
is measurably cheaper than the Built-in pipeline plus post-processing stack, and
the entire lighting model is legible in one file.

**Why everything is procedural.** Trees, rocks, grass, item icons, UI chrome and
all audio are generated in code from the block palette. The APK stays small
(the shader-only build is a few MB, not hundreds), and adding a new block
automatically gives you its icon, its sound, its particles and its inventory entry.

**Why the code is checked without Unity.** A Unity install is a 15 GB liability in
CI and a licence problem in review. The stub harness gives 90 % of the safety of
a real compile in 10 seconds of setup, and it runs on every push.

---

## Roadmap

See [ROADMAP.md](ROADMAP.md). Milestone 1 is done; the next milestones are
villages and NPCs, animals and birds, deeper ore/crafting, mechanisms, and dragons.

---

## Licence

Code is yours. Unity 6 Personal is fine for this project; the IL2CPP Android
build stays within Personal's revenue limit as long as the game is free.
