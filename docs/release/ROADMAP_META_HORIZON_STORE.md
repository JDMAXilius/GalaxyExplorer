# Roadmap 2 of 3 — Publishing on the Meta Horizon Store

*Written 4 Oct 2026 from the repository (`main` at `f18fa1a`), `docs/store/*` as drafted on 12 Sep, and current
Meta developer documentation (see Sources; Meta's own pages could not be opened from the writing session, so the
facts below come from search excerpts of those pages and must be re-read in the Developer Dashboard when each
step is done). This roadmap assumes the testing roadmap (`ROADMAP_QUEST_TESTING.md`) has produced a release
APK that passes its exit criteria; nothing here replaces that. Steam is `ROADMAP_STEAM.md`, kept separate.*

Track tags: **[OWNER]** account, legal, money or a decision; **[CC]** code in the repo; **[TERM]** editor, build or
headset. Ticket numbers **CS-230 to CS-249** are reserved for this roadmap and are proposals until they are rows
in `docs/BACKLOG.md`.

## 0. Where the store work stands today

- **The store docs exist but one is now wrong.** `docs/store/STORE_LISTING.md` (copy), `ABOUT_COPY.md`,
  `STORE_READINESS_CHECKLIST.md` (CS-084) and `PRIVACY_POLICY.md` were written on 12 Sep against an app with
  *no microphone and no network*. Since CS-200 the Cosmic Being opens the microphone and streams audio to OpenAI's
  Realtime API. The privacy policy, the Data Use Checkup answers and the manifest audit all change because of it.
- **The OpenAI key would ship inside the APK.** `docs/COSMIC_BEING.md` says it plainly: the APK needs the key in
  `being_keys.asset`, anyone with the APK can read it out, and a shipping build wants a short-lived client secret
  from a small endpoint instead, "not done". A store build cannot carry the key. This is the first blocker.
- **No entitlement check.** The project has no Meta Platform SDK (`Oculus.Platform` is absent). Meta's
  VRC.Quest.Security.1 asks store apps to call the entitlement API within 10 seconds of launch.
- **Debug-signed APK.** `androidUseCustomKeystore: 0`. The store needs one release keystore, APK signature scheme
  v2, used consistently from the first upload.
- **Target API is right.** Apps created in the Developer Dashboard after 1 Mar 2026 must target API 34 (min may
  stay 32). `Quest3ProjectSetup` writes min 32 / target 34, 64-bit (ARM64), IL2CPP, Vulkan. Confirm the dashboard
  has not moved the number by the time the app is created.
- **Open store-readiness tickets from CS-084 still open:** CS-086 (About slate: remove four Microsoft links,
  re-point source and privacy), CS-114 (version on the About slate), CS-117 (MIT licence text visible in-app),
  CS-091 (verify the exported manifest). None blocks testing; all block submission.
- **App Lab no longer exists as a separate thing.** Since Aug 2024 everything is one Meta Horizon Store with the
  same submission and VRC path; there is an "Early Access" label for unfinished apps. One path to plan for.

## 1. What "published" means (exit criteria)

| # | Criterion |
|---|---|
| P1 | The app is live on the Meta Horizon Store under our own name, art and copy, with the About slate showing our links, version and licence notices |
| P2 | The published privacy policy, the DUC answers and the manifest describe the same app: microphone used for the being while summoned, audio sent to OpenAI, nothing stored, no accounts |
| P3 | No OpenAI key, or any other secret, in the APK; the being obtains a short-lived secret from our endpoint, with a per-device cap |
| P4 | VRC results all pass on the submitted build (Performance.1 72 fps, Packaging, Security.1 entitlement, Input.6/7/8, Functional.1–5, Tracking.1, Privacy.1, Asset.*) |
| P5 | Organization verified, age group self-certified, content rated, comfort and play-mode fields filled from device results, device targeting chosen |

## 2. Decisions the owner has to make first

These shape the code work, so they come before the phases.

1. **Free or paid.** Paid needs the financial account (country, business type, tax ID, W-8/W-9, bank) before
   submission and its TIN must match tax records exactly. Free skips that. The being's running cost (OpenAI
   per-minute audio) is paid by us either way; see decision 2.
2. **Who pays for the being.** Options, in order of least effort: (a) ship with the being behind a daily cap per
   device enforced by our token endpoint and a kill switch; (b) make the being a paid add-on (Meta in-app purchase)
   so its cost is covered; (c) ship v1 without the being on the store and add it later. The roadmap below assumes
   (a); (b) adds the Platform SDK IAP flow to the entitlement work; (c) removes CS-231 to CS-233 from the critical
   path.
3. **Age group.** Meta requires age group self-certification for every app. The GDD's design target is a reading age
   of 12+. Declaring a preteen audience brings child-data rules (COPPA-style) onto an app that streams voice to a
   third party. Recommended: certify **teens and adults (13+)**, keep the copy honest about a 12+ reading level, and
   let the content rating questionnaire set the displayed rating.
