# Roadmap 2 of 3 — Publishing on the Meta Horizon Store

*Written 4 Oct 2026, expanded the same day, from the repository (`main` at `f18fa1a`), `docs/store/*` as drafted
on 12 Sep, and current Meta developer documentation (Sources at the end; Meta's own pages could not be opened
from the writing session, so the facts below come from search excerpts of those pages and must be re-read in the
Developer Dashboard when each step is done). This roadmap assumes `ROADMAP_QUEST_TESTING.md` has produced a
release APK that passes its exit criteria. Steam is `ROADMAP_STEAM.md`, kept separate.*

Track tags: **[OWNER]** account, legal, money or a decision; **[CC]** code in the repo; **[TERM]** editor, build or
headset. Tickets **CS-230 to CS-249** are reserved here and are proposals until they are backlog rows. Each ticket
has a **Goal**, atomic **Objectives**, **Needs**, **Evidence** and **If it fails**.

## Status and corrections, 6 Oct 2026

Re-read against Meta's pages on 6 Oct 2026 (they opened this time). Where this block and the text below
disagree, this block wins; the lines below that were wrong have been corrected in place and say so.

**Decided (D-012 in `docs/decisions.md`):** D1 **free** for v1 · D3 **13+** (`TEENS_AND_ADULTS`) · D4 **Quest 3
and Quest 3S only** · D5 publisher is the owner as an individual · **no Meta Platform SDK** in v1.

**App record exists:** "Cosmic Simulation XR", **App ID `1436957446161428`**, organization **"Juan Diego Lugo"**
(individual; admin identity verification, no business verification). Bundle id
`com.jdmaxilius.cosmicsimulationxr`, version 0.9.0 (900).

| What the roadmap said | What Meta's documentation says today | Source |
|---|---|---|
| Entitlement check is asked of store apps (Security.1 in P4, CS-235) | **Recommended, not required**: "Apps are not required to perform a platform entitlement check." CS-235 is dropped from v1 (free app, no Platform SDK) | https://developers.meta.com/horizon/resources/vrc-quest-security-1/ |
| `installLocation` is internal (CS-237) | "must be set to `auto`" - which is what `Quest3ProjectSetup` already writes | https://developers.meta.com/horizon/resources/publish-mobile-manifest/ |
| Input.6 (hands lost) is a VRC to pass (P4, CS-238) | **VRC.Quest.Input.6 is retired.** Hiding a lost hand is still good behaviour, not a review item | https://developers.meta.com/horizon/resources/publish-quest-req/ |
| Platform "Meta Quest" / "Meta Quest Store" | The store is the **Meta Horizon Store** | same page |
| Testers "see it in their library" once uploaded (CS-247) | "Initially, the Alpha, Beta, and Release Candidate channels are empty and contain no users - not even yourself." Add your own account first | https://developers.meta.com/horizon/resources/publish-release-channels/ |
| Upload via MQDH only | The CLI works too and **`--age-group` is required** (`TEENS_AND_ADULTS`, `MIXED_AGES`, `CHILDREN`); without it the upload lands as a draft | https://developers.meta.com/horizon/resources/publish-reference-platform-command-line-utility/ |
| (not listed) | `android:excludeFromRecents="true"` on the launch activity is required; supported devices are named `quest3`, `quest3s` | publish-mobile-manifest, above |
| (not listed) | **VRC.Quest.Functional.14 (required):** apps that can launch in passthrough show a passthrough loading screen when launched from passthrough Home | https://developers.meta.com/horizon/resources/vrc-quest-functional-14/ |

**Done in the repo on 6 Oct 2026 (needs one editor build to verify, see CS-237):** CS-236 release signing from
the environment (`Quest3Build.BuildStoreApk`, `docs/release/RELEASE_SIGNING.md`); manifest: devices limited to
Quest 3/3S, `excludeFromRecents`, `BLUETOOTH` removed (`Quest3Manifest.cs`, `Quest3ProjectSetup.ConfigureStoreFeatures`);
passthrough loading screen switched on; CS-234 privacy policy rewritten; store listing updated.

