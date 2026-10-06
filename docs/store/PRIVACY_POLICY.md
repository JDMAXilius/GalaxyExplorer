# Privacy policy — Cosmic Simulation XR

*Status: DRAFT, not published. Meta requires this at a public URL (VRC.Quest.Privacy.1). Before publishing, the
owner fills the two placeholders marked **[OWNER: …]**, hosts the page, puts the URL in the Meta Developer
Dashboard and on the About screen (`docs/store/ABOUT_COPY.md`), and deletes this status paragraph and the
"Maintainer notes" section at the end.*

**Publisher:** [OWNER: publisher name as shown on the Meta Horizon Store]
**Contact:** [OWNER: contact email address]
**Last updated:** 6 October 2026 (app version 0.9.0)

---

## Summary

Cosmic Simulation XR is a single-player educational app for Meta Quest 3 and 3S. It has no accounts, no
advertising and no analytics, and we run no servers. Everything works offline except one optional feature: the
**voice guide**. When you summon the guide and speak to it, your voice is sent over the internet to OpenAI,
which produces the spoken answer. If you never summon the guide, the app sends nothing anywhere.

## What data the app handles

### Your voice (only when you use the voice guide)

- The app asks for the headset's microphone permission the first time you summon the guide. You can refuse; the
  rest of the app works without it.
- The microphone is open only while the guide is out. Your speech is sent while the guide is listening - a few
  seconds after it greets you, after each answer, or when you tap it - and not while it is speaking. Dismissing
  the guide closes the microphone and the connection.
- What is sent: the audio of what you say, and a one-line description of what you are looking at in the app
  (for example which place is open and which planet you are holding) so the answer fits the moment.
