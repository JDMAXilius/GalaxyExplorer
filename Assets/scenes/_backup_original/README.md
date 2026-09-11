# Original scene backups — do not edit

Untouched copies of the five scenes as they stood at the end of the Quest 3 port
(commit `8bf52811`, 11 September 2026), taken before the Cosmic Simulation XR
rebuild began.

| File | Copy of |
|---|---|
| `main_scene.unity` | `Assets/scenes/main_scene.unity` |
| `core_systems_scene.unity` | `Assets/scenes/core_systems_scene.unity` |
| `galaxy_view_scene.unity` | `Assets/scenes/view_scenes/galaxy_view_scene.unity` |
| `solar_system_view_scene.unity` | `Assets/scenes/view_scenes/solar_system_view_scene.unity` |
| `galactic_center_view_scene.unity` | `Assets/scenes/view_scenes/galactic_center_view_scene.unity` |
| `main_scene_user_copy.unity` | The owner's own backup of `main_scene`, made 11 Sep 2026 |

Rules:
- **Never edit these, and never open them to work in.** They exist so any live
  scene can be compared against, or restored from, its pre-rebuild state.
- **Keep them out of the build.** None of these may appear in
  `ProjectSettings/EditorBuildSettings.asset`.
- Lighting data is not duplicated: these scenes reference the same baked
  lighting assets as the originals.
- To restore one, copy the file back over the live scene in Windows Explorer
  (not through Unity), then let the editor reimport.

Ticket: CS-001 / CS-007.
