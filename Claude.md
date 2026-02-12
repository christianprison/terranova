# Terranova – Project Briefing for AI Developer Agent

> This file is the primary context for Claude Code working on Terranova.
> Last updated: February 2026 | Based on GDD v0.8

---

## What is Terranova?

Terranova is a **real-time strategy/economy simulation** for tablets (iPad, M4+) where the player guides a civilization through 29 epochs – from stone tool culture to a speculative post-biological future – on a procedurally generated voxel planet.

**Elevator pitch:** Empire Earth's epoch system + Minecraft's voxel terrain + Anno/Settlers' economic chains + RimWorld's emergent storytelling.

### Design Pillars (in priority order)

1. **Building Fascination ("Wuselfaktor")** – Individual settlers autonomously work, trade, and live. The player watches, optimizes, and guides indirectly. The joy of seeing a civilization grow from a campfire to a metropolis.
2. **Strategic Depth Through Terrain** – The voxel world is not backdrop but strategy. Where you build, which biome you settle, how you shape terrain – all have real consequences on build costs and research direction.
3. **Epoch Progression** – Advancing through epochs must feel like a civilizational leap. New possibilities, new aesthetics, new strategies. Even the way research works evolves over time.

### What Terranova is NOT

- Not a combat-focused RTS (combat is emergent from self-defense/hunting, no dedicated system)
- Not a city builder (individual settlers, not abstract population)
- Not turn-based (real-time with pause)
- Not multiplayer (singleplayer first, MP as future expansion)

---

## Technical Stack

| Aspect | Decision |
|--------|----------|
| Engine | Unity (LTS – version TBD, either 2022 LTS or Unity 6) |
| Language | C# |
| Render Pipeline | URP (Universal Render Pipeline) |
| Primary Platform | iPad (M4 processor or higher) |
| Future Platforms | Meta Quest 3 (MR multiplayer – far future) |
| Input | Touch primary, Apple Pencil optional, mouse/keyboard for dev |
| Voxel System | Custom chunk-mesh system (not an off-the-shelf solution) |
| Networking | Singleplayer first. Architecture must allow future multiplayer. |

---

## Core Systems Overview

### 1. World Geometry: Goldberg Polyhedron

The planet is a **Goldberg polyhedron** – a body of pentagons and hexagons approximating a sphere. Each facet (pentagon or hexagon) is internally flat and contains a chunk-based voxel terrain. Planet shape emerges at macro level through angles between facets.

- **Planet size** scales via polyhedron resolution: GP(1,0) = 32 facets (quick game) to GP(4,0)+ = 162+ facets (planetary scale)
- **Each facet = one biome**
- **Facet edge length = view distance = 12 chunks (192m)**
- **Zoom levels:** Close (gameplay, flat facet) → Medium (LOD for neighbors) → Planetary (facets as colored areas)
- **Coordinate system:** Local 2D per facet, global facet ID, Mercator projection at edges

> **For Vertical Slice:** Start with a SINGLE flat facet. Polyhedron integration is a later milestone.

### 2. Voxel System

- Block size: 1×1×1 meter
- Chunk size: 16×16×256 blocks
- View distance: 12 chunks each direction (192m)
- Terrain height: 0–256 blocks (sea level at block 64)

### 3. Biomes (20 types)

Each facet is assigned one biome at world generation. Biomes determine available resources, possible discoveries, and strategic options.

**Climate zones (latitude-based):**
- Polar: Glacier, Tundra
- Subpolar: Taiga, Fjord
- Temperate: Forest, Grassland, River Valley, Karst, High Plateau
- Subtropical: Steppe, Savanna, Desert, Mountains
- Tropical: Rainforest, Mangroves, Desert

**Full biome list:** Grassland, Forest, Desert, Tundra, Mountains, Ocean, Coast, Rainforest, Steppe, Volcanic, Savanna, Swamp/Moor, Taiga, Mangroves, High Plateau, River Valley, Coral Reef, Glacier, Karst, Fjord.

