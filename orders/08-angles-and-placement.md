# 07 -- ANGLES, PLACEMENT, AND THE THINGS ON THE TABLE: the second research pass

**Why this file exists.** The player asked (v130) for a second deep study, of a different subject from `06`:
*"angles/perspective, how objects react with each other, hover for mobile, mobile-game user needs,
interactions that keep it interesting, animation, natural placement of keyboard, mouse, clipboard,
monitor-as-light-source, a person sitting behind a table."*

`06` answers *how is a surface painted*. This one answers *where every object goes, and why*, and then the
interaction layer: what a finger can do that a mouse cannot, what keeps a 6-second loop interesting for 25
minutes, and how animation carries a tell.

Like `06`, every rule names **the file that enforces it**, and rules with no enforcer are labelled `OWED` so
they become work instead of decoration. Sources at the end, [1]..[9].

---

## 1. ONE EYE, ONE HORIZON, ONE GRID

- **The horizon line is the viewer's eye level, and it is decided once.** Every object's height in the frame,
  its top face's visible area, and where its contact shadow sits all follow from it. [1][2]
- **This game's rule: the eye is seated at the desk.** The horizon is the desk's far edge (canvas row 170);
  the camera looks slightly DOWN at the wood and straight AT the monitor's glass. That is why the wood shows
  more depth than the monitor's top face, and why the applicant's head (behind the desk, lower camera) is
  seen level-on rather than from above. It is a *height-scaled oblique* / 3-4 view, not isometric: props keep
  a 1:1 horizontal scale and lose depth by a few px per 10 px of height. [2][3]
- **Consistent scale is the immersion.** Two props at the same distance that are not drawn to the same scale
  read as two different rooms. [1][5]
- **Integer scale only.** A 480x270 canvas shown at 3x or 4x; never 3.5x -- half a pixel is a blurred edge on
  every sprite in the frame. [4]

**Enforced by:** `Core/Rules/MenuLayout.cs` (`DeskSurfaceY`, one horizon), `desk_layout.json` (`anchor`,
`sortingOrder` per layer), `tools/comp/twin_desk.py --verify` (every layer and hotspot resolves on 480x270),
and the "monitor right of x=240, applicant left of it" assertion in the same gate.

## 2. THE TABLE IS A SET OF ZONES, NOT A DUMPING GROUND

Ergonomics gives numbers that are also composition rules: a desk legible to a human is a desk legible to
a player. [6][7]

| Zone | What belongs there | The number | In this game |
|---|---|---|---|
| **Dead centre, in front of the torso** | keyboard, centred on the monitor | keyboard within arm's reach, flat | keyboard at x 262..346, monitor at 256..451 |
| **Right of the keyboard, same plane** | mouse | same height as the keyboard, no reach | mouse at x 356..375 |
| **Left of the keyboard** | papers, clipboard -- things you read, not type on | document holder at eye level, never twisted to | forms at x 96..157, letter/clipboard left-centre |
| **Far left, the non-dominant side** | the drink, away from the electronics | arm's length, outside the typing arc | coffee at x 34..77 |
| **Behind the desk, standing** | the monitor, at arm's length | top of bezel at or just below eye level | glass rows 61..176 with the applicant behind it |
| **On the wood, right** | the lamp, so it lights the work and not the eye | 45 degrees off the sight line | lamp at x 420..475 |

- **The monitor goes behind the key surface, not beside it.** Everything the player types on or reads sits
  between them and the glass: that is what makes the desk feel worked-in instead of arranged. [6]
- **The drink is on the opposite side from the electronics.** Real desks do this; it also keeps the coffee
  out of the monitor's pool so that its lit flank is a *deliberate* measurement, not overflow. [7]
- **Nothing floats and nothing overlaps the desk's edge.** Every prop's base sits on the surface line and
  every prop is inside the wood's span; a prop with its foot on the front lip is a prop falling off. [1]

**Enforced by:** `desk_slab_M` (the props are painted INTO the slab, so they share its perspective, its
contact shadows and its light), `ScreenLight.Props[]` and `LightRigTests.every_prop_is_inside_the_pool`
(the same four rects the art draws), `twin_desk --verify`.

