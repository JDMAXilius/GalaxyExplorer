# Privacy policy (draft) — Cosmic Simulation XR

*Status: DRAFT, not published. Meta's store listing requires a privacy policy
hosted at a public URL; nothing in this repo can host it. The owner must post
this (or an edited version of it) somewhere public and put that URL both in
the Meta Developer Dashboard and in the About screen (see
`docs/store/ABOUT_COPY.md`), then remove this status line.*

*Version: draft 1, written 2026-09-12 against the `quest3-port` branch as it
stood for ticket CS-084. Every claim below was checked against the code or the
project settings on that date; see "How this was verified" at the end. If the
app's behaviour changes, this document has to be checked again — it is not
evergreen.*

---

## Summary

Cosmic Simulation XR is an offline, single-player educational app. It does not
have a network connection to any server we run, does not create an account,
does not collect analytics, and does not save anything about your room. The
few things it remembers are stored only on your own device and never leave it.

## What the app uses on your device

**Hand tracking.** The app reads your hand position and finger pinch through
the headset's own OpenXR hand-tracking API (`UnityEngine.XR.Hands.OpenXR.HandTracking`
and the Meta hand-tracking-aim extension, enabled in
`Assets/build_scripts/Editor/Quest3ProjectSetup.cs`). This is how pinching and
pointing at objects works. Joint positions are used for that one frame and
discarded; the app does not record, save or transmit hand data.

**Passthrough camera.** With passthrough on (the default), the headset shows
your room behind the stars. The app enables this through OpenXR's AR session
and camera features (`ARSessionFeature`, `ARCameraFeature`); in code
(`Assets/scripts/ExperienceModeManager.cs`) it only turns the `ARCameraManager`
component on or off — it never calls an image-read API (`GetLatestImage` /
`TryAcquireLatestCpuImage`; a search of the project found no such call). The
compositing of the camera feed into the passthrough view happens in the
headset's own system layer, outside this app. The app's own code never sees a
camera frame.

**Room placement.** At the start of a session you place an "Earth pin" on your
floor, which positions the content in your room for that session. This is a
one-time transform set at runtime; `Assets/scripts/WorldAnchorHandler.cs`
still contains code to persist a spatial anchor, but that code path is guarded
by `#if UNITY_WSA && !UNITY_2020_1_OR_NEWER` — the old HoloLens runtime — and
does not compile or run on Quest 3 or desktop. Nothing about your room's shape
or the pin's position is written to disk or sent anywhere; place it again next
time you open the app.

## What the app stores on your device

Three small preferences, written with Unity's `PlayerPrefs` (local device
storage, not synced or transmitted):

| Key | What it remembers |
|---|---|
| `GalaxyExplorer.ExperienceMode` | Whether you last used passthrough or full VR (`Assets/scripts/ExperienceModeManager.cs`) |
| `GalaxyExplorer.Muted` | Whether you muted the app on desktop (`Assets/scripts/menu_scripts/DesktopMenuManager.cs`) |
| `CosmicSimulation.DesktopDockVisible` | Whether the desktop dock menu is open (`Assets/scripts/experience/DesktopDock.cs`) |

None of these identify you, and none are readable outside the app on your own
device.

## What the app does not do (verified)

- **No network calls.** A search of `Assets/scripts` for `UnityWebRequest`,
  `HttpClient` and similar found none. The app makes no request to any server
  we run.
- **No analytics, ads, crash reporting or purchasing.** `ProjectSettings/UnityConnectSettings.asset`
  has every Unity cloud service explicitly turned off in this project
  (`UnityAnalyticsSettings.m_Enabled: 0`, `UnityAdsSettings.m_Enabled: 0`,
  `CrashReportingSettings.m_EnableCloudDiagnosticsReporting: 0`,
  `UnityPurchasingSettings.m_Enabled: 0`, top-level `m_Enabled: 0`). No
  third-party analytics or ad SDK is present in the project.
- **No microphone.** No code in the project calls `Microphone`; narration and
  music are pre-recorded clips played back locally, not recorded speech.
- **No eye, face or body tracking.** Only hand tracking and passthrough are
  enabled in `Quest3ProjectSetup.cs`; no eye-tracking, face-tracking or
  body-tracking OpenXR feature is turned on.
- **No accounts, no user-generated content, no multiplayer, no chat.** All
  explicitly out of scope for this app (`docs/GDD.md` section 13).