### 4. Epochs (29 in 4 Eras)

| Era | Epochs | Theme |
|-----|--------|-------|
| I: Early History | 10 (I.1–I.10) | Stone tools → Weaving. No active research – discoveries are emergent. |
| II: Antiquity & Pre-Modern | 8 (II.1–II.8) | Wheel → Printing press. Active research becomes possible (from II.3). |
| III: Industry & Modern | 8 (III.1–III.8) | Steam → AI/Robotics. Systematic science. |
| IV: Speculative Future | 3 (IV.1–IV.3) | AGI → Post-Biological. Far future, not yet designed. |

**Epoch transitions** are fluid: not triggered by one key technology but by a **threshold of thematically matching discoveries**. Different discovery combinations can trigger the same transition.

### 5. Research & Discoveries (Probabilistic)

**There is NO tech tree.** Instead, research evolves over the eras:

- **Era I:** No active research. Discoveries happen through observation, imitation, trial-and-error. Two types: **biome-driven** (what's in the environment) and **activity-driven** (what settlers do). From I.3, cave paintings act as knowledge multiplier.
- **Era II+ (from Writing):** Player can assign research areas. Results remain probabilistic.
- **Knowledge multipliers:** Cave painting (I.3) → Oral tradition (I.3) → Writing (II.3) → Printing press (II.8) → Internet (III.7)

**"Bad luck protection":** Guaranteed discovery after X activity cycles (exact value via playtesting).

### 6. Resources & Economy

Resources form a **dependency tree**. Root = gatherable resources (stones, sticks, berries, herbs, water, clay, plant fibers, shells, feathers – biome-dependent). Everything else requires combinations of: Knowledge (discovery), Tools, Buildings, Specialized workers, Other resources.

**Example chain (Era I):**
```
Sticks + Stones → (Discovery: Composite Tool) → Stone Axe
Stone Axe + Trees → Wood (Logs)
Wood + (Discovery: Fire) → Charcoal
Wild Animal + Hunter with Spear → Meat + Hide
Hide + (Discovery: Leatherworking) + Tool → Leather
```

Goods are physically transported by individual settlers. Paths, distances, and storage capacities are strategically relevant.

### 7. Population System

- **Indirect control only.** Player never commands settlers directly. Influence through building placement, job training, priorities, infrastructure, equipment, guidelines.
- **Individual settlers** with: Profession, skills (improve over time), needs, age, health, satisfaction, knowledge.
- **Life cycle:** Birth → Child (can't work, learns from adults) → Adult → Old (slower, more knowledge) → Death (age, hunger, disease, accidents, wild animals).
- **Knowledge inheritance:** Parents pass skills/knowledge to children. If experienced settler dies before passing knowledge, it's lost (until cave paintings from I.3).
- **Population cap:** Era I–II: max ~100 individual settlers. Era III+: population group abstraction (TBD).

### 8. Terraforming

Three atomic operations on voxel level:
- **Remove** (dig, mine, quarry, level hills)
- **Add** (build walls, dams, landfill)
- **Transform** (earth → farmland, sand → foundation, rock → tunnel wall)

Same system used by player actions, building operations, and events (earthquakes, GenAI).

**Terrain affects build costs:** Flat = standard cost. Slope = terracing needed, higher cost. Rock = excavation needed, much higher cost. Swamp/water = drainage needed, special materials.

### 9. Combat

No dedicated combat system. Conflict behavior emerges from self-defense and hunting. Every settler defends themselves when attacked. Neighbors rush to help. Coordinated hunting techniques (Era I) evolve into military strategy in later eras.

### 10. Touch Controls (Gesture Lexicon v0.4)

Panel-driven single-object model with building-as-group-proxy. 22 gestures total, 73% simple taps. Key patterns:
- **Camera:** 1-finger drag (pan), 2-finger pinch (zoom), toggle+drag (rotate)
- **Selection:** Tap object → panel → action
- **Commands split:** Free actions (move) = direct tap on target. Costly actions (build, research) = preview + OK/Cancel.
- **Building:** Center-screen ghost + camera pan to position + optional 90° rotation + build loop for serial construction.

---

## Architecture Principles

### Code Quality

- **Simplicity over cleverness.** The producer is a C# beginner. Code must be readable, well-commented, and easy to understand.
- **One class, one responsibility.** Keep scripts focused and small (<200 lines ideally).
- **Use comments generously.** Explain the "why", not just the "what". Write all comments and code in English.
- **No premature optimization.** Make it work → make it right → make it fast.
- **Document decisions.** When choosing between approaches, add a comment explaining why.

### Unity-Specific

- **Use ScriptableObjects for data definitions:**
  - `BiomeDefinition` – biome properties, resource lists, discovery modifiers
  - `EpochDefinition` – epoch thresholds, unlocked buildings/features
  - `ResourceDefinition` – resource properties, dependency chain
  - `BuildingDefinition` – building stats, costs, terrain footprint, `needsRotation` flag
  - `DiscoveryDefinition` – discovery conditions, probabilities, effects
- **Prefer composition over inheritance.** Use Unity's component system as intended.
- **Use namespaces.** All code under `Terranova.*`:
  - `Terranova.Core` – Game loop, epoch manager, event bus, save/load
  - `Terranova.World` – Goldberg polyhedron, facet management, coordinate transforms
  - `Terranova.Terrain` – Voxel system, chunk generation/rendering, biome terrain features
  - `Terranova.Economy` – Resources, production chains, storage, transport
  - `Terranova.Buildings` – Building placement, construction, operation, terraforming integration
  - `Terranova.Population` – Settler AI, needs, lifecycle, knowledge, professions
  - `Terranova.Research` – Discovery system, probabilities, knowledge multipliers, epoch transitions
  - `Terranova.Terraforming` – Three operations (remove/add/transform), cost calculation, preview
  - `Terranova.Camera` – RTS camera, zoom levels, touch input
  - `Terranova.Input` – Gesture state machine, touch handling, command flows
  - `Terranova.UI` – HUD layers, panels, build menu, resource display
  - `Terranova.Audio` – Sound management, biome ambience, SFX
  - `Terranova.Events` – Event bus, spontaneous discoveries, weather, future GenAI integration
- **Use Assembly Definitions** for each namespace to keep compilation fast.
- **Follow Unity naming conventions:**
  - PascalCase for classes, methods, properties
  - camelCase for local variables and parameters
  - _camelCase for private fields
  - UPPER_SNAKE_CASE for constants

### Project Structure

```
Assets/
├── Terranova/
│   ├── Scripts/
│   │   ├── Core/
│   │   ├── World/
│   │   ├── Terrain/
│   │   ├── Economy/
│   │   ├── Buildings/
│   │   ├── Population/
│   │   ├── Research/
│   │   ├── Terraforming/
│   │   ├── Camera/
│   │   ├── Input/
│   │   ├── UI/
│   │   ├── Audio/
│   │   └── Events/
│   ├── Data/                    ← ScriptableObjects
│   │   ├── Biomes/
│   │   ├── Epochs/
│   │   ├── Resources/
│   │   ├── Buildings/
│   │   └── Discoveries/
│   ├── Prefabs/
│   ├── Materials/
│   ├── Scenes/
│   ├── Audio/
│   │   ├── SFX/
│   │   │   ├── Ambience/
│   │   │   ├── Resources/
│   │   │   ├── Building/
│   │   │   ├── UI/
│   │   │   ├── Units/
│   │   │   └── Weather/
│   │   └── Music/
│   │       └── Stingers/
│   └── Tests/
│       ├── EditMode/
│       └── PlayMode/
├── Plugins/
└── ThirdParty/
```

### Key Architectural Patterns

- **Event Bus** for system communication (settler discovered something → research system → UI notification → epoch check). This same bus will later support GenAI events.
- **ScriptableObject-driven definitions** make balancing possible without code changes.
- **Separation: Simulation vs. Presentation.** The simulation (economy, population, research) should work without rendering. This enables: headless testing, future multiplayer, and future server-side simulation of distant facets.

### Testing

- **Edit Mode tests** for: economy calculations, discovery probabilities, epoch transition logic, resource dependency validation, terraforming cost calculations.
- **Play Mode tests** for: building placement, terrain generation, settler pathfinding, camera controls.
- Every new system should have at least basic test coverage.

---

## Current Milestone: MS1 – Technical Foundation

**Goal:** Prove that the voxel terrain and basic interaction work.

### Acceptance Criteria

- [ ] Flat voxel terrain generates and renders (single facet, single biome, min. 64×64 blocks)
- [ ] Multiple voxel types visible (grass, stone, water, sand)
- [ ] RTS camera works (pan, zoom, rotate) – mouse/keyboard first, touch later
- [ ] A single building type (campfire) can be placed on terrain via click
- [ ] Terrain-aware placement (building snaps to surface, rejects water)
- [ ] Basic resource counter in UI (wood, stone – just numbers, no gathering yet)

### What is NOT in this milestone

- Settlers / population
- Economy / gathering
- Research / discoveries
- Multiple biomes or Goldberg polyhedron
- Touch input (use mouse/keyboard for faster iteration)
- Audio
- Save/Load

### After MS1, the next milestones are:

- **MS2: Living World** – Settler AI, resource gathering, 3–5 buildings
- **MS3: Discovery** – Research system (5–8 discoveries), Epoch I.1 complete
- **MS4: Vertical Slice** – Second biome, epoch transition I.1→I.2, terraforming, touch input
- **MS5: Demo** – Polish, full UI, sound, tutorial, save/load

See `docs/roadmap.md` for full details.

---

## How to Work With Me

The human producer is learning C# and Unity. When writing code:

1. **Always explain what you're building** before writing code. A brief summary helps the producer understand the approach.
2. **Keep changes focused.** One feature or fix per session.
3. **If you see multiple valid approaches**, present them briefly with trade-offs and let the producer decide.
4. **If something is risky or experimental**, flag it clearly.
5. **Write code that teaches.** The producer learns from reading your output.
6. **Reference the GDD.** The design documents in `docs/` are the source of truth. If a design decision is unclear, ask rather than assume.
7. **Flag scope creep.** If implementing something "right" would take significantly longer than a simpler version, say so and suggest the simpler version for now.

---

## Reference Documents

| Document | Content |
|----------|---------|
| `docs/gdd-terranova.md` | Main Game Design Document (v0.8) |
| `docs/epochs.md` | All 29 epochs, transition mechanics |
| `docs/biomes.md` | 20 biome types, distribution rules, discovery influence |
| `docs/research.md` | Research system, discoveries per epoch |
| `docs/terraforming.md` | Unified terraforming mechanics, build cost coupling |
| `docs/terranova-gesture-lexicon-v04.md` | Complete touch control specification |

---

## QA Rule
After completing each feature, perform a self-review before moving on.
Check for: GC allocations in Update loops, null safety, memory leaks, 
magic numbers, and event-bus architecture compliance.

---

## Working with the Task Board
- when done with an issue, find the next one by looking in the ToDo column for the top item of type "Story" in the column "ToDo"
- plan the implementation work by creating sub-issues of type "Task" for the given story
- then set the story and the first task to "In Progress"
- finished tasks can be set to done directly
- when all tasks of a story are done, write a comment into the story about what has been done
- then plan the QA work by creating more sub-issues of type "Task" for the given story. Use the acceptance criteria as an orientation for creating the QA tasks
- then set the story and the first (new) task to "QA"
- finished tasks can be set to done
- when all QA tasks are done, the story can also be set to done
- repeat the process until all stories of a feature are done
- then inform me so I can do the testing in Unity