**CLI upload, for reference** (the app secret or user token comes from the dashboard's API page and is never
committed):

```
ovr-platform-util upload-quest-build --age-group TEENS_AND_ADULTS --app-id 1436957446161428 --token <user token> --apk Builds\Quest3\CosmicSimulationXR-store.apk --channel alpha
```

## 0. Where the store work stands today

- **The store docs exist but one is now wrong.** `docs/store/STORE_LISTING.md`, `ABOUT_COPY.md`,
  `STORE_READINESS_CHECKLIST.md` (CS-084) and `PRIVACY_POLICY.md` were written on 12 Sep against an app with *no
  microphone and no network*. Since CS-200 the Cosmic Being opens the microphone (`Assets/Being/Mic.cs`) and
  streams audio to OpenAI's Realtime API (`Session.cs`, `Realtime.cs`). The privacy policy, the Data Use Checkup
  and the manifest audit all change because of it.
- **The OpenAI key would ship inside the APK.** `docs/COSMIC_BEING.md` says it: the APK needs the key in
  `being_keys.asset`, anyone with the APK can read it out, and a shipping build wants a short-lived client secret
  from a small endpoint, "not done". `Realtime.cs` sends it as `Authorization: Bearer`. First blocker.
- **No entitlement check, and none is needed for v1.** No `Oculus.Platform` anywhere. VRC.Quest.Security.1 is
  *Recommended*, not required (corrected 6 Oct 2026); the app is free, so there is no revenue to protect.
- **The bench APK is debug-signed; the store build is not.** *Build Store APK* signs from the environment
  (CS-236, done 6 Oct 2026). The owner still has to create the keystore (`docs/release/RELEASE_SIGNING.md`).
- **Target API is right.** Apps created in the Developer Dashboard after 1 Mar 2026 must target API 34 (min may
  stay 32). `Quest3ProjectSetup` writes min 32 / target 34, ARM64, IL2CPP, Vulkan.
- **No pause handling** anywhere in the app (Functional.2).
- **Open store-readiness tickets from CS-084:** CS-086 (About slate links), CS-114 (version on the slate), CS-117
  (licence text in-app), CS-091 (exported manifest). None blocks testing; all block submission.
- **App Lab no longer exists as a separate thing.** Since Aug 2024 everything is one Meta Horizon Store with one
  submission and VRC path; an "Early Access" label exists for unfinished apps.

## 1. What "published" means (exit criteria)

| # | Criterion |
|---|---|
| P1 | Live on the Meta Horizon Store under our own name, art and copy; the About slate shows our links, version and licence notices |
| P2 | The published privacy policy, the DUC answers and the manifest describe the same app: mic only while the being listens, audio to OpenAI, nothing stored, no accounts |
| P3 | No OpenAI key or other secret in the APK; the being obtains a short-lived secret from our endpoint, with a per-device cap |
| P4 | Every required VRC passes on the submitted build (Performance.1, Packaging.1–6, Input.7/8, Functional.1–5 and 14, Tracking.1, Privacy.1–5, Asset.*). Security.1 is recommended only; Input.6 is retired |
| P5 | Organization verified; age group self-certified; content rated; comfort, play modes and devices filled from device results |

## 2. Decisions the owner has to make first

These shape the code work, so they come before the phases. Each is a yes/no or a pick; the recommendation is
first. **D1, D3, D4 and D5 were decided as recommended on 6 Oct 2026 (D-012); D2 is still open.**

