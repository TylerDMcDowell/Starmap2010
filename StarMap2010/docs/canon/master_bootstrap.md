# Master Collaboration Bootstrap
**Status:** Active  
**Scope:** Global (applies to all sessions unless explicitly overridden)

---

## Purpose
This document defines the global operating rules for collaboration across all ChatGPT sessions,
regardless of topic. It governs how knowledge is preserved, how continuity is maintained, and
how temporary work is distinguished from long-term project memory.

This bootstrap acts as the **root layer of session governance**.

---

## 1. Memory Model
Chat sessions are treated as **temporary working memory**, not authoritative storage.
All long-term knowledge must live in **user-controlled external documents** (e.g.,
GitHub repositories, structured notes, or project documentation). These documents
function as the persistent memory layer across sessions.

When invoked, external bootstrap documents take **priority over conversational memory**.

---

## 2. Bootstrap Hierarchy
1. **Master Bootstrap** (this document) — global collaboration rules
2. **Domain Bootstraps** — project-specific knowledge and constraints
3. **Session Context** — temporary working discussion

Domain bootstraps are loaded only when relevant to the session topic.

---

## 2.1 Domain Bootstrap Registry
This section lists all active domain-specific bootstraps and their repository locations.
These documents contain persistent project knowledge and are loaded when their subject
domain becomes relevant in a session.

| Domain                  | Bootstrap Name                           | Repository Path                                                | When To Load                                                     |
|-------------------------|------------------------------------------|----------------------------------------------------------------|------------------------------------------------------------------|
| 3D Printing Workshop    | Workshop Bootstrap — 3D Printing & Hardware Tuning | StarMap2010/docs/process/workshop_bootstrap.md                  | Any session involving 3D printers, hardware tuning, slicer settings, or print diagnostics |
| Astraeum Project        | Astraeum Canon Bootstrap                 | StarMap2010/docs/canon/session_bootstrap.md                     | Any session involving Astraeum lore, canon, SQL generation, or worldbuilding |
| Coding & Development    | Coding Bootstrap                         | StarMap2010/docs/bootstrap/coding_bootstrap.md                  | Any session involving software development, code changes, or technical implementation |
| Paper Writing Project   | Paper Project Bootstrap                  | (To be defined)                                                | Any session involving structured paper development, research doctrine, or academic writing workflow |

---

## 3. Transient vs Persistent Sessions
Not all sessions are intended to produce long-term knowledge.

A session is **transient** when it involves:
- One-off questions
- Brainstorming without commitment
- Exploratory or throwaway work

ChatGPT must **not promote** information from transient sessions to long-term storage unless the
user explicitly directs it.

---

## 4. Detection of Long-Term Knowledge
When information crosses from temporary discussion into **structural, project-level knowledge**, it
becomes continuity-critical.

Indicators include:
- Stable hardware configurations or tuning doctrine
- Canonical project rules or constraints
- Locked research direction or terminology
- Reusable workflows or standards
- Lessons learned that prevent repeated failure

---

## 5. Hard Stop Rule (Continuity Safeguard)
When continuity-critical knowledge is detected:

> **ChatGPT must pause forward progress.**

It must clearly state that:

> This information has reached long-term significance and must be preserved before continuing.

Work on the active task must not proceed until the user either:
1. Confirms the information is temporary, or
2. Agrees it should be **formally written** into the appropriate bootstrap or long-term document.

---

## 6. Preservation Workflow
When the user agrees information should be preserved:

1. ChatGPT drafts or edits the canonical text in clear, structured language.
2. The user saves the updated document to external storage (e.g., GitHub).
3. ChatGPT verifies that the update was completed.
4. ChatGPT confirms the stored version accurately reflects the agreed knowledge.

Only after verification may the session proceed.

---

## 7. Authority and Responsibility
**User Responsibilities**
- Decide what becomes long-term knowledge.
- Maintain and store external bootstrap documents.
- Confirm when preservation steps are complete.

**ChatGPT Responsibilities**
- Detect when knowledge reaches long-term significance.
- Enforce the Hard Stop Rule.
- Draft structured canonical language when needed.
- Verify preservation before continuing.

---

## 8. Text File and Encoding Standards
All project text files must follow consistent formatting to avoid encoding issues,
conflicting diffs, or compilation problems.

**Encoding**
- Use **UTF-8 without BOM** where possible.
- Source code should avoid smart quotes and special Unicode punctuation.

**Line Endings**
- Use **Windows CRLF** in all code and docs.

**Indentation**
- Use **spaces only** (4 spaces per indent level).

**Trailing Whitespace**
- Avoid trailing whitespace on any line.

**Files Covered**
- `.cs` source
- `.md` documentation
- `.sql` schema or migrations
- `.txt`, `.cfg`, or other plain text formats

Rationale: Consistent encoding and formatting prevent:
- Git diff noise (CRLF vs LF)
- Invisible BOM errors in SQLite or script loads
- Editor toolchain mismatches

---

## 9. Change Contribution Process (Preferred Workflow)
When making changes that should persist:

1. **Draft the change text or code** in session.
2. **Write it into the appropriate bootstrap or doc** in the correct file path.
3. The user **saves it externally** (GitHub or similar).
4. ChatGPT **verifies the saved content** by fetching it back.
5. ChatGPT **confirms** successful preservation before proceeding.

This process is the **preferred way for lasting changes**.

---

## 10. Session Independence
Session context may reference other session knowledge, but only **external documents**
(counted as canonical) are treated as authoritative.

Sessions should never rely solely on prior chat content that has not been preserved
into a bootstrap or document.

---

## 11. Cross-Bootstrap Access
Any session may reference any domain bootstrap as needed, once activated.

Loading order:
1. Master Bootstrap
2. Domain Bootstraps (relevant to topic)
3. Session Context

---

## 12. Purpose of This System
This bootstrap exists to:
- Prevent loss of important decisions
- Enable continuity across sessions
- Reduce reliance on fragile conversational memory
- Allow structured, cumulative progress across multiple domains

When necessary, this system **prioritizes durability of knowledge over conversational flow**.

---

**End of Master Collaboration Bootstrap**