- **No entitlement check.** The project does not currently include the
  Meta/Oculus Platform SDK, so it makes no call to Meta's servers to verify
  the install. (Noted as an open question below — this may change.)

## Links you can open from the About screen

The About screen has buttons that open pages in your device's browser,
outside the app (the app calls `Application.OpenURL`, in
`Assets/scripts/Hyperlink.cs`). Once you leave the app this way, the site
you land on has its own privacy policy, which this document does not cover.
Today those links still point at Microsoft's pages, inherited from the
project this app is forked from; replacing them with our own is ticket
CS-086 (see the store-readiness checklist). Once replaced, this section
should list our own destinations by name (for example: our source repository,
this policy, and a terms page).

## Third-party imagery and credit

Body descriptions and imagery come from NASA, ESA, the Hubble Space
Telescope and Solar System Scope; the full list with licences is
`Assets/_sources/CREDITS.md`. NASA does not endorse this app. No user data is
shared with any of these sources; they are credited only as the origin of
static art assets bundled in the app.

## Vendored code with unused network capability

**Resolved 12 Sep 2026: the module was removed, so there is nothing left to
disclose here.** The project inherited Microsoft's TouchScript package for
legacy touchscreen support (`Assets/external/TouchScript`), which bundled a
TUIO/OSC module (`OSCsharp.dll`, `TUIOsharp.dll`) able to open a network socket
to receive input from external multitouch tables. Nothing referenced it, but the
two libraries were marked enabled for Android and were reachable from
`TuioInput.cs`, which compiled into the app's own assembly - so the capability
would have shipped in the APK even though no code path invoked it. The whole
module (14 files: `TuioInput.cs`, its editor, the two DLLs and their folder
metadata) was deleted. The app now bundles no library capable of opening a
network socket.

## Age and audience

The design target is a reading age of 12 and up (`docs/GDD.md` section 1);
there is no content aimed at younger children and no mechanism to collect
information from anyone, so no separate children's-privacy section is drafted
here. Confirm this against Meta's actual data-safety questionnaire answers
before submission — see open questions.

## Open questions for the owner

These are things this document cannot answer from the repository alone; do
not let the published policy assert an answer that has not been decided.

1. **Where will this policy be hosted?** Meta requires a public URL. A GitHub
   Pages page rendering this file, or a page on whatever web presence the
   product has, both work; nothing in the repo can host it.
2. **Company/developer name and contact.** This draft has no real-world legal
   entity, support email or address to publish. Needed before this can be
   posted publicly or submitted to Meta.
3. **Will the Meta/Oculus Platform SDK (entitlement check) be added later?**
   If so, it does make a network call to Meta's servers to verify the
   purchase/install, and this document needs a line added for it. Not present
   today.
4. **Will any crash or ANR reporting be added on Android** (Play-services-style
   or Meta's own), beyond the disabled Unity Cloud Diagnostics already in the
   project? None is configured today; if one is added, this document needs an
   update before the next submission.
5. **Should the vendored TouchScript TUIO/OSC module be removed** rather than
   just left unwired? See "Vendored code with unused network capability" above.
6. **Official age rating and content descriptors.** Meta's own questionnaire
   produces the store's displayed age rating; this document states our
   design target (12+) but does not attempt to answer Meta's questionnaire on
   the owner's behalf.
7. **Data-deletion / contact process.** Since nothing is collected off-device,
   there is nothing to delete on request today; if that changes (see 3 and 4),
   this section needs a real contact process.

## How this was verified

Grep-based search of `Assets/scripts/**/*.cs` for `UnityWebRequest`,
`HttpClient`, `PlayerPrefs`, `Microphone`, `Analytics`, `File.Write` and
similar; reading of `Assets/build_scripts/Editor/Quest3ProjectSetup.cs` for
the enabled OpenXR features; reading of
`ProjectSettings/UnityConnectSettings.asset` for Unity cloud service state;
reading of `Assets/scripts/ExperienceModeManager.cs`,
`Assets/scripts/WorldAnchorHandler.cs` and `Assets/scripts/Hyperlink.cs` in
full; a search of `Packages/manifest.json` for analytics/ads packages; and a
search of `Assets` for third-party plugin DLLs and Oculus Platform SDK files.
No live build was run and no on-device network capture was taken — that
verification (a Quest build with a packet capture during a full session)
belongs with CS-081/CS-085, not this ticket.