| # | Decision | Recommendation | What changes if not |
|---|---|---|---|
| D1 | Free or paid | **Free for v1**; price later with an update | Paid adds the financial account (tax ID, W-8/W-9, bank) to M1 and makes the entitlement check protect revenue |
| D2 | Who pays for the being's OpenAI minutes | **(a) ship it behind a daily per-device cap** enforced by our endpoint, with a kill switch | (b) paid add-on via Meta IAP: adds the Platform SDK purchase flow to CS-235; (c) ship v1 without the being: CS-231–233 leave the critical path and the mic and INTERNET permissions go away |
| D3 | Age group | **Teens and adults (13+)**; keep the copy's "12+ reading level" as prose | A preteen declaration brings child-data rules onto an app that streams voice to a third party |
| D4 | Devices | **Quest 3 and Quest 3S only** (same chipset, passthrough quality the app is designed for) | Quest 2 / Pro each need their own T4 performance pass first |
| D5 | Publisher identity | Pick the name on the store and the legal entity; the privacy policy and About copy carry placeholders for it | Admin verification (ID) for an individual; Business verification (incorporation papers) for a company |

## 3. Phases

### M1 — Account, organization, app record

**Phase goal:** a verified organization with an app record, an App ID the code can use, and a public URL for the
privacy policy.

#### CS-230 [OWNER] Developer account → organization → verification → app

- **Goal:** the dashboard side exists and is allowed to publish.
- **Objectives:**
  1. Meta developer account with two-factor authentication; the same account used on the headset in testing.
  2. Create the **organization** under the chosen name (D5).
  3. **Organization verification:** Admin verification (government ID, usually minutes) for an individual, or
     Business verification (documents) for a company. Unverified organizations cannot publish or recertify.
  4. Create the **app** on the Meta Horizon Store; name "Cosmic Simulation XR". **Done: App ID
     `1436957446161428`, organization "Juan Diego Lugo".** Identity verification is still to do (step 3).
  5. If paid (D1): financial account with country, business type, name, address, tax ID; the TIN must match
     tax records exactly or payouts fail.
- **Needs:** D1, D5.
- **Evidence:** screenshot of the app record with its App ID; verification status "verified".
- **If it fails:** verification rejected → fix the name/ID mismatch and resubmit; nothing else in M1–M3 waits on it
  except the final upload.

#### Privacy policy URL [OWNER]

- **Goal:** `docs/store/PRIVACY_POLICY.md`, once rewritten (CS-234), reachable at a stable public URL.
- **Objectives:** enable GitHub Pages on the repository (or any static host); render the Markdown; put the URL in
  the dashboard's privacy field and in `docs/store/ABOUT_COPY.md` for CS-086.
- **Evidence:** the URL loads from a phone with no login.

### M2 — Secrets out of the build

**Phase goal:** the APK carries no key; the being gets a short-lived secret from an endpoint we control that can
also say no.

How the API supports this: a server holding the real key calls `POST /v1/realtime/client_secrets` and gets a
secret whose life is configurable (default 10 minutes; 10 s to 2 h); a session started with it may continue after
it expires; the client opens the WebSocket with that secret as the bearer.

#### CS-231 [OWNER] Choose and provision the endpoint host

- **Goal:** one place to run a 50-line function with a secret environment variable.
- **Objectives:**
  1. Pick Supabase Edge Functions or Vercel Functions (both accounts already exist).
  2. Set `OPENAI_API_KEY` as a secret there; never in the repo, never in `being_keys.asset`.
  3. Set a `BEING_KILL_SWITCH` flag and a `BEING_DAILY_CAP` number as environment variables so they can change
     without a deploy.
- **Evidence:** the project exists with the variables set; the URL is known.

#### CS-232 [CC] The token endpoint

- **Goal:** `POST /being/secret` returns a client secret, or a reason not to.
- **Objectives:**
  1. Request body: `{installId, appVersion, platform}`; `installId` is a random id the app makes once and keeps in
     `PlayerPrefs` (not a device identifier).
  2. Checks, in order: kill switch off; `appVersion` in the allow-list; `installId`'s count today under the cap
     (a tiny key-value table keyed `installId:date`).
  3. Call `client_secrets` with the session config the app uses today (model, voice, `session.update` fields the
     app would otherwise send) and `expires_after` of 10 minutes.
  4. Respond `{secret, expiresAt}` or `{deny: "cap" | "off" | "version"}` with HTTP 200 either way, so the app
     never treats a deny as a crash.
  5. Log counts only; never audio, transcripts or the secret.
  6. `tools/being-endpoint/` in the repo with a README and a `curl` smoke; the deploy command documented.
