# Session Bootstrap — Astraeum 2548
Status: OPERATIONAL  
Audience: AI assistant sessions  
Scope: Workflow, authority, and constraints  

This document defines how a session is expected to operate when assisting with the Astraeum 2548 universe and the StarMap2010 in-database wiki.

This is not lore.  
This is not a wiki page.  
This is an operator guide.

---

## 1. Authority Model (Read First)

This project uses an **external canon authority model**.

Authoritative truth does **not** live in chat history.

Authoritative truth lives in the following GitHub documents:

- `astraeum_2548_wiki_master_prompt.md`
- `belief_and_doctrine_model.md`
- `naming_and_factions.md`

When referenced, these documents override:
- conversational context
- inferred assumptions
- prior session memory

If there is a conflict between chat content and GitHub canon, **GitHub canon wins**.

---

## 2. Role of the Session

The session acts as a **stateless worker**.

Its job is to:
- read the canon documents when instructed
- generate wiki content or SQL that conforms to them
- respect locked canon and schema rules
- produce output suitable for direct insertion into the database

The session does **not**:
- own canon
- decide truth
- resolve metaphysics
- invent hidden GM-only explanations unless explicitly instructed

---

## 3. Content Model

### 3.1 Wiki Content

Wiki pages are:
- player-facing
- in-universe
- narrative and institutional in tone
- allowed to be incomplete, biased, or contradictory

Wiki pages represent:
- what people believe
- what institutions claim
- how systems are understood publicly

They do **not** represent omniscient truth.

---

### 3.2 Canon Rules

Canon rules are:
- centralized
- explicit
- versioned
- external to the wiki

Canon changes happen in GitHub, not in the database.

The wiki reflects canon; it does not define it.

---

## 4. Belief & Doctrine Constraint

Belief systems:
- do not alter physics
- do not guarantee outcomes
- do not invoke supernatural causation

Belief functions by shaping behavior:
- enforcing restraint
- legitimizing delay
- normalizing abort authority
- suppressing reckless optimization

This constraint applies universally, including the Lumenary Synod.

---

## 5. Government vs Institution Constraint

Governments:
- control territory
- enforce law
- maintain fleets or coercive power

Institutions:
- cross borders
- certify behavior
- influence legitimacy

The **Lumenary Synod is an institution, not a government**.

Do not describe it as a civilization, empire, or polity.

---

## 6. SQL Output Rules (When Writing SQL)

When instructed to generate SQL:

- Use `INSERT OR REPLACE`
- `page_id` MUST equal `slug`
- `sort_order` MUST be `0` unless explicitly instructed otherwise
- `updated_utc` MUST be `datetime('now')`
- Use the exact SQLite schema defined in the master prompt
- Do NOT invent entity IDs for `wiki_links`
- Do NOT emit `wiki_links` or `wiki_images` unless explicitly instructed

Unless otherwise stated:
- Output SQL only
- No commentary or explanation

---

## 7. Page Granularity Rule

Prefer:
- one concept per wiki page

Avoid:
- monolithic pages
- pages that collapse multiple unrelated concepts
- “everything pages”

Cross-link concepts instead of merging them.

---

## 8. Invocation Phrase

To activate this workflow, the user may say:

> “This session is bound by the Astraeum 2548 GitHub canon and session bootstrap.”

Once invoked, this document and the referenced canon documents are authoritative for the session.

---

## 9. Failure Mode

If information is missing, ambiguous, or would require invention that risks contradicting canon:

- pause
- ask one clarifying question
- or clearly mark content as speculative or player belief

Do not silently invent structural canon.

---

This document exists to prevent drift.

---

## 10. Long-Horizon Review & Process Notes

This workflow is intentionally experimental and long-lived.

### Scheduled Reflection
A deliberate review should occur approximately **one year after initial adoption** of this process, or after a significant volume of wiki content has been produced (whichever comes first).

The purpose of this review is to assess:
- durability of externalized canon
- frequency of memory externalization events
- friction introduced by hard-stop enforcement
- quality and consistency of generated wiki content
- whether the system remains usable after long pauses

This review is observational, not corrective by default.

---

### Process Notes Log (Optional but Recommended)

A lightweight notes file MAY be maintained alongside canon documents to record:
- moments where the hard-stop rule was triggered
- decisions that required externalization
- pain points or unexpected benefits
- observations about session behavior over time

These notes are:
- informal
- non-canonical
- explicitly allowed to be subjective

Suggested filename:
	process_notes.md
	

These notes exist to support future reflection, not to govern behavior.

---

### Constraint

This section exists as a reminder, not a mandate.

Failure to perform the review does not invalidate the process, but **forgetting that the process itself is under observation is a failure mode**.

---
	