- Where it goes: directly from your headset to **OpenAI's Realtime API** (`api.openai.com`) over an encrypted
  connection. OpenAI turns the audio into text, generates the answer and sends back the guide's voice. OpenAI
  processes this as our service provider under its API data terms
  (https://openai.com/policies/privacy-policy and https://openai.com/enterprise-privacy), which state that API
  data is not used to train its models by default and may be retained for a limited period for abuse monitoring.
- What we keep: **nothing.** Audio and transcripts are not written to the headset's storage and are not sent to
  us; we have no server that could receive them. The conversation exists in memory until you dismiss the guide.
- The microphone settings row can show a live input level so you can check the microphone works. That audio
  stays on the headset and is not sent anywhere.

### Preferences stored on your headset

The app saves a few settings locally (Unity `PlayerPrefs`) so they are remembered next time. They never leave
the headset and do not identify you:

| What is remembered | Stored as |
|---|---|
| Passthrough or dark sky | `CosmicSimulation.PassthroughForced` |
| Sound muted; narration muted | `Cosmic.Muted`, `Cosmic.VoiceMuted`, `GalaxyExplorer.Muted`, `GalaxyExplorer.NarrationMuted` |
| Text size | `Cosmic.TextScale`, `CosmicSimulation.PanelTextScale` |
| Hint cards already shown; labels shown | `Cosmic.HintsSeen`, `CosmicSimulation.HintsSeen`, `Cosmic.LabelsVisible` |
| Which microphone to use | `Companion.Microphone` |
| Desktop menu open (desktop test build only) | `CosmicSimulation.DesktopDockVisible` |

### Hand tracking and passthrough

- **Hands.** The app reads your hand and finger positions through the headset's hand-tracking system so you can
  pinch and point. They are used for the current frame and discarded; nothing is recorded or sent.
- **Passthrough.** The view of your room is drawn by the headset's own system. The app only switches it on or
  off; it never receives, stores or sends camera images.
- **Room placement.** Where the content sits in your room is decided fresh each session and is not saved.

### What the app does not collect

No name, email, account or Meta user ID; no location; no contacts, photos or files; no eye, face or body
tracking; no advertising identifiers; no analytics or crash reports sent to us or to third parties; no
purchases. There is no multiplayer, chat or user-generated content.

## How the data is used

- Your voice and the one-line description of what you are looking at are used for one purpose: to answer the
  question you asked the guide. We do not use them for advertising, profiling or analytics, and we do not sell
  or share them with anyone other than OpenAI as described above.
- The stored preferences are used only to restore your settings.

## Internet use

The app needs an internet connection only for the voice guide. Without one the guide plays a recorded greeting
and cannot answer; everything else works offline. Links on the About screen open in the headset's browser,
outside the app; the sites they lead to have their own privacy policies.

## Deleting your data

- **Preferences on your headset:** uninstall the app, or use *Settings → Apps → Cosmic Simulation XR → Clear
  data* on the headset. This removes everything the app has stored.
- **Voice data:** we hold none, so there is nothing for us to delete. To ask about or request deletion of
  anything OpenAI may have retained from your use of the guide, email us at **[OWNER: contact email address]**
  with the date and approximate time you used it; we will reply within 30 days and pass the request to OpenAI.
- **Microphone access:** you can withdraw it at any time in the headset's *Settings → Privacy and safety → App
  permissions → Microphone*.

## Children

The app is intended for ages 13 and up and is not directed at children under 13. We do not knowingly collect
personal information from children. If you believe a child has used the voice guide and want to make a request
about it, contact us at the address above.

## Changes

If the app's handling of data changes, this page is updated before the new version is released and the date at
the top changes.

---

## Maintainer notes (delete before publishing)

Maps to Meta's requirements (https://developers.meta.com/horizon/resources/publish-quest-req/): Privacy.1 the
hosted URL; Privacy.2 "What data the app handles"; Privacy.3 "How the data is used"; Privacy.4 "Deleting your
data"; Privacy.5 is the Data Use Checkup in the dashboard, answered consistently with this page.

Checked against the code on 6 Oct 2026 (`quest3-port` at `9384a75b`):

- Network: the only outbound connection in `Assets/scripts`, `Assets/Being`, `Assets/Cosmic` is
  `wss://api.openai.com/v1/realtime` (`Assets/Being/Session.cs:15`, `Realtime.cs`). No `UnityWebRequest`,
  no `HttpClient`.
- Microphone: `Assets/Being/Mic.cs` (`Arm` opens the device while the being is out, `Start`/`Stop` bound the
  frames that are sent, `Close` ends it); permission requested in `Mic.Request`. The level meters
  (`Assets/Cosmic/UI/MicPicker.cs`, `Assets/scripts/experience/MicrophoneRow.cs`) read locally only.
- Transcription is done by OpenAI inside the same session (`BeingSettings.transcriptionModel`); the app shows
  transcripts but does not persist them.
- `PlayerPrefs` keys: grep of `PlayerPrefs.` in the three folders. The earlier draft listed three keys; there
  are now twelve across the two UI trees, all listed above.
- Manifest permissions expected in the store build: `INTERNET`, `RECORD_AUDIO`, `MODIFY_AUDIO_SETTINGS`,
  `com.oculus.permission.HAND_TRACKING`. `BLUETOOTH` is removed by `Quest3Manifest.cs`.
- No Meta Platform SDK, so no entitlement call to Meta (decision D-012).

Must change before the statement stays true:

1. **CS-231–233 (token endpoint).** When the app fetches a short-lived secret from our own endpoint, add a
   section: what the endpoint receives (an anonymous install id, app version, a daily count), that it receives
   no audio, and add the install id to the preferences table. "We run no servers" then becomes false.
2. If `alwaysListening` is ever turned on in `BeingSettings`, the "sent while the guide is listening" bullet
   must say the microphone stays live between answers.
3. Confirm OpenAI's current API retention wording when publishing; the links above are the source.

Owner to confirm before publishing (not verifiable from the repository): the two headset menu paths in
"Deleting your data" as they read on the current Horizon OS; and the 30-day reply commitment, which is a
promise the owner makes, not a fact about the app.