- **Needs:** CS-231.
- **Evidence:** `curl` returns a secret; a second `curl` past the cap returns `deny: cap`.
- **If it fails:** a provider limitation (e.g. no persistent store) → the cap becomes per-process in-memory for v1
  and the note says so.

#### CS-233 [CC] The app uses the endpoint

- **Goal:** `Assets/Being/` connects with a client secret and never looks for a key in an asset in a store build.
- **Objectives:**
  1. `BeingKeys.cs` → `BeingAccess.cs`: holds the endpoint URL (a `BeingSettings` field), the install id, and
     `GetSecret()` that fetches, caches until `expiresAt` minus 30 s, and refreshes.
  2. `Realtime.cs` takes the bearer from `GetSecret()`; `Session.cs` keeps the model query.
  3. Deny or network failure → the stored greeting path (already exists for "no key"), then the window closes.
  4. The editor and desktop keep a developer bypass: `OPENAI_API_KEY` in the environment still works in the
     editor only, guarded by `#if UNITY_EDITOR`.
  5. Delete the key field from the shipped asset; `.gitignore` keeps the old entries so a stray local file never
     commits.
  6. `docs/COSMIC_BEING.md` "Running it" rewritten.
- **Needs:** CS-232.
- **Evidence:** terminal unpacks a store build and finds no key string; the being connects on device through the
  endpoint; past the cap it greets from the clip.
- **If it fails:** D2(c): ship without the being; `Mic.cs` and the WebSocket leave the build so the permissions go
  with them.

### M3 — Store compliance in the app

**Phase goal:** the app tells the truth about itself (policy, manifest, notices), passes Meta's security and
functional checks, and is signed the way the store will see it forever.

#### CS-234 [CC] Privacy policy rewrite

- **Goal:** a policy that matches the code line by line, the way the 12 Sep draft did.
- **Objectives:**
  1. New sections: *Microphone* (opens only while the being is summoned and listening; closed while it speaks;
     armed state explained); *Voice data sent to OpenAI* (audio frames and transcripts, under OpenAI's API data
     terms; not stored by us); *Our token service* (receives an anonymous install id, app version and a count;
     no audio); *Entitlement check* (talks to Meta to confirm the install).
  2. Remove "no microphone", "no network" claims; keep the verified "no analytics, no accounts, no camera image
     read" lines after re-checking each with a grep.
  3. The three `PlayerPrefs` keys table gains `installId` and the being's prefs.
  4. Fill D5's name and contact; a data-deletion contact line (nothing to delete, but the contact must exist).
  5. Date the version; the "How this was verified" section lists the greps run.
- **Needs:** D2, D5.
- **Evidence:** the file; the owner publishes it (M1).
- **If it fails:** an unanswerable item stays in "Open questions" and is not asserted.

#### CS-235 [CC] Entitlement check (VRC.Quest.Security.1) — NOT IN v1

*Dropped 6 Oct 2026 (D-012): the check is Recommended, not required, the app is free, and it would bring in the
Meta Platform SDK for nothing else. Kept below for the day the app is paid.*

- **Goal:** within 10 s of launch the app asks Meta whether this user is entitled, and quits politely if not.
- **Objectives:**
  1. Add the Meta XR Platform SDK as a UPM package (`com.meta.xr.sdk.platform`, from Meta's scoped registry or
     tarball); confirm it coexists with `com.unity.xr.meta-openxr` on 6000.6 without the Meta Core SDK.
  2. *Oculus Platform Settings* asset: the App ID from CS-230 for Quest; a test user for the editor.
  3. `Entitlement.cs` on the app root: `Core.AsyncInitialize(appId)` then `Entitlements.IsUserEntitledToApplication()`
     in `Start`; a 10 s watchdog.
  4. Failure → a one-line notice via `SwitchNotice` or a dedicated slate, then `Application.Quit()` after 5 s.
     Success → nothing visible.
  5. `#if UNITY_EDITOR || UNITY_STANDALONE` bypass so desktop and the editor never call it (Steam has its own
     store).
  6. Privacy policy line added (CS-234 point 1).
