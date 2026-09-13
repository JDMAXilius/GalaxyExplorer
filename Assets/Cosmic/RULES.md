# Cosmic rework — rules

Everything under `Assets/Cosmic/` is the rework. The old tree stays bootable and is never edited from here.

## Code

- Namespace `Cosmic`; editor code `Cosmic.Editor`. File name is the type name.
- **No comments** except at most one line where a non-obvious decision needs recording. No doc comments,
  no `#region`, no banner headers. A comment that restates the line is deleted on sight.
- Tight spacing: no blank line after an opening brace, at most one blank line between members, no trailing
  whitespace. Expression-bodied members where they fit on one line.
- Every serialized number carries its unit in its name — `heightMetres`, `fadeSeconds`, `titleMm`. No
  `[Tooltip]` essays; the name is the documentation.
- No `FindObjectOfType` at runtime. No `Resources.Load` except the licence text.
- Three singletons exist in the whole app and no more: `App`, `Director`, `Room`.
- Files stay under roughly 250 lines. A file that wants to be longer is two types.
- `Awake` may not have run: a component added with `AddComponent` and used the same frame has not had it.
  Initialise lazily from every public entry point.

## Assets

- One canvas unit is one millimetre (canvas scaled 0.001). Millimetres are serialized, never metre constants.
- Custom shaders carry the stereo macros (`UNITY_VERTEX_INPUT_INSTANCE_ID`, `UNITY_VERTEX_OUTPUT_STEREO`,
  `UNITY_SETUP_INSTANCE_ID`, `UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO`), `#pragma target` 4.5 or lower, and no
  geometry shaders. Android ships Single Pass Instanced, where a missing macro breaks one eye and Link
  cannot show it.
- `Shader.Find` is not safe for built-ins. Ship the shader and register it under Always Included Shaders.
- Every hand interaction has a mouse or keyboard equivalent in the Desktop action map.
- Nothing snaps back. The player arranges objects and they stay arranged. The one exception is a lost
  object: left out of reach or below the floor for five seconds it comes home, so nothing is ever unreachable.
  Off by default (`Grabbable.autoReturn`); the owner signs it off in CS-140.

## Shape of the rework

| Kind | Target |
|---|---|
| Runtime scripts | 29 |
| Editor scripts | 6 planned, 9 landed (Bake split three ways, Bodies beside Content; CS-150) |
| Shaders | 16 |
| Scenes | 1 |

The count is a budget, not a quota. A file that would take the runtime past 29 has to displace one.

## Process

- **Every builder is idempotent.** An editor script that constructs a prefab, a scene or an asset can be run
  twice with the same result: it finds or creates, never appends. Re-running a builder is the normal way to
  pick up a change, so a builder that duplicates on the second run is a bug.
- **Every exit gate is a play-mode or device observation**, not a compile. "It builds" closes nothing. The
  gate is a thing seen in Play mode in the editor or on the Quest, written down with what was seen.

## Fixed facts the rebuild depends on
- `StarVert` is 10 floats, 40 bytes, sequential: yOffset, curveOffset, ellipseDistance, ellipseOffset, color(3), uv(2), size. Every point shader declares exactly this. Old baked `StarsData`/`NebulaVolumeData` are 11 floats and must be re-baked, never reinterpreted.
- Data enum is `RoomMode` and the panel copy struct is `PanelCopy`, so the `Room` and `Panel` components keep the short names.
- Content is reused byte for byte: meshes, textures, audio, fonts, sprites, copy. Only code, prefab assembly, shaders and scenes are rebuilt.
- No prefab or scene is hand-edited, ever. Every value lives in code or a data asset, every prefab is written by a builder, and the editor is driven only through the MCP relay. A rebuilt prefab is therefore faithful by construction; there is no capture step.