## 3. OBJECTS REACT TO EACH OTHER: CONTACT, OCCLUSION, LIGHT, AND WHAT IS BEHIND WHAT

- **Overlap is the only depth cue a flat frame has that costs nothing.** A prop that overlaps another says
  which is nearer; a prop that only sits next to it says nothing. [1][2]
- **Contact beats contact.** Where two objects touch, the join gets ONE darker pixel -- never a gradient. A
  cup on a saucer, a monitor's foot on the wood, an elbow on the desk. [3]
- **Occlusion decides light.** If the lamp is behind the monitor from the camera's point of view, the wood in
  front of the monitor is NOT lit by the lamp. This is why the lamp's pool lives left of the lamp and not
  under it. [3]
- **A thing that is lit by a neighbour is lit on the side that faces it.** The cup's monitor-side flank, the
  keyboard's near keys, the mouse's near edge -- the player named all three, and the rig measures them.
- **Reflection is a cheat worth one pixel.** A CRT throws a sliver of light onto the glass-adjacent edge of
  the desk; one or two pixels of the screen's own colour sold the whole "the monitor is ON" read in v129.

**Enforced by:** `LightRig.ShadowSideAt` (which flank takes the contact shadow), `LightRig.ForDesk`'s five
sources with their occlusion-sane centres, `make_desk_room.desk_slab_sprite`'s draw order (wood -> props ->
contact shadow -> pool), and the render pair in `docs/images/v130/lights_off_on.png`.

## 4. A PERSON SITS BEHIND A TABLE, NOT ON TOP OF IT

- **The desk cuts the body at the waist.** A seated figure is: head, shoulders, chest, then the table. Drawing
  the whole torso puts the person IN FRONT of the desk, which is the wrong side of the world. [8]
- **Further back and beside, not centred.** The player settled this twice: the applicant sits *beside* the
  table (left), *behind* its far edge, at a smaller apparent scale than the props. That single offset is what
  makes the desk the near object and the person the far one. [8]
- **Eye line below the glass's centre.** A seated visitor's eyes land around the monitor's lower third; if
  their eyes are level with the top bezel they read as standing. [6]
- **Hands, chin and the letter are the acting surfaces.** At 192x216 the face is 3/4 of the read, so the
  four-frame idle spends its motion on breath (1 px), a blink, and one fidget -- not on a walk cycle.