- **Needs:** CS-230's App ID.
- **Evidence:** on a device signed in to a test user with the app in a release channel, the check passes; signed
  out, the notice shows and the app quits.
- **If it fails:** the package pair conflicts → the tarball install of the Platform SDK alone; it has no
  dependency on the Core SDK.

#### CS-236 [CC] Release signing — code done 6 Oct 2026

*Implemented as `Quest3Build.BuildStoreApk` → `Builds/Quest3/CosmicSimulationXR-store.apk`; the keystore
settings are applied for that build only and restored afterwards. Owner steps and the exact commands:
`docs/release/RELEASE_SIGNING.md`. Remaining: the owner's keystore, and the `apksigner` evidence.*

- **Goal:** one keystore, outside the repo, used by every store upload, with the build refusing to proceed without it.
- **Objectives:**
  1. Owner generates the keystore once (`keytool -genkeypair -keyalg RSA -keysize 2048 -validity 10000`) and
     backs it up in two places; losing it means a new app on the store.
  2. `Quest3Build.cs` reads `COSMIC_KEYSTORE_PATH`, `COSMIC_KEYSTORE_PASS`, `COSMIC_KEYALIAS`,
     `COSMIC_KEYALIAS_PASS` from the environment and sets `PlayerSettings.Android.useCustomKeystore`,
     `keystoreName`, `keystorePass`, `keyaliasName`, `keyaliasPass`.
  3. A `store` flag (menu item *Build Store APK* or `-store` batch arg): without the four variables it refuses;
     the plain *Build APK* keeps debug signing for the bench.
  4. Version code still from `AppVersion`; the store rejects a re-used code, so every upload bumps the patch.
  5. README line in `docs/TECHNICAL_OVERVIEW.md` build section.
- **Needs:** the owner's keystore.
- **Evidence:** `apksigner verify --print-certs` shows our certificate and scheme v2 on the store APK.
- **If it fails:** a wrong password fails the Gradle step with a clear message; nothing silent.

#### CS-237 [CC] Manifest hygiene

- **Goal:** the exported manifest requests exactly what the policy describes, nothing more.
- **Objectives:**
  1. From the testing roadmap's CS-214 dump, list every `uses-permission` and `uses-feature`.
  2. Expected set: `RECORD_AUDIO`, `INTERNET`, `MODIFY_AUDIO_SETTINGS` (Unity adds it with the microphone),
     `com.oculus.permission.HAND_TRACKING`, `oculus.software.handtracking` (required="false": hands-first,
     controllers through the same ray path), `com.oculus.feature.PASSTHROUGH`, the VR intent category and
     `headtracking` feature from the Meta Quest feature. `ACCESS_NETWORK_STATE` is acceptable if a package adds
     it; anything else (`BLUETOOTH`, location, storage, camera image access) is a finding.
  3. Corrections go in `Assets/build_scripts/Editor/Quest3Manifest.cs`, an XR Management
     `IAndroidManifestRequirementProvider` (the route the OpenXR package itself uses), not a custom
     `AndroidManifest.xml`. As of 6 Oct 2026 it sets `excludeFromRecents="true"` on the launch activity, writes
     `com.oculus.supportedDevices` = `quest3|quest3s`, and removes `BLUETOOTH`.
  4. Confirm `android:debuggable` is absent, `installLocation` is **auto** (Meta's requirement; corrected 6 Oct
     2026 - it was written here as internal), `com.oculus.ossplash.background` is `passthrough-contextual`
     (Functional.14), and the category is VR-only (not a 2D panel app) unless the owner decides otherwise.
- **Needs:** CS-214.
- **Evidence:** `aapt dump badging` matches the policy word for word; pasted into the row.
- **If it fails:** a permission that cannot be removed because a package needs it → the policy documents it.

#### CS-238 [CC] Pause, hands lost, system gesture

