# Copy deck

All player-facing text lives here. An editor script (`CopyImporter`, ticket
CS-021) reads these files into `ExperienceModule` and `BodyInfo` assets, so text
is edited here and never typed into Unity.

## Rules

1. **ASCII only.** The Selawik SDF fonts in the project carry ASCII and nothing
   else. No degree signs, en dashes, em dashes, curly quotes or superscripts.
   Write "430 degrees Celsius", "minus 180", "1854-1856", "Earth's".
   The one exception is the mass stat, where the importer builds the superscript
   from the `exponent` column using rich text.
2. **Our own words.** Nothing is transcribed from the reference app. Facts come
   from NASA planetary fact sheets and mission pages.
3. **Lengths.** Body paragraph: 55 words maximum. Scene paragraph: 45 words
   maximum, two of them, plus an instruction line of 20 words maximum. Moon
   sentence: 25 words maximum.
4. **Tone.** Plain and concrete, one striking fact per object. No exclamation
   marks, no second-person hype, no "amazing".
5. **Numbers.** Thousands separated with commas. Units spelled in the value
   column ("km", "days", "hours"), not in the number.
6. Changing a number here changes it in the app; check it against the fact sheet
   before you do.

## Files

| File | Feeds |
|---|---|
| `experiences.md` | The seven scene panels (`ExperienceModule.panel`) |
| `bodies.md` | The Sun, eight planets and Pluto (`BodyInfo`) |
| `moons.md` | Moons (`BodyInfo`, moon variant panel) |
| `nebulae.md` | Milky Way destination overlays |
| `hints.md` | The two first-run hint cards |
| `ui.md` | Button labels, tooltips, About text |
