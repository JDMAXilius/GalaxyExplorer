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

- **[TERM] CS-090 - Decide the fate of the vendored TouchScript TUIO/OSC
  module.** `Assets/external/TouchScript/Modules/TUIO` (with `OSCsharp.dll`
  and `TUIOsharp.dll`) can open a network socket to receive touch input from
  external multitouch tables. Nothing in the project's scenes or prefabs
  wires it in today (no `TuioInput` reference found), so it is inert, but an
  automated scan of the built APK could flag the networking capability in a
  way that does not match a privacy policy that says "no network calls." Ship
  a decision either way: remove the module, or record in
  `docs/decisions.md` why it stays. Flagged [TERM] because removing part of a
  vendored package needs a compiled, verified pass in the editor before it is
  trusted.

- **Fill in the real legal details.** `docs/store/PRIVACY_POLICY.md` and
  `docs/store/ABOUT_COPY.md` both have owner-only placeholders: company/
  developer name, support contact, the actual source-code and privacy-policy
  URLs. Not a ticket - a decision only the owner can make.

## Should do before submission

- **[CC] CS-087 - Give the app an owned version number.** No version
  constant exists anywhere in the project; `PlayerSettings.bundleVersion` is
  whatever Unity's default is. Follow the pattern decision D-001a already set
  for the bundle id: own it in `Quest3ProjectSetup`, not only in Project
  Settings, so a build cannot silently revert it.

- **[TERM] CS-088 - Show the version on the About slate.** There is no text
  element for it today. Needs a new UI element in
  `about_slate_prefab.prefab` bound to the value CS-087 introduces.

- **[TERM] CS-091 - Verify the exported `AndroidManifest.xml`.** There is no
  custom manifest in `Assets/Plugins/Android`; every permission the shipped
  APK requests comes from the OpenXR/Meta package auto-merge (hand tracking,
  passthrough/AR session). Confirm the actual Gradle-exported manifest only
  requests what `docs/store/PRIVACY_POLICY.md` describes, and nothing
  unexpected (for example `RECORD_AUDIO` or `INTERNET`) arrived transitively
  from a package. Needs a real build (pairs with CS-081).

- **[CC] Bundle the MIT licence text with the build.** `License.txt` sits at
  the repo root; it is not confirmed to ship inside the built APK or be
  reachable from the About screen, and the MIT licence requires the notice to
  travel with the software. A small build step that copies it into
  `StreamingAssets` (or a "Licences" panel that reads it) would close this;
  today the About screen's attribution line only points back at the source
  repository (`docs/store/ABOUT_COPY.md`), which is weaker than shipping the
  text itself.

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