- **Goal:** the three VRC behaviours the code does not have yet (one implementation shared with the testing
  roadmap's CS-221).
- **Objectives:**
  1. **Functional.2:** `AppLifecycle.cs` with `OnApplicationPause(bool)`: on pause `VOManager.Stop()`,
     `AmbienceController.StopBed()` (or duck), the being's session closed and mic released; on resume, music back,
     mic re-armed if the being is out, nothing else re-triggered.
  2. **Hands lost (was Input.6, retired by Meta - polish, no longer a review item):** hand visuals and `GEPointer` ray hidden when the hand's tracking state is lost; confirm in
     `ge_xr_rig.prefab` whether XR Hands already does the visual half.
  3. **Input.8:** drop any press that arrives while the Meta aim extension reports the system gesture; audit
     `GEPointer` and `XRInputRig` for the aim flags.
- **Evidence:** the testing roadmap's CS-219 items 1, 3, 4 pass on device.

#### CS-086 / CS-114 / CS-117 [TERM] About slate

- **Goal:** the slate shows our links, our version, and both legal notices; no Microsoft link remains.
- **Objectives:**
  1. A `PrefabUtility.LoadPrefabContents` script over `about_slate_prefab.prefab`: delete `hl2_for_devs`,
     `galaxy_explorer`, `original_galaxy_explorer`, `microsoft_services_agreement`; re-point the source-code and
     privacy `Hyperlink`s to our URLs (from `docs/store/ABOUT_COPY.md` and M1).
  2. Add a `LegalNoticeText` element set to `Version` (fills from `Application.version`).
  3. Add a scrollable `LegalNoticeText` for the MIT licence and our notice (the assets `Resources/legal/*` already
     ship, CS-116).
  4. Capture the slate on device.
- **Evidence:** the capture; the three rows done.

#### CS-239 [TERM] What is in the store build

- **Goal:** the store APK contains the legal text and none of the development scenes.
- **Objectives:** unzip the store APK; list `assets/`; confirm `Resources/legal` texts present; confirm the
  parked rework scene and any test scene are not in the scene list (`EditorBuildSettings` enabled scenes only);
  confirm no `being_keys`.
- **Evidence:** the listing in the row.

### M4 — Store listing and assets

**Phase goal:** everything the listing form asks for, in the sizes it asks for, from our own art and from the
release build, with no claim the app cannot keep.

Meta's asset set (re-check the dashboard's list when uploading):

| Asset | Size |
|---|---|
| Hero cover 10:3 | 3000 × 900 PNG |
| Cover landscape 16:9 | 2560 × 1440 PNG |
| Cover square 1:1 | 1440 × 1440 PNG |
| Cover portrait 7:10 | 1008 × 1440 PNG |
| Mini landscape 3:1 | 1080 × 360 PNG |
| Logo, transparent | up to 9000 × 1440 PNG-32 |
| Icon 1:1 | 512 × 512 PNG |
| Spatialized icon | 180 × 180 PNG, transparent |
| Screenshots 16:9 | 2560 × 1440 PNG, at least 5 |
| Trailer | 16:9, 1080p–2K, MP4 H.264/AAC, under 2 minutes, plus a 2560 × 1440 cover frame |

Rejection causes to design around: cover art with taglines, text overlays or banners; screenshots showing UI,
logos or icons not in the app; any imagery that is not ours.

#### CS-240 [TERM] Screenshots

- **Goal:** 8–10 real captures at 2560 × 1440 from the release build.
- **Objectives:**
  1. One per experience (7), the being beside the player, Jupiter pulled with its moons and card, a nebula
     overlay over passthrough.
  2. Device capture is per-eye and square-ish; either compose the left eye to 16:9 at 2560 × 1440, or add an
     editor-only scene camera at that resolution driven from the same head pose on a Link session (full VR only;
     passthrough shots must come from the device).
  3. No debug HUD, no OVR Metrics overlay, no editor gizmos.
- **Evidence:** the PNGs in `docs/store/assets/` (or a shared folder), each named by scene.

