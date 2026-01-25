# Master Collaboration Bootstrap
**Status:** Active  
**Scope:** Global (applies to all sessions unless explicitly overridden)

---

## Purpose
This document defines the global operating rules for collaboration across all ChatGPT sessions, regardless of topic. It governs how knowledge is preserved, how continuity is maintained, and how temporary work is distinguished from long-term project memory.

This bootstrap acts as the **root layer of session governance**.

---

## 1. Memory Model
Chat sessions are treated as **temporary working memory**, not authoritative storage.  
All long-term knowledge must live in **user-controlled external documents** (e.g., GitHub repositories, structured notes, or project documentation). These documents function as the persistent memory layer across sessions.

When invoked, external bootstrap documents take **priority over conversational memory**.

---

## 2. Bootstrap Hierarchy
1. **Master Bootstrap** (this document) — global collaboration rules  
2. **Domain Bootstraps** — project-specific knowledge and constraints  
3. **Session Context** — temporary working discussion  

Domain bootstraps are loaded only when relevant to the session topic.

---

## 2.1 Domain Bootstrap Registry

| Domain | Bootstrap Name | Repository Path | When To Load |
|--------|----------------|-----------------|--------------|
| 3D Printing Workshop | Workshop Bootstrap — 3D Printing & Hardware Tuning | StarMap2010/docs/process/workshop_bootstrap.md | Sessions involving 3D printers, hardware tuning, slicer settings, or print diagnostics |
| Astraeum Project | Astraeum Canon Bootstrap | StarMap2010/docs/canon/session_bootstrap.md | Sessions involving Astraeum lore, canon, SQL generation, or worldbuilding |
| Coding & Development | Coding Bootstrap | StarMap2010/docs/bootstrap/coding_bootstrap.md | Sessions involving software development, code structure, or implementation standards |
| StarMap Application | StarMap Application Bootstrap | StarMap2010/docs/bootstrap/application_bootstrap.md | Sessions involving UI behavior, architecture, interaction modes, or feature implementation |
| StarMap Database | StarMap Database Bootstrap | StarMap2010/docs/bootstrap/database_bootstrap.md | Sessions involving schema design, relationships, migrations, or data integrity |
| Research / Paper Project | AI Collaboration Research Bootstrap | StarMap2010/docs/process/paper_bootstrap.md | Sessions discussing methodology, collaboration design, research framing, or academic paper development |

---

## 3. Transient vs Persistent Sessions
A session is **transient** when it involves:
- One-off questions  
- Brainstorming without commitment  
- Exploratory or throwaway work  

ChatGPT must **not promote** information from transient sessions to long-term storage unless explicitly directed.

---

## 4. Detection of Long-Term Knowledge
When information becomes continuity-critical, it includes:
- Stable hardware configurations  
- Canonical project rules  
- Locked research direction  
- Reusable workflows  
- Structural changes to AI–human collaboration

---

## 5. Hard Stop Rule
When continuity-critical knowledge is detected:

> ChatGPT must pause forward progress and request preservation before continuing.

---

## 6. Preservation Workflow
1. Draft canonical text  
2. User saves externally  
3. ChatGPT verifies  
4. Confirm accuracy before proceeding

---

## 7. Authority and Responsibility

**User**
- Decides what is long-term knowledge  
- Maintains documents  

**ChatGPT**
- Detects continuity significance  
- Enforces Hard Stop  
- Drafts canonical text  
- Verifies preservation  

---

## 8. Text File and Encoding Standards
- UTF-8 (no BOM preferred)  
- Windows CRLF line endings  
- Spaces only (4 per indent)  
- No trailing whitespace  

Applies to `.cs`, `.md`, `.sql`, and text-based project files.

---

## 9. Change Contribution Process
1. Draft change  
2. Write into correct document  
3. User saves externally  
4. ChatGPT verifies  
5. Confirm before continuing

---

## 10. Session Independence
Only external documents are authoritative, not chat history alone.

---

## 11. Cross-Bootstrap Access
Loading order:
1. Master Bootstrap  
2. Relevant Domain Bootstraps  
3. Session Context  

---

## 12. Research Documentation Requirement
Because this collaboration system is a research subject, a file:

`StarMap2010/docs/process/process_notes.md`

must log:
- Bootstrap changes  
- Workflow discoveries  
- Collaboration model improvements  

ChatGPT should suggest logging when the system evolves.

---

## 13. Purpose of This System
To ensure continuity, preserve knowledge, enable structured progress, and support research into long-term AI-assisted collaboration.

---

**End of Master Collaboration Bootstrap**
