# Divergent Genesis — Roadmap

Everything in **Milestone 1** is implemented and type-checks cleanly. The sections
below are what comes next, in build order. Each milestone is meant to be playable
on its own, not a greybox.

---

## Milestone 1 — Playable core ✅ COMPLETE

The bar was "a genuinely playable game", not terrain and a fly camera.

- [x] 140 × 140 km deterministic world, centred coordinates, ±70 km
- [x] Chunk streaming: 32 m voxel chunks + 128/256/512/1024 m height-field LOD
- [x] Budgeted generation on worker threads, per-frame build caps
- [x] LOD upgrade/downgrade on approach and retreat, edit mirroring into LOD tiles
- [x] Selectable render distance: 512 m / **1 km** / **2 km** / 4 km
- [x] 18 biomes — deep ocean → snow peaks, with biome-blended transitions
- [x] Rivers carved by an FBM-warped channel field, lakes, beaches
- [x] Terrain: continentalness / erosion / ridge noise, caves, ore veins by depth
- [x] Player: walk, sprint, crouch, jump, swim, gravity, exact voxel AABB collision
- [x] Fall damage, drowning, health / hunger / breath
- [x] Touch: virtual joystick, look pad, jump / fly / run buttons
- [x] Break and place all 43 blocks, wireframe highlight, break progress
- [x] 9-slot hotbar + 27-slot backpack, drag & drop, stack splitting
- [x] 100 items, 34 shaped recipes, crafting table requirement
- [x] Tool tiers wood → stone → iron → diamond, correct mining speeds and drops
- [x] Instanced trees, rocks, cacti, flowers, grass, density per biome
- [x] Day/night cycle, sun, moon, stars, clouds, graded fog
- [x] 420 m world border at ±70 km with proximity warnings
- [x] Procedural audio: footsteps per material, break/place, jump, water, ambience
- [x] Procedural 32×32 item icons and full code-built UGUI
- [x] JSON saves: seed, player, inventory, edits, time of day, + autosave
- [x] Five quality profiles auto-selected by device (`DeviceProbe`: RAM, cores, GPU, Vulkan), overridable in Settings
- [x] Android IL2CPP ARM64 build, min SDK 24, target SDK 35, GameCI APK artifact
- [x] Headless type-check harness in CI as a first gate, plus a project-structure validator

---

## Milestone 2 — Living world

The world currently reacts to weather and light. This milestone makes it react to
*you*.

### Villages
- [ ] Structure generation: house footprints placed on flat, non-flooded, temperate ground
- [ ] Village layout pass — roads, plots, well placement, biome-aware architecture
- [ ] Static villagers (idle, wander, look-around) with simple navigation on the voxel surface
- [ ] Day schedule: sleep indoors at night, work during the day, shelter in rain
- [ ] Loot containers, beds (sleep to skip night), crafting tables in houses
- [ ] Village sizes and densities per biome, minimum separation between villages

### NPCs
- [ ] Persistent entities streamed like chunks — spawn near, despawn far
- [ ] Navigation: A* over the voxel surface with jump and step-up
- [ ] Schedules, path memory, avoidance of other NPCs
- [ ] Trading: villagers exchange emeralds-equivalent currency for rare items
- [ ] Reputation system — hostile, neutral, friendly by player standing
- [ ] Idle chatter, follow, flee-from-player behaviours

### Animals & birds
- [ ] Herds of passive mobs (deer, sheep, cattle, pigs) with boids-style cohesion
- [ ] Spawn rules per biome, light level, and time of day
- [ ] Flee/chase AI, drops, breeding
- [ ] Predators that hunt the player *and* the herds
- [ ] Bird flocks: boid simulation, altitude-following terrain, nesting sites
- [ ] Procedural quadruped and bird rigs animated in code (no rig assets)

