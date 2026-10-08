# Divergent Genesis — Roadmap

Everything in **Milestone 1** and **Milestone 2** is implemented and type-checks
cleanly through the CI harness. The sections below record what shipped, in build
order, and what is still open. Each milestone is meant to be playable on its own,
not a greybox.

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
- [x] Break and place every block, wireframe highlight, break progress
- [x] 9-slot hotbar + 27-slot backpack, drag & drop, stack splitting
- [x] Tool tiers wood → stone → iron → diamond, correct mining speeds and drops
- [x] Instanced trees, rocks, cacti, flowers, grass, density per biome
- [x] Day/night cycle, sun, moon, stars, clouds, graded fog
- [x] 420 m world border at ±70 km with proximity warnings
- [x] Procedural audio: footsteps per material, break/place, jump, water, ambience
- [x] Procedural 32×32 item icons and full code-built UGUI
- [x] JSON saves: seed, player, inventory, edits, time of day, + autosave
- [x] Five quality profiles auto-selected by device (`DeviceProbe`: RAM, cores, GPU, Vulkan)
- [x] Android IL2CPP ARM64 build, min SDK 24, target SDK 35, GameCI APK artifact
- [x] Headless type-check harness in CI as a first gate, plus a project-structure validator

---

## Milestone 2 — Living world ✅ COMPLETE

The world used to react to weather and light. It now reacts to *you*.

### World furniture — `StructureGenerator`
- [x] Deterministic 320 m structure cells, hashed from the world seed, no runtime state
- [x] Site selection: rejects water, cliffs over 6.5 m, and picks a kind from the biome
- [x] Stamped during voxel generation, before player edits, so builds inside a village survive
- [x] **Villages**: levelled plot, crossroads and ring road, street lamps, 4–8 houses,
      gabled roofs, glazing, doors facing the well, beds, chests, crafting tables,
      furnaces, bookshelves, an anvil, a fenced farm plot, a watchtower with a ladder
- [x] **Bandit camps**: cleared ground, log palisade with a gate, 3–5 tents with loot,
      central campfire, watchtower, chief's chest
- [x] **Ruins**: broken hall walls, fallen columns, a chest
- [x] **Ritual sites**: stone platform, altar, four pedestals, lamp ring, rune circle
- [x] **Node shrines**: overworld portals — a stone frame with a rift in it
- [x] Prop system refuses to plant trees or boulders inside any structure

### Villagers, guards and bandits
- [x] Entity streaming: spawn near, despawn far, capped by graphics tier
- [x] Node-less navigation: voxel ground sampling, step-up, obstacle hopping, water handling
- [x] Village population by profession: villagers, farmers, smiths, scribes, guards
- [x] Day schedule: work by day, go indoors and sleep at night, wake at dawn
- [x] Guards patrol and engage anything hostile inside the village
- [x] Bandits raid villages, hunt the player, and drop coins
- [x] **Trading**: tap a villager for three offers; overworld deals in Coins, the Node in Gumdrops
- [x] Petted creatures follow the player and remember it

### Animals, birds and monsters
- [x] Herds of passive mobs with cohesion, spawn rules per biome and time of day
- [x] Skittish prey that bolts, predators that hunt the herds, nocturnal spawns
- [x] Bird flocks that follow the terrain at altitude
- [x] Hostiles: Hollows, spiders, slimes, emberlings and wisps, with projectiles
- [x] Procedural quadruped / biped / bird / blob / dragon / wisp rigs, animated in code
      (no rig assets, no animation files — every joint is driven by maths)

### Dragons
- [x] Four dragons: Ember, Frost, Storm and the Elder (the Node Sovereign)
- [x] Flight model with banking, altitude hold and thermalling
- [x] Multi-segment neck, tail and wing chains with membrane flex
- [x] Breath attack with a damage cone, cooldown and sound
- [x] Staged boss phases: aerial strafing → perched assault → desperation
- [x] Boss health bar, phase changes, and the sky healing when it dies

### Environment life
- [x] Ambient audio for day, night, weather and depth
- [ ] Fish shoals in rivers and lakes
- [ ] Butterflies and insect swarms near flowers
- [ ] Weather: rain, thunderstorms, snow in cold biomes, with particles and audio
- [ ] Crops that grow over real time, withered by neglect

---

## The Node dimension ✅ COMPLETE

A second, complete world reachable from the overworld — and the thing the whole
ritual system exists for.

