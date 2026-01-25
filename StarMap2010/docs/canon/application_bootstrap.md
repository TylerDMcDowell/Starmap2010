# StarMap Application Bootstrap

**Status:** Active\
**Scope:** Software architecture, UI systems, interaction logic, and
coding standards

------------------------------------------------------------------------

## 1. Purpose

This bootstrap defines the structural and behavioral rules of the
**StarMap application**.\
It ensures code consistency, UI predictability, and safe feature
expansion across sessions.

This document governs **how the program is built**, not the fictional
universe.

------------------------------------------------------------------------

## 2. Technology Stack (Locked)

  -----------------------------------------------------------------------
  Component                           Standard
  ----------------------------------- -----------------------------------
  Language                            C#

  Framework                           .NET Framework 4.x

  IDE Target                          Visual Studio 2013

  UI Framework                        Windows Forms

  Database                            SQLite

  Pattern Style                       Practical WinForms + DAO pattern
                                      (not enterprise MVC)
  -----------------------------------------------------------------------

No migrations to WPF, .NET Core, or web frameworks should be assumed
unless explicitly authorized.

------------------------------------------------------------------------

## 3. Architectural Model

StarMap follows a **layered practical architecture**:

  Layer              Responsibility
  ------------------ ---------------------------------------------
  **UI Forms**       Display, user interaction, event handling
  **UI Helpers**     Rendering helpers, layout utilities
  **Models (VOs)**   Plain data containers matching DB structure
  **DAOs**           All database read/write operations
  **Database**       SQLite schema (see Database Bootstrap)

**Rule:**\
UI code must **never directly execute SQL**.\
All DB access goes through **DAO classes**.

------------------------------------------------------------------------

## 4. Form Responsibilities

Forms should follow these roles:

  Form Type         Role
  ----------------- ----------------------------------------------
  Viewer Forms      Read-only display of data
  Editor Forms      Controlled editing with Save/Cancel workflow
  MainForm          Navigation, map display, global tools
  Canvas Controls   Rendering only --- no DB logic

------------------------------------------------------------------------

## 5. Editing Workflow Standard

All editors must follow this pattern:

1.  Load data from DAO
2.  User edits local in-memory copy
3.  **Save → DAO writes to DB**
4.  **Cancel → Discard local changes**

❗ No DB writes should occur before Save is pressed.

------------------------------------------------------------------------

## 6. UI Behavior Consistency Rules

-   Buttons that modify data appear **only in edit mode**
-   Read-only mode must be safe from accidental data changes
-   Toolbars may be context-sensitive but must not hide core navigation
-   Confirmation dialogs required for destructive actions

------------------------------------------------------------------------

## 7. Map Interaction Modes

MapCanvas supports multiple **interaction modes**.\
Modes must be **explicitly set**, never inferred.

Examples: - Selection Mode - Swap Mode - Measure Distance Mode - Gate
Edit Mode

Mode state must be: - Clearly visible in UI (checkbox, button, status
label) - Mutually exclusive when appropriate

------------------------------------------------------------------------

## 8. Coordinate System Standard

Star positions use a **3D cartesian coordinate system in light-years**.

Distance calculations must use:

    sqrt( (dx²) + (dy²) + (dz²) )

All spatial math assumes LY units unless otherwise specified.

------------------------------------------------------------------------

## 9. Image Asset Rules

Images used by the wiki or UI:

-   Stored under `Assets/`
-   Referenced by **relative paths**
-   Never embedded as BLOBs in SQLite

------------------------------------------------------------------------

## 10. Code Style Rules

-   Use descriptive names (no single-letter vars except loops)
-   Avoid LINQ if it reduces clarity for maintenance
-   Keep methods under \~60 lines when possible
-   UI layout code should be separated from logic code

------------------------------------------------------------------------

## 11. Change Preservation Rule

Any time: - A new interaction mode is added\
- A core UI behavior changes\
- A new architectural pattern is introduced

This bootstrap must be updated before further feature expansion.
