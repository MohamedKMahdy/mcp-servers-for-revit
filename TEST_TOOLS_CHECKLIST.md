# Test Tools panel — door-on-wall placement + tagging checklist

Manual verification for hosted-family placement (`create_point_based_element` /
`place_and_configure`) and `tag_element`, driven entirely from the **Test Tools**
ribbon button. **Do not perform model writes by any other route** — use the panel.

> **Revit version:** run this in **Revit 2025 or 2026** (both .NET 8). Revit 2027 is
> dropped for now — it hosts .NET 10 and RevitMCPSDK has no 2027 release on nuget yet.

---

## 0. Prerequisites

- [ ] Commandset built for your Revit version (`Debug R25` or `Debug R26`) — the
      build's `DeployCommandSet` target copies the DLLs to
      `%AppData%\Autodesk\Revit\Addins\<ver>\revit_mcp_plugin\Commands\RevitMCPCommandSet\`.
- [ ] Plugin built/deployed for the same version (provides the ribbon + panel).
- [ ] A project open with **Level 1** and a **Floor Plan : Level 1** view active.
- [ ] The **M_Single-Flush** door family loaded (Insert → Load Family if missing,
      or edit the test's `familyName` to a door family you have).
- [ ] A **Door Tag** family loaded (for the tagging step).

## 1. Model setup (the one thing that must match the test coordinates)

The placement tests target point **(2500, 0, 0) mm**. The nearest-wall logic only
snaps a door if a wall passes near that point.

- [ ] Draw a **wall on Level 1** running through (2500, 0) — e.g. a straight wall
      from **(0, 0)** to **(5000, 0)** mm. Stay in the Floor Plan : Level 1 view.

---

## 2. Panel verification sequence (run in this order)

| # | Action in panel | Expected result |
|---|---|---|
| 1 | Click **Revit MCP Switch** (ribbon) | "Open Server" dialog; TCP server on :8080 |
| 2 | Click **Test Tools** (ribbon) | Window opens; status dot is **green / "Running on port 8080"** |
| 3 | Run **send_code_to_revit** | `✓ Pass`; result is your **project title** (bridge sanity) |
| 4 | Run **get_available_family_types** | `✓ Pass`; list includes **M_Single-Flush** (door family is loaded) |
| 5 | Run **get_current_view_elements** | `✓ Pass`; your wall appears — note its `elementId` |
| 6 | Run **create_point_based_element** | `✓ Pass`; a **door appears hosted in the wall** at ~(2500,0), snapped to the wall centerline. Read the new door's `elementId` from the result JSON |
| 7 | *(or instead of 6)* Run **place_and_configure** | `✓ Pass`; door placed **and** its **Mark = D-TEST**; read the `elementId` from `Response` |
| 8 | Visually confirm | The door is **cut into the wall** (not floating in space) and swings/faces sensibly |
| 9 | Put the placed door's id in the **Element ID** box (or select the door in Revit → **Get Selected Element ID**) | Element ID box shows the door id |
| 10 | Run **get_element_info** | `✓ Pass`; `host` = the **wall's elementId** from step 5, `category` = **Doors** |
| 11 | Run **tag_element** | `✓ Pass`; a **Door Tag** appears on the door in the active view |

### Pass criteria
- Steps 6/7 place a door **hosted in the wall** (step 8 visual + step 10 `host` = wall id).
- `place_and_configure` (7) sets **Mark = D-TEST** atomically (verify in the door's properties).
- `tag_element` (11) adds a visible Door Tag.

---

## 3. Expected failure modes & fixes

| Symptom | Cause | Fix |
|---|---|---|
| Error "requires a host wall" / door not placed | No wall near (2500,0) | Redraw the wall through (2500,0) (step 1) |
| Door places but floats / not cut into wall | Wall too far from the point (beyond half-width + tol) | Move the door point onto the wall, or the wall onto the point |
| `get_available_family_types` lacks M_Single-Flush | Door family not loaded | Load M_Single-Flush, or edit the test's `familyName` |
| `tag_element` fails "No tag family found" | No Door Tag loaded | Load a Door Tag family, retry |
| Status dot red | TCP server not started | Click **Revit MCP Switch** first (step 1) |

---

## 4. What this verifies vs. the codebase

- `create_point_based_element` → `ProjectUtils.CreateInstance` `OneLevelBasedHosted`
  branch: auto-detects the nearest wall (`GetNearestWallByLocationLine`) and snaps
  the door to its centerline.
- `place_and_configure` → atomic place + parameter set in one `TransactionGroup`.
- `tag_element` → `IndependentTag.Create`, auto-selecting the tag family by the
  element's category when `tagTypeId` is omitted.
