# Store listing draft

Draft copy for the Meta Horizon Store listing (free, ages 13+, Quest 3 and 3S - decision D-012). Our own words only - nothing here
is transcribed from the reference app's promo material. Written in the voice
set out in `docs/copy/README.md`: plain and concrete, one striking fact at a
time, no exclamation marks, no "amazing". This listing describes the Quest
3/3S build; the desktop mouse-and-keyboard mode is a testing/demo build and is
not what ships on the Meta store, so it is not sold on the strength of it here.

Lengths below are a starting point, not measured against Meta's current
character limits - check the submission form when this is pasted in and trim
if it does not fit.

---

## App name

Cosmic Simulation XR

## Short description (listing card, one line)

Pull the planets into your room and hold them in your hands.

## Long description

Touch the solar system. Reach into the sky in your own room, pull a planet
toward you, and it grows until you are holding it - or until it fills the
space around you. Pinch to take hold of anything; use two hands to make it
bigger or smaller. Nothing is behind a menu you have to learn first.

Start at the Sun and work outward. Every planet and moon opens with a short
description and its real numbers - size, distance, day length, temperature -
next to it, not buried in a menu. Stand the Earth beside its actual Moon.
Compare a marble-sized Mercury with a Jupiter that fills your living room.

Go further than the solar system. Step into the light of the Crab Nebula and
the Pillars of Creation. Walk around a black hole. See where our galaxy sits
among countless others in the observable universe. Everything is at real
scale, or scaled deliberately so you can feel the difference, and the app
always tells you which.

The app runs in passthrough by default, so your room stays visible around the
stars; switch to a dark sky if you want the room to disappear. Sessions run
five to twenty minutes and there is no score, no fail state and no required
order - explore at your own pace, and a single button puts everything back
where it started.

Have a question? Summon the guide, a small sphere of light that stays beside you, and ask it out loud. It knows
what you are looking at and answers in a spoken voice. The guide is the one part of the app that needs an
internet connection and the microphone: your question is sent to OpenAI to produce the answer. Everything else
works offline, and the guide stays silent until you call it.

## Feature bullets

- Pinch to grab any planet or moon out of the sky; two hands to resize it
- Real scale, shown honestly: hold a 15 cm Moon, or grow a planet to fill
  your room
- Every body opens with a plain-language paragraph and four key numbers
- Seven places to open from the Milky Way map - six nebulae and a star
  cluster - and a black hole you can walk around
- A voice guide you can ask questions out loud (needs internet and the
  microphone; the rest of the app works offline)
- Passthrough by default - your room stays part of the experience; a
  full-dark mode is one tap away
- No score, no fail state, no required order - a five-minute look or a
  twenty-minute deep dive both work
- One button restores everything to where it started

## Age and comfort framing

**Recommended age.** Declared age group on the store: **13+ (teens and
adults)**, decision D-012 - the voice guide sends speech to a third party, so
the app is not offered to under-13s. The copy keeps a reading age of about 12
(`docs/GDD.md` section 1) as a writing target only. The content rating itself
comes from the IARC questionnaire during submission; do not publish an age
badge based on this line alone.

**Internet.** Required for the voice guide only; set the store's internet
field to the option that means "not required" / "optional".

**Comfort.** All movement in the app is physical: you walk around objects and
reach for them with your own hands, and there is no joystick or teleport
locomotion to move your viewpoint through the scene (`docs/GDD.md` sections 1
and 13 rule out any locomotion scheme; confirmed against
`Assets/scripts/XR/XRInputRig.cs` and `Assets/scripts/experience/`, neither of
which drives camera position from a control stick). That supports a
"Comfortable" self-rating on Meta's comfort scale, but the rating itself is a
field in the submission form the owner sets after actually trying the current
build on-device - CS-080/CS-081 - not something this document can certify.

**Play space.** Room-scale or standing; a few square metres of clear floor is
enough to walk around the black hole and nebula destinations. Works seated
too for the solar-system and planet interactions.

## Open questions for the owner

1. Screenshots and a capture/trailer - needs the live app on-device (CS-080,
   CS-081; tracked in the roadmap as P7-T5).
2. Store category and keywords - Meta's submission form, not drafted here.
3. Final short/long description length against Meta's current limits at
   submission time.
4. The official age rating and comfort rating, both set via Meta's own
   questionnaire, not asserted here.
5. Feature bullets rechecked 6 Oct 2026 against `Assets/data/destinations/`
   and `Assets/Cosmic/Data/Generated/places/`: seven map destinations (Helix,
   Crab, Homunculus, Orion, Pillars of Creation, NGC 1501 - six nebulae - and
   the Trumpler 14 cluster). The newer `Cosmic` tree also holds Andromeda,
   Triangulum, Whirlpool, Pinwheel and the HD 110067 system; they are not
   claimed here until it is confirmed on device that the store build shows them.