- [x] `DimensionState` flips the terrain function, biome table and palette at once
- [x] Node terrain: cream seas, sherbet dunes, cotton highlands, gumdrop woodland
- [x] Four Node biomes (18–21) with pastel grass, foliage, stone and sky
- [x] Node block set: candy dirt, frosting grass, gumdrop logs and leaves, cotton
      blocks, sherbet stone, plus candy cane, marshmallow, gumdrop and cute essence items
- [x] Node structures: cute villages, gumdrop groves, floating cotton clouds, the Hollow
- [x] **Cute mobs**: Fluff, Bunbun, Cloudpup, Jellybean, Starlet, Puffcap and Nibble —
      bouncy rigs, pettable, harmless, and delighted to see you
- [x] Node Guardians defend the dimension
- [x] Travel is a real swap: edits stored per plane, chunks rebuilt, entities reset
- [x] Rifts built by the player, the shrine, or the ritual all work
- [x] No second world in memory — one plane is streamed at a time, so the Node is free

---

## The ritual & the sky ✅ COMPLETE

- [x] Craftable altar and pedestals (or use a generated ritual site)
- [x] Four pedestals each accept one *tool*, destroyed forever; the pedestal lights up
- [x] The offering is persisted in the world, so you can walk away and come back
- [x] In the overworld the altar tears open a rift into the Node
- [x] In the Node it calls **The Node Sovereign**: cyan sky → purple, then cracks open
- [x] Cracks are a procedural branching fracture field in the sky shader, re-rolled per ritual
- [x] The tear pulses, swallows light around the seams, thickens the fog, and heals on death

---

## Building & crafting ✅ COMPLETE

- [x] **26 build kits** across Comfort, Shelter, Defence, Settlement and Ritual
- [x] Kits are stamped from a block list; their material cost is *derived* from that list
- [x] Free materials: anything you can dig up costs nothing to build with
- [x] Ghost preview with a live footprint, green/red validity, and 90° rotation
- [x] Validation: affordability, flat ground, free space, and "you are standing in the way"
- [x] Blocks place over a second or two, so a house visibly rises out of the ground
- [x] **Recipe book**: every recipe in one window, grouped, tap to craft, shows what is missing
- [x] Expanded crafting tree: home kit, plaster and roof tiles, anvil, bookshelf, ladder,
      fence, campfire, lamp, bed, door, chest, glass, stone bricks, plus the Node and
      dragonbone tiers and the ritual pieces
- [ ] Smelting with a real furnace UI, fuel values and cook times
- [ ] Brewing stand and potions
- [ ] Decorative slabs, stairs and coloured variants
- [ ] Redstone-adjacent mechanics: levers, buttons, pressure plates, traps

---

## Rendering ✅ COMPLETE

- [x] **HDR post pipeline**: half-float target → threshold → separable blur → filmic
      tone map, exposure, saturation and vignette (`DGPost` + `DGPostEffect`)
- [x] Bloom that makes lamps, lava, lightning and the cracked sky actually glow
- [x] Underwater wobble and colour shift, driven from the player's head state
- [x] `DGEntity` shader: per-vertex lighting matching the terrain, plus a vertex-alpha
      emissive mask (eyes, glow sacs, lamp glass bloom) and a silhouette rim light
- [x] `DGGlow` additive shader for projectiles, wisps, breath and effects
- [x] `DGSky` gained the corruption field: purple palette, branching cracks, flare

---

## Interface ✅ COMPLETE

- [x] Dimension badge, location readout, crosshair target and context prompt
- [x] Boss health bar with name, percentage and phase feedback
- [x] Ritual progress ("2 of 4 tools given") in the status line
- [x] Build catalogue window and recipe book window, both code-built UGUI
- [x] Villager trade window
- [x] Live build status: cost, validity, rotation hint
- [x] Held item view: procedural tool and block models with idle sway, walk bob,
      mining rhythm tied to tool tier, attack arc, placement punch and equip raise

---

## Still open

### Depth
- [ ] Tool enchantments and tiers above dragonbone
- [ ] Balance pass on mining speed, durability and ore rarity
- [ ] Caves that connect, with lighting and mob spawn rules
- [ ] More generated structures: mineshafts, dungeons, geodes
- [ ] Better river continuity — rivers can still dead-end in basins

### Mechanisms
- [ ] Redstone-lite: power, dust, repeaters, comparators, lamps
- [ ] Pistons, hoppers, observers, droppers
- [ ] Water and lava flow as simulated blocks
- [ ] Fire spreading, TNT, mob griefing rules

### Infrastructure
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
- URP/HDRP. The Built-in pipeline with seven hand-written shaders is a deliberate
  performance decision for 4 GB devices, not a shortcut.
- Two worlds resident at once. The Node streams in place of the overworld, never
  alongside it.