4. **Devices.** Quest 3 and Quest 3S share a chipset and the passthrough quality the app is designed around. Quest
   2 and Quest Pro are opt-in per build in the dashboard ("Quest binary device targeting"). Recommended: **Quest 3
   and 3S only for v1**; add Quest 2 only after a performance pass on one.
5. **Name and entity.** The privacy policy and About copy still carry placeholders for the legal name, support
   contact and the hosted URLs. Decide the publisher name (individual or business) because verification is per
   organization: Admin verification (government ID, minutes) for an individual, Business verification (incorporation
   documents) for a company.

## 3. Phases

### M1 — Account, organization, app record [OWNER]

| ID | Task | Done when |
|---|---|---|
| CS-230 | Meta developer account with 2FA; create the **organization**; complete **organization verification** (Admin or Business); create the **app** in the Developer Dashboard (platform Meta Quest), note the **App ID**; if paid, set up the financial account | App ID exists; verification status "verified" |
| — | Host the privacy policy at a public URL (GitHub Pages from `docs/store/PRIVACY_POLICY.md` is enough) once CS-234 has rewritten it | URL resolves |

### M2 — Secrets out of the build (the first blocker) [CC] + [OWNER]

The Realtime API is designed for this: a server holds the real key and mints a **client secret** with
`POST /v1/realtime/client_secrets` (default life 10 minutes, configurable 10 s to 2 h; a session already started may
outlive it). The headset opens its WebSocket with that secret instead of the key.

| ID | Track | Task | Done when |
|---|---|---|---|
| CS-231 | [OWNER] | Pick the host for a tiny endpoint. The owner already has Vercel and Supabase accounts; a Supabase Edge Function or a Vercel serverless function is one file. Set `OPENAI_API_KEY` there, never in the repo | Endpoint URL exists |
| CS-232 | [CC] | The endpoint: `POST /being/secret` → calls `client_secrets` with the session config (model, voice, instructions hash), returns `{secret, expires_at}`. Per-device daily cap keyed on an anonymous install id, a global kill switch, a short allow-list of app versions. No logging of audio or transcripts | Deployed; `curl` returns a secret; the cap trips |
| CS-233 | [CC] | `BeingKeys.cs` → fetch the secret from the endpoint URL (a setting, not a constant), use it as the bearer, refresh on expiry; the stored greeting path when the endpoint says no. Remove the key field from the shipped asset. `docs/COSMIC_BEING.md` updated | The being connects on device with no key in the APK (terminal confirms by unpacking it) |

### M3 — Store compliance in the app [CC] then [TERM]

| ID | Track | Task | Done when |
|---|---|---|---|
| CS-234 | [CC] | **Privacy policy rewrite** (`docs/store/PRIVACY_POLICY.md`): microphone only while the being is summoned and listening; audio and transcripts go to OpenAI under its API data terms; our endpoint sees an anonymous install id and a count; no accounts, no analytics; the entitlement check talks to Meta. Re-verify every "we do not" line against the code the way the 12 Sep draft did | Policy matches the code; owner publishes it (M1) |
| CS-235 | [CC] | **Entitlement check** (VRC.Quest.Security.1): add the Meta XR Platform SDK (UPM `com.meta.xr.sdk.platform` from Meta's registry; it does not need the rest of the Meta XR SDK alongside Unity OpenXR, but confirm the package pair on this Unity version), `Core.AsyncInitialize`, `Entitlements.IsUserEntitledToApplication` inside 10 s, a plain-language quit screen on failure, a desktop/editor bypass. App ID from CS-230 | Passes on a device signed in to a test user with entitlement |
| CS-236 | [CC] | **Release signing:** `Quest3Build` reads keystore path, store and alias passwords from environment variables, refuses to build a store APK debug-signed, writes the version code from `AppVersion`. Keystore kept outside the repo and backed up by the owner (losing it means a new app) | A release-signed APK; `apksigner verify --print-certs` shows our cert, scheme v2 |
| CS-237 | [CC] | **Manifest hygiene** on top of CS-091: VR intent category present, hand tracking `uses-feature` with `required` set per our decision (hands-first with controllers through the same ray path means `required="false"`), `RECORD_AUDIO` and `INTERNET` present and nothing else unexplained, `installLocation`, no debuggable flag. If Unity's auto-merge gets one wrong, a custom `Assets/Plugins/Android/AndroidManifest.xml` | `aapt dump badging` matches the policy word for word |
| CS-238 | [CC] | **Pause and resume** (Functional.2): `OnApplicationPause` stops narration, ducks music and closes the being's microphone; resume restores. **Hands hidden on tracking loss** (Input.6) if the XR Hands visuals do not already. **System gesture ignored** (Input.8) — audit `GEPointer` for anything bound to the palm pinch | Device checks in the testing roadmap's T2 pass |
| CS-086 / CS-114 / CS-117 | [TERM] | About slate: delete the four Microsoft `Hyperlink`s, re-point source and privacy to our URLs (`docs/store/ABOUT_COPY.md`), add the version element and the licence text element. Prefab surgery by `PrefabUtility.LoadPrefabContents` script | The slate shows our links, the version and both notices |
| CS-239 | [TERM] | Confirm a store build contains `Assets/Resources/legal/galaxy_explorer_license.txt` and our notice (unpack the APK), and that `Builds/`, dev scenes and the parked rework scene are not in it | Listed from the unpacked APK |

### M4 — Store listing and assets [OWNER] with [TERM] captures and `asset-smith`

Meta's asset set and sizes (re-check the dashboard's current list when uploading):

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

