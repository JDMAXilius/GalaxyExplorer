# About-screen copy block

ASCII only, matching `docs/copy/README.md` rule 1 (the Selawik SDF fonts carry
ASCII and nothing else). This is the store-ready version of the About section
in `docs/copy/ui.md`; that file stays the short design draft, this one is the
literal text plus the placeholders a store submission needs. Wire it into
`Assets/prefabs/about_slate_prefabs/about_slate_prefab.prefab` as part of
CS-086 (see `docs/store/STORE_READINESS_CHECKLIST.md`); `CopyImporter` does
not read either file, so this has to be hand-typed into the prefab.

Anything in `{braces}` is a placeholder for the owner to fill with a real
value before this ships. Do not publish with a placeholder still in it.

---

**Title:** About Cosmic Simulation XR

**Version:** Cosmic Simulation XR {version}

*Updated 12 Sep 2026 (CS-113): the number now exists.
`Quest3ProjectSetup.AppVersion` owns it - `0.9.0` - and every build writes it
to `PlayerSettings.bundleVersion`, so `Application.version` is authoritative
(see the D-001a extension in `docs/decisions.md`). The slate still has no text
element showing it: add one and drop a `LegalNoticeText` on it with notice
`Version`, which produces exactly "Cosmic Simulation XR 0.9.0" from the
product name and version the build wrote. That is ticket CS-114 and it is
prefab-only - do not type the number in.*

**Body:**

Cosmic Simulation XR lets you hold the solar system in your own room. Pull a
planet out of the sky, grow it until it fills the space around you, and travel
from the Sun to the edge of the observable universe.

It is built on Galaxy Explorer, the open-source app Microsoft made for
HoloLens and released under the MIT License. This edition rebuilds it for
Meta Quest 3 with hand tracking and passthrough, and keeps a desktop mode for
mouse and keyboard.

Imagery from NASA, ESA and the Hubble Space Telescope. Facts from NASA
planetary fact sheets. NASA does not endorse this app.

**Attribution line (required by the MIT License; keep verbatim):**

Galaxy Explorer (c) Microsoft Corporation. Used under the MIT License. Full
licence text: License.txt in the project source, or {SOURCE_URL}/blob/main/License.txt

*Updated 12 Sep 2026 (CS-116): the full text now ships inside the app, so this
line no longer has to carry the obligation on its own. The verbatim MIT text
is `Assets/Resources/legal/galaxy_explorer_license.txt`, regenerated from the
repo-root `License.txt` by the build script; our own copyright is the separate
`cosmic_simulation_xr_notice.txt`. Do not merge the two, reword either, or
paste the MIT text into this file - a `LegalNoticeText` component with notice
`GalaxyExplorerLicense` reads it straight out of the shipped asset. The first
sentence above is the same string as `LegalNotices.GalaxyExplorerAttribution`;
if you change one, change the other. The slate element that shows the full
text is ticket CS-117 (prefab surgery); until it exists, the notice ships in
the APK but is not on screen.*

**Credits line:**

Full asset credits and licences: {SOURCE_URL}/blob/main/Assets/_sources/CREDITS.md

**Buttons and their links (all placeholders - see below):**

| Button | Destination | Note |
|---|---|---|
| Source code | {SOURCE_URL} | Our repository, e.g. https://github.com/JDMAXilius/GalaxyExplorer - replaces the `github` link, which currently points at `https://github.com/Microsoft/GalaxyExplorer` |
| Privacy | {PRIVACY_URL} | Where the owner publishes `docs/store/PRIVACY_POLICY.md`; replaces the `privacy` link, which currently points at `https://privacy.microsoft.com` |
| Terms | {TERMS_URL} | Only if we decide we need our own terms page - open question in the privacy policy draft; if we do not, remove this button rather than leave a placeholder link live |
| Close | (no link) | Dismisses the slate |

**Links to remove outright** (no equivalent in our own copy, per
`docs/copy/ui.md`'s About section, which specifies only Source code and
Privacy): `hl2_for_devs` ("HoloLens 2 for Developers"), `galaxy_explorer` and
`original_galaxy_explorer` (both link to Microsoft's Galaxy Explorer docs
pages), `microsoft_services_agreement` ("Microsoft Services Agreement"). These
four are not just wrong URLs - they are links to content that has nothing to
do with this app and should not survive into a store build.