#### CS-241 [OWNER] + `asset-smith` Branded art

- **Goal:** the ten branded sizes from our own art.
- **Objectives:** one master key-art composition (a planet in a hand over a room, our name as a wordmark); derive
  every size; the logo transparent; icons readable at 180 px; no taglines on covers; log sources and generators in
  `Assets/_sources/CREDITS.md`.
- **Evidence:** all sizes exported, checked against the table.

#### CS-242 [OWNER] Trailer

- **Goal:** under 2 minutes, real device footage, no claim the app cannot keep.
- **Objectives:** 6–8 device clips (MQDH capture), cut to 60–90 s; the being speaking once; music from the app's
  own beds; captions only for facts; export 1080p H.264/AAC; a 2560 × 1440 cover frame.
- **Evidence:** the MP4 and cover uploaded.

#### CS-243 [OWNER] Metadata

- **Goal:** every form field filled from a document or a device result, none guessed.
- **Objectives:**
  1. Name, short and long description from `docs/store/STORE_LISTING.md`, trimmed to the form's limits.
  2. Genre Education; keywords (solar system, planets, nebula, galaxy, astronomy, mixed reality); language English.
  3. Play modes from CS-219 item 7; input: hands and controllers; comfort rating from the device sessions
     (expected Comfortable: no artificial locomotion).
  4. Internet: "optional" (the being only); supported devices per D4; price per D1.
  5. Privacy policy URL from M1; support contact from D5.
- **Evidence:** the form saved; a copy of the final text in `docs/store/`.

### M5 — Questionnaires

**Phase goal:** the three compliance forms answered consistently with the policy and the code.

#### CS-244 [OWNER] Age group self-certification

- **Goal:** D3 declared.
- **Objectives:** answer the age-group form as teens and adults; the social-features section is "none" (no chat,
  no multiplayer); keep a copy of the answers.
- **Evidence:** submitted; status shown in the dashboard.

#### CS-245 [OWNER] Content rating

- **Goal:** the IARC rating issued through the dashboard.
- **Objectives:** educational content, no violence, no user-generated content, no user interaction between
  players, voice input processed by a third party (OpenAI) — answer the "shares data" and "voice" questions the
  same way the policy does.
- **Evidence:** the rating certificate in the dashboard.

#### CS-246 [OWNER] Data Use Checkup

- **Goal:** the DUC approved with the being's data flow declared.
- **Objectives:** declare microphone/voice audio and transcripts; purpose: answering the user's questions;
  processor: OpenAI; retention by us: none; the install id as a non-identifying app id; the privacy URL; the
  annual recertification date in the calendar.
- **Evidence:** DUC status approved.
- **If it fails:** a DUC rejection names the mismatch; fix the policy or the answer, never both silently.

### M6 — Upload, channels, submission

**Phase goal:** the store sees the final certificate from the first upload, testers get the build from the store
client, and submission happens with every VRC already green.

#### CS-247 [TERM] Alpha → RC

- **Goal:** the release-signed APK on a channel, installed by invited testers from the store client.
- **Objectives:**
  1. Upload to **Alpha** via MQDH, or the CLI with `--age-group TEENS_AND_ADULTS` (required; see the command
     at the top). The upload checks the manifest; read the result page.
  2. Channels start with **no users, not even the owner**: add the owner's own Meta account email to Alpha
     first, then the testers'; confirm each sees it in their library.
  3. Run the testing roadmap's T6 on the channel build; fix; re-upload with a bumped version code.
  4. Promote the passing build to **Release Candidate**.
- **Evidence:** the channel page; two tester confirmations.
- **If it fails:** an upload rejection names the manifest line; CS-237.

#### CS-248 [OWNER] Submit for review

- **Goal:** a submission with nothing red on the automated results page.
- **Objectives:**
  1. In the dashboard, confirm M4 and M5 are complete and the RC build is selected.
  2. Read the automated VRC results; each red line is a backlog row before human review (common ones: manifest
     Packaging.1/2, passthrough loading screen Functional.14, 72 fps Performance.1, cover art text Asset.*,
     privacy policy Privacy.1–4).
  3. Submit; note the date.
