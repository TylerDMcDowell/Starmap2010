# Astraeum 2548 Wiki Master Prompt
Version: 1.0  
Status: CANON  
Last Updated: 2026-01-21  

This document defines the authoritative rules for generating wiki content and SQL inserts for the Astraeum 2548 Codex used by the StarMap2010 project.

This document supersedes any conversational context unless explicitly overridden.

---

## Temporal Canon (LOCKED)

- Current in-universe year: **2548 CE**
- Last major interstellar war ended: **2544 CE**
- “Post-war” refers to the period 2544–2548 unless otherwise specified

---

## Interstellar Transit Canon (LOCKED)

### Direct FTL Speeds (hours per light-year)
- Courier-class direct FTL: ~3 h/ly
- Heavy direct FTL: ~14 h/ly

### Jumpgate Speeds (hours per light-year)
- Legacy Gate: ~14–15 h/ly
- Standard Gate: ~9 h/ly
- Advanced Gate: ~7–8 h/ly

### Gate Throughput (Bulk Cruisers per day)
- Legacy: ~2–3
- Standard: ~5–8
- Advanced: ~10–15

These values are not subject to reinterpretation or retcon without explicit canon revision.

---

## Economic & Logistics Canon (LOCKED)

- The **Bulk Cruiser** is the fundamental unit of interstellar commerce and logistics
- Cargo capacity is ~2,500 tons order-of-magnitude
- Bulk Cruisers use modular cargo pods
- Pods detach for in-system distribution via local craft

All interstellar trade, gate throughput, fleet logistics, and economic planning reference Bulk Cruisers implicitly or explicitly.

---

## Governments vs Institutions (CRITICAL DISTINCTION)

### Sovereign Governments / Polities
- Solarian Concord
- Virel Free Marches
- Thalean Compact
- Astraeum Protectorate
- Khar’Vess Dominion
- Heliad Trade League
- Minor Systems Directorate

### Supra-Political Institution
- **The Lumenary Synod**

The Lumenary Synod is NOT:
- a sovereign state
- a civilization
- a polity
- a territorial government

The Synod operates across borders through doctrine, certification, observance, and legitimacy.

Do not describe the Synod as a government under any circumstances.

---

## Belief Model (FOUNDATIONAL)

- Belief does not alter physics
- Belief alters behavior
- Behavior alters survival outcomes

Ritual, doctrine, and observance exist to:
- enforce restraint
- legitimize delay
- normalize abort authority
- suppress reckless optimization

This is a behavioral system, not magic.

---

## Tone & Style Requirements

All wiki content must default to:
- in-universe voice
- narrative-forward prose
- institutional perspective where appropriate
- implication over exposition
- “rich and creamy” texture (thick, atmospheric, GM-useful)

Avoid:
- sterile encyclopedic tone
- omniscient narration
- absolute metaphysical claims

---

## SQLite Schema (AUTHORITATIVE)

All SQL must conform exactly to this schema:

[SCHEMA OMITTED HERE — REFER TO REPO VERSION]

Rules:
- Use INSERT OR REPLACE
- page_id == slug
- sort_order == 0 unless instructed otherwise
- updated_utc = datetime('now')
- Do not invent entity IDs
- Do not emit wiki_links or wiki_images unless explicitly instructed

---

## Output Rules

Unless explicitly stated:
- Output SQL only
- No commentary
- No explanations

This document is the law.
