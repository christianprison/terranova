# Terranova – Project Briefing for AI Developer Agent

## What is Terranova?

Terranova is a single-player RTS/economy simulation where the player guides a civilization from the Stone Age to the far future. The core fantasy: watch your world evolve across epochs on a living, voxel-based terrain.

**Closest reference:** Empire Earth (epoch progression) meets Minecraft (voxel terrain) meets Anno/Settlers (economic chains).

### Core Pillars (in priority order)

1. **Epoch System** – The player progresses through historical and future epochs. Each epoch unlocks new buildings, resources, and terrain interactions. This is the backbone of the game.
2. **Voxel Terrain with Biomes** – The world is built from voxels. Different biomes (forest, desert, tundra, volcanic, etc.) offer different resources and challenges.
3. **Economy Simulation** – Resource chains (gather → process → build) are the primary gameplay loop. The player must manage supply and demand across an increasingly complex economy.
4. **Combat is secondary** – Basic unit/defense systems may come later, but the core experience is building and managing, not fighting.

### Future Vision (NOT in scope now)

- GenAI-generated dynamic events (natural disasters, discoveries, political events)
- Multiplayer
- Mod support

---

## Technical Stack

- **Engine:** Unity (latest LTS)
- **Language:** C#
- **Target Platform:** PC (Windows/Mac/Linux)
- **Rendering Pipeline:** URP (Universal Render Pipeline)
- **Version Control:** Git + GitHub

---

## Architecture Principles

### Code Quality

- **Simplicity over cleverness.** The producer is a C# beginner. Code must be readable, well-commented, and easy to understand.
- **One class, one responsibility.** Keep scripts focused and small (<200 lines ideally).
- **Use comments generously.** Explain the "why", not just the "what". Write comments in English.
- **No premature optimization.** Make it work, make it right, then make it fast – in that order.

### Unity-Specific

- **Use ScriptableObjects** for data (resource definitions, building configs, epoch configs). This separates data from logic and makes balancing easier.
- **Prefer composition over inheritance.** Use Unity's component system as intended.
- **Use namespaces.** All code lives under `Terranova.*` namespaces:
  - `Terranova.Core` – Core systems (game loop, epoch manager)
  - `Terranova.Terrain` – Voxel terrain generation and rendering
  - `Terranova.Economy` – Resources, production chains, buildings
  - `Terranova.UI` – User interface
  - `Terranova.Camera` – Camera controls
  - `Terranova.Units` – Unit system (later)
- **Use Assembly Definitions** to keep compilation fast and dependencies clean.
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
│   │   ├── Terrain/
│   │   ├── Economy/
│   │   ├── UI/
│   │   ├── Camera/
│   │   └── Units/
│   ├── ScriptableObjects/
│   │   ├── Resources/
│   │   ├── Buildings/
│   │   └── Epochs/
│   ├── Prefabs/
│   ├── Materials/
│   ├── Scenes/
│   └── Tests/
│       ├── EditMode/
│       └── PlayMode/
├── Plugins/
└── ThirdParty/
```

### Testing

- Write **Edit Mode tests** for pure logic (economy calculations, epoch transitions).
- Write **Play Mode tests** for integration (placing buildings, terrain generation).
- Every new system should have at least basic test coverage.

---

## Current Milestone: Technical Proof of Concept

**Goal:** Prove that the technical foundation works.

### Acceptance Criteria

- [ ] Voxel terrain generates and renders (flat grid, single biome)
- [ ] RTS-style camera works (pan, zoom, rotate)
- [ ] A single building can be placed on the terrain
- [ ] Basic resource display in UI (even just a counter)

### What is NOT in this milestone

- Multiple biomes
- Economy chains
- Epoch progression
- Combat
- Polish, menus, save/load

---

## How to Work With Me

I am controlled by a human producer who is learning C# and Unity. When writing code:

1. **Always explain what you're building** before writing code. A brief summary helps the producer understand the approach.
2. **Keep PRs/changes focused.** One feature or fix per session.
3. **If you see multiple valid approaches**, present them briefly with trade-offs and let the producer decide.
4. **If something is risky or experimental**, flag it clearly.
5. **Write code that teaches.** The producer learns from reading your output.
