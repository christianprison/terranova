# Terranova – Roadmap

> Last updated: February 2026

## Overview

Terranova is developed in milestone-based increments. Each milestone has a clear goal and defined acceptance criteria. We move to the next milestone only when the current one is done.

---

## Milestone 1: Technical Proof of Concept 🔴 NOT STARTED

**Goal:** Prove that the technical foundation works.

**Acceptance Criteria:**
- [ ] Voxel terrain generates and renders (flat grid, single biome, 64x64 minimum)
- [ ] RTS-style camera (pan with WASD/arrow keys, zoom with scroll, rotate with middle mouse)
- [ ] One building type can be placed on terrain via click
- [ ] Basic UI shows at least one resource counter

**Estimated Complexity:** L (Large)

**Key Risks:**
- Voxel rendering performance in Unity (mitigate: start with simple chunk-based approach)
- Getting MCP Unity bridge stable (mitigate: fallback to manual script import)

**Definition of Done:** A person can open the scene, move the camera around a voxel terrain, and place a building. It doesn't need to look good. It needs to work.

---

## Milestone 2: Vertical Slice 🔴 NOT STARTED

**Goal:** Prove that the core game loop is fun.

**Acceptance Criteria:**
- [ ] One biome fully functional with terrain variation (hills, water, trees)
- [ ] 3–5 building types with distinct functions
- [ ] One complete resource chain (e.g., Forest → Wood → Lumber Mill → Planks → Construction)
- [ ] One epoch fully playable with tech tree
- [ ] Basic worker/villager AI (move to resource, gather, return)
- [ ] Win/progress condition: accumulate enough resources to "advance epoch" (even if only one transition)

**Estimated Complexity:** XL

**Key Risks:**
- Scope creep ("just one more building type")
- Balancing resource chains without playtest data

**Definition of Done:** Someone who has never seen the project can play for 15 minutes, understand the goal, and want to keep playing.

---

## Milestone 3: Core Loop Complete 🔴 NOT STARTED

**Goal:** A playable demo / Early Access candidate.

**Acceptance Criteria:**
- [ ] 3+ biomes with unique resources and visual identity
- [ ] 3+ epochs with meaningful progression (new buildings, resources, mechanics per epoch)
- [ ] Functional tech tree spanning all available epochs
- [ ] 10+ building types
- [ ] Basic combat or defense mechanics (if decided to include)
- [ ] Save/Load system
- [ ] Main menu, settings, basic tutorial/onboarding
- [ ] Performance: stable 30+ FPS on mid-range hardware

**Estimated Complexity:** XXL

**Definition of Done:** Could be published as a paid Early Access title on Steam/itch.io.

---

## Milestone 4: GenAI Integration 🔮 FUTURE

**Goal:** Every playthrough feels unique through AI-generated events.

**Acceptance Criteria:**
- [ ] Event system framework (trigger → evaluation → effect)
- [ ] GenAI service integration (API calls to LLM)
- [ ] Event validation and balancing (AI can't break the game)
- [ ] 20+ event templates that AI can customize

**This milestone is intentionally vague.** It depends on the state of GenAI services and APIs when we get here.

---

## Principles

1. **Playable over perfect.** Every milestone ends with something you can play.
2. **Vertical before horizontal.** One complete system beats five half-finished ones.
3. **Cut scope, not quality.** If a milestone takes too long, remove features – don't ship broken ones.
