# Store-readiness checklist

Written for CS-084 by going through the code, not from a generic launch
checklist. Each item says what was actually found and why it is here. Track
tags match `docs/BACKLOG.md`'s convention: **[CC]** doable in the repo alone,
**[TERM]** needs the live Unity editor, the Quest, or a build. Ticket IDs
below are suggestions for whoever updates the backlog; none of them exist
there yet.

## Blocking submission

- **[TERM] CS-086 - Replace/remove the About slate's Microsoft links.**
  `Assets/prefabs/about_slate_prefabs/about_slate_prefab.prefab` has six
  `Hyperlink` instances under `links_offset`. Two map to things we still want
  (source code, privacy) and need new URLs; four (`hl2_for_devs`,
  `galaxy_explorer`, `original_galaxy_explorer`,
  `microsoft_services_agreement`) point at Microsoft pages with no equivalent
  in our own About copy (`docs/copy/ui.md`'s About section only lists Source
  code and Privacy) and should be deleted, not re-pointed. Do this as a
  `PrefabUtility.LoadPrefabContents` script per the project's working rule,
  not by hand-editing the YAML. Copy block: `docs/store/ABOUT_COPY.md`.

- **Host the privacy policy at a public URL.** Meta's submission form requires
  one. `docs/store/PRIVACY_POLICY.md` is the drafted text; nothing in this
  repository can host it (no CC or TERM ticket applies - this is an owner
  action: publish the page, then put the URL in both the Meta Developer
  Dashboard and the About screen via CS-086).

- **[DONE 12 Sep 2026] CS-112 - the vendored TouchScript TUIO/OSC module is
  gone.** (This item previously carried the wrong ticket number, CS-090.)
  `Assets/external/TouchScript/Modules/TUIO` held `OSCsharp.dll` and
  `TUIOsharp.dll`, which can open a network socket to receive touch input from
  external multitouch tables. It was **not** merely inert: both DLLs were marked
  `Android: enabled: 1`, and `TuioInput.cs` lived under `Assets/` and so
  compiled into the app's own assembly and referenced them - meaning the
  capability shipped in the APK even though nothing invoked it, which is exactly
  what an automated store scan would flag against a policy saying "no network
  calls." Verified a closed cluster (nothing inside or outside TouchScript
  referenced the module, and TouchScript has no assembly definition), then
  removed - 14 files. Compiles clean afterwards. See D-009.

- **Fill in the real legal details.** `docs/store/PRIVACY_POLICY.md` and
  `docs/store/ABOUT_COPY.md` both have owner-only placeholders: company/
  developer name, support contact, the actual source-code and privacy-policy
  URLs. Not a ticket - a decision only the owner can make.

## Should do before submission

- **[CC] CS-113 - Give the app an owned version number. DONE 12 Sep 2026,
  unverified.** `Quest3ProjectSetup.AppVersion` is `0.9.0` and is written to
  `PlayerSettings.bundleVersion` on every `ConfigureProject()`, with the
  Android version code derived from it (`major*10000 + minor*100 + patch` =
  900) so the two cannot drift. See the D-001a extension in
  `docs/decisions.md`. Still needs a [TERM] check that one *Configure Project*
  really rewrites `ProjectSettings.asset`, which still holds the stale `1.0`
  / `1`.

- **[TERM] CS-114 - Show the version on the About slate.** There is no text
  element for it today. Needs a new UI element in `about_slate_prefab.prefab`
  with a `LegalNoticeText` component set to notice `Version`; that fills
  itself from `Application.version`, so no further code is needed.

- **[TERM] CS-091 - Verify the exported `AndroidManifest.xml`.** There is no
  custom manifest in `Assets/Plugins/Android`; every permission the shipped
  APK requests comes from the OpenXR/Meta package auto-merge (hand tracking,
  passthrough/AR session). Confirm the actual Gradle-exported manifest only
  requests what `docs/store/PRIVACY_POLICY.md` describes, and nothing
  unexpected (for example `RECORD_AUDIO` or `INTERNET`) arrived transitively
  from a package. Needs a real build (pairs with CS-081).

- **[CC] CS-116 - Bundle the MIT licence text with the build. DONE in code
  12 Sep 2026, unverified; in-app half is [TERM] CS-117.**
  `Quest3ProjectSetup.EnsureLegalNoticesShip()` copies the repo-root
  `License.txt` verbatim into `Assets/Resources/legal/galaxy_explorer_license.txt`
  on every `ConfigureProject()`, and `Quest3Build.BuildApk` aborts if it
  cannot - a Resources asset rather than `StreamingAssets`, because on Android
  StreamingAssets is only readable through `UnityWebRequest`. Our own notice
  is the separate `cosmic_simulation_xr_notice.txt`; Microsoft's text is not
  reworded, trimmed or merged with it. `LegalNotices` reads either at runtime
  and `LegalNoticeText` writes one into a `TMP_Text`. **Remaining:** the About
  slate has no element showing it yet (CS-117), and nobody has unpacked a
  built APK to confirm the asset is inside it.

- **Screenshots and a capture.** Needs the live app on a headset
  (`docs/COSMIC_SIMULATION_XR_ROADMAP.md` P7-T5, and CS-080/CS-081 in the
  backlog). Nothing to do here until those land.

## Decisions to make, not yet blocking

- **Meta/Oculus Platform SDK entitlement check.** Not present in the project
  at all today (no `Oculus.Platform` reference found). Optional for Meta
  store submission. If added later, `docs/store/PRIVACY_POLICY.md` needs a
  new section, since it would call Meta's servers.
- **Official age rating and comfort rating.** `docs/store/STORE_LISTING.md`
  states our design target (12+, "Comfortable" by design) but both are
  actually set by Meta's own submission questionnaire; confirm there once
  CS-080/CS-081 have put the current build in front of someone on-device.
- **Terms-of-use page.** `docs/store/ABOUT_COPY.md` leaves a Terms button as
  optional; decide whether we need one distinct from the privacy policy
  before wiring a third link into CS-086.

## Already tracked elsewhere (not duplicated here)

`docs/TECHNICAL_OVERVIEW.md` section 15 already lists the legacy intro/
placement flow, the legacy galaxy POI cards, the duplicate
`PlanetPreviewController`, and the stray `main_scene - Copy.unity` backup file
as known debt; none of those block a store submission specifically, so they
are left where they are rather than repeated here.