Rules that cause rejections: cover art with taglines or text overlays; screenshots showing UI or logos not in the
app; any imagery that is not ours (the fork's name, logo and art are never used, per the project's own rule).

| ID | Track | Task | Done when |
|---|---|---|---|
| CS-240 | [TERM] | **Screenshots** from the release build on device: one per experience, the being, a pulled planet with moons, a nebula over passthrough. Device capture is per-eye; compose to 2560 × 1440 or capture from a scene camera at that size | 8–10 candidates |
| CS-241 | [OWNER] + `asset-smith` | Branded covers, logo, icons from our own art; log sources in `Assets/_sources/CREDITS.md` | All sizes above |
| CS-242 | [OWNER] | **Trailer** under 2 minutes from device recordings; no voice-over claims the app cannot keep | Uploaded |
| CS-243 | [OWNER] | **Metadata** from `docs/store/STORE_LISTING.md`: name, short and long description trimmed to the form's limits, genre (Education), keywords, supported languages (English), play modes (standing and roomscale, sitting if T2 confirmed), input (hands, controllers), comfort rating from the device sessions, internet "optional" (only the being), supported devices (decision 4), price (decision 1) | Form complete, no field guessed |

### M5 — Questionnaires [OWNER]

| ID | Task | Done when |
|---|---|---|
| CS-244 | **Age group self-certification** (decision 3) | Submitted |
| CS-245 | **Content rating** questionnaire (IARC through the dashboard): educational, no violence, no user interaction, voice input goes to a third party | Rating issued |
| CS-246 | **Data Use Checkup**: declare microphone/voice data, purpose (answering the user), third-party processor (OpenAI), no retention by us, the privacy URL from M1. Annual recertification afterwards | DUC approved |

### M6 — Upload, test channels, submission [TERM] + [OWNER]

| ID | Track | Task | Done when |
|---|---|---|---|
| CS-247 | [TERM] | Upload the release-signed APK to the **Alpha** channel through MQDH; add tester emails (channels hold 200 by default); run the testing roadmap's T6 on the channel build; promote to **Release Candidate** | Testers installed from the store client, not adb |
| CS-248 | [OWNER] | **Submit for review** from the RC build with M4 and M5 complete. Read the automated VRC results page first; fix anything red before human review starts. Common failures to pre-empt: manifest (Packaging.1/4), entitlement (Security.1), 72 fps (Performance.1), cover art with text (Asset.*), privacy URL mismatch (Privacy.1) | Submitted; status visible |
| CS-249 | [OWNER] + [CC]/[TERM] | **Review loop:** each rejection becomes a backlog row with the VRC id in its title; fix, rebuild, re-upload, resubmit. Reviews take days to a few weeks; secondary sources vary and Meta does not promise a number | Approved; release date set |

### M7 — Launch and after

- Release at the scheduled time; check the live page, the icon, the price, the age badge.
- Updates go through the same upload → channel → submission path; the version code must increase every upload
  (`AppVersion` owns it).
- Keep the DUC and organization verification current (annual), the privacy policy in step with every behaviour
  change, and the token endpoint's cap and kill switch watched during the first weeks.

## 4. Order

M1 and M2 start now and in parallel (M1 is forms, M2 is code). M3 follows M2's CS-233 for the being parts and
can start today for the rest. M4 waits for the testing roadmap's release build. M5 waits for M1. M6 waits for all.

## 5. Risks

- **The key.** If M2 slips, the honest v1 is decision 2(c): ship without the being. Never a key in the APK.
- **OpenAI terms and age.** The API's usage policies set a minimum user age; declaring a preteen audience conflicts
  with streaming children's voices to a third party. Decision 3 avoids it.
- **Review surprises.** The two the code suggests: hand-tracking-only paths with no controller fallback (Input.7) and
  the system gesture firing a pointer press (Input.8). Both are tested in the testing roadmap's T2 before submission.
- **Licence of reused narration.** The 22 original narration clips are reused under the fork's MIT licence; confirm
  the licence file in the original repository covers the audio as well as the code before the store copy claims
  "all our own", and keep the MIT notice shipping (CS-116/117).

## Sources

- VRC guidelines: https://developers.meta.com/horizon/resources/publish-quest-req/
- Release-build manifest: https://developers.meta.com/horizon/resources/publish-mobile-manifest/
- Android 14 (API 34) requirement from 1 Mar 2026: https://developers.meta.com/horizon/blog/meta-quest-apps-android-14-march-1/
- Entitlement check (Security.1): https://developers.meta.com/horizon/documentation/unity/ps-entitlement-check/
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
