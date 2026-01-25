# Workshop Bootstrap — 3D Printing & Hardware Tuning

**Status:** Active  
**Scope:** All sessions involving 3D printers, slicer tuning, hardware modification, or print troubleshooting

This document defines the persistent working knowledge, constraints, and tuning doctrine for the user's 3D printing workshop. It is loaded whenever a session involves printer hardware, slicer configuration, or print diagnostics.

---

## 1. Workshop Philosophy

Primary goal: **Reliability and consistency over raw speed**

The workshop prioritizes:
- Stable extrusion
- Repeatable print quality
- Reduced mechanical stress
- Lower failure rates

Throughput comes from **multiple printers running reliably**, not from pushing individual machines to maximum speed.

---

## 2. Printer Fleet Context (General)

The workshop operates multiple Creality Ender-series style printers. Machines may vary in modification level, but tuning assumptions should default to:

- Bowden extrusion unless stated otherwise
- Manually maintained machines (user performs mechanical adjustments)
- Terrain and tabletop model printing as common use case

Specific machine configurations may be defined in future subsections or machine-specific notes.

---

## 3. Global Tuning Doctrine

### 3.1 Tuning Order of Operations

When diagnosing or improving print quality, adjustments must follow this order:

1. **Mechanical integrity**
   - Belts, pulleys, tensioners
   - Frame rigidity
   - Motion smoothness
2. **Thermal and filament path**
   - Hotend condition
   - PTFE seating
   - Heat break type
3. **Extrusion stability**
   - Flow rate
   - Nozzle size
   - Temperature
4. **Slicer geometry settings**
   - Line width
   - Wall structure
   - Top/bottom configuration
5. **Speeds and retraction**
6. **Cooling and fine detail**
7. **Experimental features (last resort)**

Slicer tweaks must not be used to compensate for unresolved mechanical faults.

---

### 3.2 One Section at a Time Rule

Slicer profiles are tuned **one section at a time** (e.g., Quality → Walls → Top/Bottom → Infill → Material → Speed → Travel).

ChatGPT must not suggest changes outside the section currently being reviewed unless a safety issue exists.

---

## 4. Nozzle Strategy

### 4.1 Terrain Printing Default
A **0.6 mm nozzle** is the default for terrain and large surface prints unless fine detail requires a smaller nozzle.

Benefits:
- Reduced extrusion back-pressure
- Faster area coverage
- More natural stone/organic texture
- Lower stress on Bowden systems

### 4.2 Detail Printing
Smaller nozzles (e.g., 0.4 mm or below) are reserved for:
- Fine detail models
- Small features where surface sharpness is critical

---

## 5. Heat Break & Hotend Rules

### 5.1 Bimetal Heat Break Behavior

When a printer is equipped with a **bimetal heat break**:

- Retraction distances must be **reduced** compared to PTFE-lined systems
- Over-retraction can cause heat creep and clogs
- Slightly higher print temperatures may be required for smooth flow
- Extrusion tuning must prioritize smooth, continuous flow over aggressive retraction

### 5.2 PTFE Seating Critical Rule

Improper PTFE tube seating against the heat break can cause:
- Extrusion resistance
- Clicking or grinding
- Partial clogs

When working on Bowden systems, PTFE seating is considered a **first-check diagnostic item**.

---

## 6. Extrusion Stability Principles

- Avoid excessive flow rates that increase back-pressure
- Larger nozzles reduce stress on the extruder system
- Clicking from the extruder is treated as a **mechanical or flow problem**, not just a slicer issue
- Stable extrusion is prioritized over maximum speed

---

## 7. Surface Finish Priorities

For terrain and large flat surfaces:

- Consistency of layers is more important than perfectly smooth surfaces
- Slight texture is acceptable and often desirable for stone or organic environments
- Wall stability and bonding take priority over visual perfection

---

## 8. Knowledge Promotion Rule

When new printer behavior, hardware lessons, or tuning doctrine proves to be **reusable across sessions**, it must be promoted into this Workshop Bootstrap following the Master Bootstrap Hard Stop process.

---

**End of Workshop Bootstrap**
