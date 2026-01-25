# StarMap Database Bootstrap

**Status:** Active\
**Scope:** SQLite schema structure, relationships, and data integrity

------------------------------------------------------------------------

## 1. Purpose

Defines the authoritative structure of the **StarMap SQLite database**
and rules for safe evolution.

------------------------------------------------------------------------

## 2. Database Engine

-   SQLite 3
-   File-based DB
-   No server features assumed

------------------------------------------------------------------------

## 3. Schema Philosophy

The DB represents **data storage**, not UI structure.

  DB Should Contain   DB Should NOT Contain
  ------------------- -------------------------
  Star systems        UI layout state
  Governments         Form-specific settings
  System objects      Temporary editing flags
  Wiki content        Cached render text

------------------------------------------------------------------------

## 4. Core Entity Relationships

  Table              Role
  ------------------ ---------------------------
  `star_systems`     Primary spatial nodes
  `system_objects`   Planets, moons, stations
  `governments`      Political entities
  `jump_gates`       Travel network
  `wiki_pages`       Documentation
  `wiki_images`      Images tied to wiki pages

------------------------------------------------------------------------

## 5. ID Rules

-   Primary keys use **TEXT GUIDs**
-   IDs are immutable once created
-   Renaming an ID requires **cascading updates**

------------------------------------------------------------------------

## 6. Referential Integrity

When an ID changes (example: `government_id`): - All referencing tables
must be updated - This must be handled in **DAO or Editor Save logic**,
not manually

------------------------------------------------------------------------

## 7. Migration Rule

Schema changes must follow:

1.  Add new columns/tables (never drop first)
2.  Update DAOs
3.  Update VOs
4.  Update UI
5.  Then optionally write a cleanup migration

------------------------------------------------------------------------

## 8. Data vs Derived Data

Only store **source-of-truth values**.

❌ Do NOT store: - Distances between stars - Rendered markdown -
Calculated orbit text

These must be computed at runtime.

------------------------------------------------------------------------

## 9. Image Storage Rule

`wiki_images.image_path` stores relative file paths only.\
Binary image data is never stored in DB.

------------------------------------------------------------------------

## 10. Hard Stop Rule for Schema Changes

If a change: - Alters relationships - Changes key structure - Affects
multiple tables

Work must pause and this bootstrap must be updated before proceeding.