- **Evidence:** the submission status and date.

#### CS-249 [OWNER] + [CC]/[TERM] Review loop

- **Goal:** each rejection becomes one row, one fix, one re-upload, one resubmission.
- **Objectives:** title the row with the VRC id; the fix is [CC] or [TERM] by nature; bump the version code; re-run
  the affected T-phase check; resubmit. Reviews take days to weeks; no number is promised.
- **Evidence:** approved; release date set.

### M7 — Launch and after

- Release at the scheduled time; check the live page, icon, price, age badge, device list.
- Updates: upload → channel → submission, version code always increasing (`AppVersion` owns it).
- Annual: DUC recertification, organization verification currency.
- Every behaviour change to the being lands in the policy before the next submission.
- Watch the endpoint's counts and the kill switch for the first weeks.

## 4. Order

M1 and M2 start now, in parallel (forms and code). M3's CS-234 and CS-238 can start today; CS-233 waits for CS-232;
CS-235 is out of v1. M4 waits for the testing roadmap's release build. M5 waits for M1 and CS-234. M6 waits
for all of it.

## 5. Risks

- **The key.** If M2 slips, the honest v1 is D2(c). Never a key in the APK.
- **OpenAI terms and age.** The API's usage policies set a minimum user age; a preteen declaration conflicts with
  streaming children's voices to a third party. D3 avoids it.
- **Review surprises the code predicts:** no pause handler (Functional.2), hand-only paths with no controller
  fallback (Input.7), the system gesture reaching a pointer press (Input.8), and Unity's own splash screen
  drawing an opaque frame between the passthrough system loading screen and the first scene (Functional.14). All tested in the testing roadmap's
  CS-219 before submission.
- **Licence of reused narration.** The 22 original clips ride on the fork's MIT licence; confirm it covers the
  audio before the store copy claims "all our own", and keep the MIT notice shipping (CS-116/117).

## Sources

- VRC guidelines: https://developers.meta.com/horizon/resources/publish-quest-req/
- Release-build manifest: https://developers.meta.com/horizon/resources/publish-mobile-manifest/
- Android 14 (API 34) requirement from 1 Mar 2026: https://developers.meta.com/horizon/blog/meta-quest-apps-android-14-march-1/
- Entitlement check (Security.1): https://developers.meta.com/horizon/documentation/unity/ps-entitlement-check/
- Security.1 status (Recommended): https://developers.meta.com/horizon/resources/vrc-quest-security-1/
- Functional.14 passthrough loading screen: https://developers.meta.com/horizon/resources/vrc-quest-functional-14/
  and https://developers.meta.com/horizon/documentation/native/android/mobile-passthrough-loading-screens/
- Splash screen best practices (artwork within 1500 × 1500 px): https://developers.meta.com/horizon/resources/mr-splash-screen-bp/
- Command-line upload (`--age-group`): https://developers.meta.com/horizon/resources/publish-reference-platform-command-line-utility/
- App submission review process: https://developers.meta.com/vr/blog/app-submission-review-process-guide/
- Release channels: https://developers.meta.com/vr/resources/publish-release-channels/
- Device targeting: https://developers.meta.com/horizon/resources/publish-release-channels-device-targeting/
- Organization verification: https://developers.meta.com/vr/resources/publish-organization-verification/
- Financial account: https://developers.meta.com/vr/resources/publish-account-management-bank-tax/
- Age group self-certification: https://developers.meta.com/horizon/policy/age-groups/
- Data Use Checkup: https://developers.meta.com/horizon/resources/publish-data-use/
- Asset design guidelines: https://developers.meta.com/vr/resources/asset-guidelines/
- App Lab folded into the store: https://developers.meta.com/vr/blog/get-apps-ready-app-lab-meta-horizon-store-meta-quest-developers/
- OpenAI Realtime client secrets: https://developers.openai.com/api/reference/resources/realtime/subresources/client_secrets/methods/create
