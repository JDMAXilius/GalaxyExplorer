# Foundations

## Accepted direction

Dark narrow exploration rail, contextual top picker, small Explore / Guide / Room dock and separate Settings. Objects stay visually dominant. Information card order: photo, title, description, two facts, Listen / Compare. No permanent hand icon. No visible Resize or Return controls. Hand tracking is primary; equivalent controller/desktop operations must remain possible.

## Color and type

Proposed colors: surface `#181D24`, deeper surface `#0E1418`, white ink `#FFFFFF`, secondary `#AAB6C2`, cyan accent `#6CCFDD`, dark text on cyan `#0E1418`, divider `#41505D`, amber attention `#F2C66D`, error ink `#FFB4AB`. Status always has text/icon in addition to color. Use near-opaque surfaces behind reading content over passthrough; do not rely on blur for contrast.

Use Selawik to align with the existing project; Inter is a Figma fallback only. Proposed flat-frame type sizes: title 28/34 semibold, heading 22/28 semibold, body 16/24 regular, value 20/26 semibold, label 14/20 regular. Large and Larger preferences are 1.25× and 1.5×; reflow instead of clipping. Screen pixels are review measurements, not Unity world units.

Spacing steps 4, 8, 12, 16, 24, 32. Card padding 24 px; photo ratio 16:9; content gap 16 px; fact-column gap 24 px. Radius: card 20 px, photograph 12 px, picker tile 12 px, controls 12 px, dock 28 px. Use subtle borders and restrained selected glow; avoid large shadows over space.

## Spatial sizing proposals — require device validation

Use millimetres for world-space designs and metres in Unity; 1 mm = 0.001 Unity unit. Begin with an 80 mm-wide primary rail, a 170–200 mm information card, 24 mm minimum control target and 12 mm inter-target gap. Direct-touch panels should be placed for comfortable seated reach; farther panels use ray selection. Panel distance, visual angle and physical size must be validated together. Do not equate a 1440 px screenshot width with metres.

The existing dock parks around 0.8 m ahead and at 70% of eye height. These are starting observations, not approval for the new dimensions. Panels remain parked rather than following the head. Palm summon relocates the explorer only; it does not restore objects or resize content. Keep content/card association visible without long crossing leader lines.

## Motion and input

Proposed hover transition 120 ms; panel appearance 200 ms; contextual change 200 ms; room transition 400 ms. Reduced-motion mode should use immediate or short fades, not scale bursts. Show progress during loading, not decorative continuous animation. Button press feedback may depress by a small physical distance; choose magnitude on device.

Hover/focus describes the target. Selection is persistent state. Pressing is temporary feedback. Gesture hints appear on first-use teaching, explicit Help replay or failed menu recovery and clear on success. Gesture detection threshold and fallback delay remain open. Do not teach a gesture that conflicts with system gestures or grabbing.

## Imagery and iconography

Use verified repository/NASA/ESA imagery and preserve attribution. Generated galaxy images in mockups are illustrative; do not treat them as observed data. Modelled nebula interiors carry an explanation in details. Use one outline icon family consistently; selected states retain labels. The companion orb is a product asset, not an interchangeable generic person icon. No image-generated icon should ship without an editable reviewed asset and consistent stroke geometry.

## Layouts

Explore layout: left rail, top local chooser, central content, right photo-first card, bottom dock, separate settings corner. Focused utility layout: preserve world/content, dismiss explorer rail/chooser, show one Guide or Settings panel. Room is a background toggle, not a fourth navigation destination. Information details replace their parent card and preserve a clear Back path; no uncontrolled stack of windows.

Desktop: anchor rail and dock to screen edges with safe spacing; retain the same context and selection. Below available width, collapse rail to an explicit Explore launcher and make details a dismissible overlay. Keep all four exploration contexts accessible; do not remove them for smaller screens. Desktop is a secondary adaptation, not a replacement for headset validation.