**Enforced by:** `desk_layout.json` (`applicant` layer at the left, behind the slab's sorting order),
`make_applicants.py` (`build_frame` breath/blink/fidget; the `no_blink` artifact), and
`LightRigTests.variety_*`.

## 5. MOBILE IS THE FIRST SCREEN, AND HOVER DOES NOT EXIST ON IT

| Rule | Number | Consequence here |
|---|---|---|
| Minimum tap target | 44 pt / 48 dp | at 480x270 and 3-4x device scale: **36-48 canvas px**, hit-tested padded beyond the drawn plate |
| Spacing between targets | 8 dp | two stamps must never share a 8dp boundary |
| Thumb zone | bottom third | the stamps and reasons live low; nothing decision-critical in the top corners |
| No hover state | -- | hover is pointer-only feedback; touch gets **press** (squash) + **settle**, and every hover-revealed fact must also be on-screen |
| Latency budget | 50-100 ms | the press acknowledgement is immediate; the panel transition is 120-200 ms |

- **The mobile problem this game actually has:** four evidence sources (face, letter, record, badge) and a
  thumb that covers 9 mm of a 480 px-wide canvas. So evidence is *borrowed* -- you look at one thing at a
  time and it stays looked-at -- and the stamps stay in the near field.
- **Hover's job on desktop is discovery; on touch it must become "tap to inspect".** Hence two taps to confirm
  a stamp: the first tap is the hover, the second is the commit.

**Enforced by:** `Core/Rules/MenuRules.ConfirmWindow(touch)` (two taps on touch, one on pointer),
`HotspotHitTest` (padded targets), `HudRules` (plates + states), gates `hit_test_safe_area_and_overlap` and
`two_taps_on_touch_only`.

## 6. WHAT KEEPS A REPEATED LOOP INTERESTING

- **Vary the case, not the interface.** 15 applicant variants across 6 archetypes, 5 letter flag types, 12
  day events. The screen stays identical; the inputs do not. (Refactor is a lie.)
- **Give the player a hypothesis to test.** The tells are visible but deniable: a seam, a straight badge, a
  shadow at the wrong angle. The player is not guessing a number, they are forming a theory -- and the game
  contradicts it just often enough.
- **Ration the interrupts.** A sound, a flicker, an alert: none of them may fire twice in the same 20 seconds
  or they become wallpaper.
- **Make the clock do the pacing.** A visible, always-advancing number turns waiting into a decision.
- **Reward a second look.** The ID photo in the CRT glass, the applicant's name on the badge, the letter's
  signature: details that confirm a conclusion AFTER the player has made it.

**Enforced by:** `Appearances` (variety, seeded and stored), `RunGenerator` (event mix), `SecurityRating`
(the interrupt budget), `ShiftClock` + `RoomClock` (visible pacing), `docs/17` section 8 (the glass shows the
applicant's own photo).

## 7. ANIMATION: FEEDBACK FIRST, CHARACTER SECOND

| What | Budget | Easing |
|---|---|---|
| Press acknowledgement | 0 ms (this frame) | squash to 0.95 |
| Hover lift (pointer only) | 100-150 ms | ease-out |
| Panel / page change | 150-250 ms | ease-out, never linear |
| Screen fade on a state change | 550 ms | ease-out (`ScreenLight.FadeSeconds`) |
| Idle loops | 6-12 fps, 4 frames | stepped, never tweened |

- **Never animate a disabled control.** If it cannot be pressed, it must not respond to being pressed; silence
  is the feedback.
- **Stepped, not tweened.** At 480x270 a 24 fps tween looks like a different game; 4-8 fps stepped motion
  looks like this one. `motion_manifest.py` caps every loop at 12 fps.
- **The tell is the animation.** `no_blink` is a mechanic: a synthetic that never blinks is caught by watching
  the idle, not by reading a stat.

**Enforced by:** `motion_manifest.py` (layer + fps budget), `Game/Desk/LampView.cs` (`MoveTowards` ramp,
one-step-down off state), `RoomClockView` (repaint once per in-game minute, never per frame).

---

## The checklist this file adds to every round

1. Is every new prop inside a **zone** in section 2, and does its foot sit on the surface line?
2. Did I add a **contact** pixel where it meets what it touches?
3. Is it **occluding** or **occluded** by the right thing, and is it sorted accordingly?
4. Does the **hover** it needs exist as "tap to inspect" too?
5. Is its animation **stepped** and inside the fps budget, and does a disabled state stay silent?
6. Is the case **varied** rather than the interface?

## Sources

[1] Perspective in 2D games -- horizon line, vanishing points, scale consistency, depth by overlap and detail.
    https://7swordsgames.com/beginners-guide-to-perspective-drawing-in-2d-game-development/
[2] 2D art fundamentals -- one-point perspective, eye level, and why "isometric" pixel art is dimetric.
    https://theultimategamingblog123.wordpress.com/2d-art/
[3] 3/4 view, height-scaled oblique and orthographic 2.5D -- the family this game belongs to.
    https://www.reddit.com/r/PixelArt/comments/14navcf/what_is_the_perspective_in_the_2dzelda_games/
[4] Pixel perfection -- integer scaling only, point filtering, consistent texel size.
    https://leahayes.co.uk/2015/03/02/pixel-perfection-in-2d-game-projects.html
[5] Isometric pixel art practice -- palette discipline, consistent scale across a large project.
    https://yandex-games.org/isometric-pixel-art-tutorials-guide/
[6] Ergonomic desk setup -- monitor height and distance, keyboard/mouse at one plane, elbow angles.
    https://chutex-innovation.com/en-us/guides/ergonomic-desk-setup/
[7] Ergonomic workstation -- monitor at arm's length, keyboard close, mouse beside it, drink away from gear.
    https://theofw.com/ergonomic-office-furniture-setup-best-practices/
[8] Ergonomic sequence -- chair, then desk, then monitor, then keyboard, then mouse; nothing out of reach.
    https://blacklyte.com/blogs/news/the-ergonomic-desk-setup-guide-monitor-chair-keyboard-mouse
[9] Touch targets and micro-interaction timing carry over from `06`'s [5][6][7][8]; they are not restated here.