### Environment life
- [ ] Fish shoals in rivers and lakes, ambient underwater audio
- [ ] Butterflies and insects near flowers, swarms near swamps
- [ ] Weather: rain, thunderstorms, snow in cold biomes, with particles and audio
- [ ] Crops that grow over real time, withered by neglect

---

## Milestone 3 — Depth

### Mining & progression
- [ ] Ore distribution rework — depth bands, rarity, world-generation seed sensitivity
- [ ] Tool enchantments / tiers above diamond
- [ ] Mining speed, tool durability and drop-rate balance pass
- [ ] Structure-generated ore veins and geodes underground

### Crafting
- [ ] Smelting with real furnace UI, fuel values, cook times
- [ ] Brewing stand and potions
- [ ] Beds, doors, chests, ladders, buckets, scaffolding
- [ ] Decorative blocks, slabs, stairs, and coloured variants
- [ ] Anvil and repair
- [ ] Redstone-adjacent mechanics without full redstone: levers, buttons, pressure plates, doors, traps

### Survival pressure
- [ ] Hunger drain that actually matters; food spoilage
- [ ] Farming loop: wheat → bread → farming tables
- [ ] Cold and heat by biome and time of day
- [ ] Fall, drowning, fire and mob damage properly balanced

### World generation depth
- [ ] Caves that connect, with proper lighting and mob spawn rules
- [ ] Ruins, dungeons, mineshafts as generated structures
- [ ] Amethyst geodes, dripstone caves, lush caves, deep dark
- [ ] Better river continuity — currently rivers can dead-end in basins

---

## Milestone 4 — Mechanisms

- [ ] Redstone-lite: power levels, dust, repeaters, comparators, lamps
- [ ] Pistons, hoppers, observers, droppers
- [ ] Doors, trapdoors, buttons, levers, pressure plates
- [ ] Water and lava flow as real sim blocks (not just static water volumes)
- [ ] Fire spreading, TNT, mob griefing rules
- [ ] Piston contraptions, flying machines, farms players can build

---

## Milestone 5 — Dragons

The centrepiece. Not "a bat with a health bar" — a boss with real phases.

- [ ] Flight model: banking, stall speed, altitude hold, thermalling
- [ ] Multi-segment procedural body, neck and tail IK chains
- [ ] Wing membrane simulation — cloth-like, reacts to velocity and turns
- [ ] Breath attack: cone of fire with particle dissipation and terrain damage
- [ ] Staged encounters in arena structures (storm, magma, end-island variants)
- [ ] Phase behaviour: aerial strafing → perched ground assault → desperation nova
- [ ] Weak points: destroy the wing membranes to force a crash
- [ ] Summonable with an End Crystal, and the ultimate crafting goal behind it
- [ ] Boss health bar, music, and a proper victory sequence
- [ ] Three biomes, three colour/material variants, one with a unique mechanic

---

## Infrastructure

- [x] `Main.unity` checked in, with a validator that fails CI if it drifts from
      `GameBootstrap` (missing/renamed fields) or loses its GUID
- [x] Deterministic `.meta` GUIDs derived from asset paths, so a fresh clone
      imports identically on every machine
- [x] Device-tier detection from RAM / cores / GPU / API instead of a first-run guess
- [ ] Frame-time graph and chunk-streaming telemetry overlay (behind a debug flag)
- [ ] Unity Test Framework coverage for generation determinism, inventory, recipes, saves
- [ ] A/B the quality profiles on real hardware and lock in the defaults
- [ ] Localisation (the UI strings are already centralised in `UIFactory`/`ItemDatabase`)
- [ ] Gamepad support as an alternative to touch

---

## Explicit non-goals

- Full infinite world — the 140 km border is a design feature, not a limitation to
  remove. "Feels infinite" is the requirement; being literally unbounded is not.
- Uncapped voxel streaming. The near voxel field stays small on purpose; the far
  field is height-based by design.
- URP/HDRP. The Built-in pipeline with four hand-written shaders is a deliberate
  performance decision for 4 GB devices, not a shortcut.
