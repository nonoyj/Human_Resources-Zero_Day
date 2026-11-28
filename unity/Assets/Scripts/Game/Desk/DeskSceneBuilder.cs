// HR//0D -- assembles the desk scene from desk_layout.json.
// Assembly: HRZeroDay.Game
// Owner: Dev A. Called by Editor/HRZeroDayMenu.cs ("HR//0D > Build Desk Scene") in the Editor, and
// usable at runtime for tests. Everything it places comes from the same json the twin renderer
// (tools/comp/twin_desk.py) draws, so the two can never disagree about placement.
//
// Layer contract (desk_layout.json, composition L approved 2026-11-26):
//   sortingOrder -30 background   the room (window, board, clock, cabinet, plant, desk front)
//                -20 monitor      desk/monitor_small.png  -- 1 px redraw at the approved 0.58 scale
//                -15 document     desk/document_small.png -- 1 px redraw at 0.56
//                  0 applicant    the 4-frame strip, scaleFactor 0.96
//                -19 monitor_stand  desk/monitor_stand.png -- the arm; base tucks behind the desk
//                -19 MonitorScreen  runtime CRT text texture, child of the monitor layer
//                 10 desk         desk/desk_foreground.png -- sorts ABOVE the applicant on purpose,
//                                 so keyboard/mug/clipboard read as sitting in front of him.
// anchor is the BOTTOM-LEFT corner in canvas pixels, y up.
using System.Collections.Generic;
using HRZeroDay.Core.Data;
using UnityEngine;

namespace HRZeroDay.Game
{
    public static class DeskSceneBuilder
    {
        public const string RootName = "DeskRoot";

        /// <summary>Builds (or rebuilds) the desk under `parent`. Returns the root transform.
        /// Pass a ShiftController to have the CRT screen wired to the live run (MonitorScreenView).</summary>
        public static Transform Build(DeskLayout layout, Transform parent, ShiftController shift = null)
        {
            if (layout == null) throw new System.ArgumentNullException("layout");

            // parent is nullable: the editor menu builds a scene with no parent, and calling Find() on a
            // null Transform is the NullReferenceException that stopped "Build Desk Scene" on 2026-11-26.
            if (parent != null)
            {
                var existing = parent.Find(RootName);
                if (existing != null) Object.DestroyImmediate(existing.gameObject);
            }
            else
            {
                // NO PARENT MEANS "IN THE OPEN SCENE", so the open scene's desk is this desk's predecessor.
                // v129 removed a stale root only when a parent was given; the player's v130 screenshot had
                // THREE applicants side by side because GameBoot built a desk next to the one HR//0D >
                // Build Desk Scene had already saved. A builder that can run twice must clean up first.
                for (int i = 0; i < 8; i++)
                {
                    var stale = GameObject.Find(RootName);
                    if (stale == null) break;
                    Object.DestroyImmediate(stale);
                }
            }

            var root = new GameObject(RootName).transform;
            root.SetParent(parent, false);

            var manifest = SpriteManifest.Load();
            var created = new Dictionary<string, GameObject>();

            foreach (var layer in layout.layers)
            {
                SpriteEntry entry;
                try { entry = manifest.Find(layer.sprite); }
                catch (System.ArgumentException ex)
                {
                    Object.DestroyImmediate(root.gameObject);
                    throw new System.InvalidOperationException(
                        "layer '" + layer.id + "' cannot be placed: " + ex.Message, ex);
                }
                var go = new GameObject("L_" + layer.id);
                go.transform.SetParent(root, false);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = LoadPrimarySprite(entry);
                sr.sortingOrder = layer.sortingOrder;

                // scaleFactor (json, optional): the applicant is drawn at 0.96 of its authored size.
                // Multiple-mode strips store the FULL strip width in entry.width (768 px for four
                // 192 px frames). Using that to place the pivot pushed the applicant 41 units to the
                // right -- off a 30-unit-wide camera. Always position by the sprite actually drawn.
                float scale = layer.scaleFactor > 0f ? layer.scaleFactor : 1f;
                int artW = entry.width;
                int artH = entry.height;
                if (entry.spriteMode == "Multiple" && entry.sprites != null &&
                    entry.sprites.Count > 0 && entry.sprites[0].IsValid)
                {
                    artW = entry.sprites[0].Width;
                    artH = entry.sprites[0].Height;
                }
                int drawW = Mathf.RoundToInt(artW * scale);
                int drawH = Mathf.RoundToInt(artH * scale);
                go.transform.localScale = new Vector3(scale, scale, 1f);

                go.transform.localPosition = PixelsToUnits(layer.anchor[0], layer.anchor[1],
                                                           drawW, drawH,
                                                           layout.canvas.pixelsPerUnit);
                created[layer.id] = go;
            }

            // the monitor gets the live CRT text layer (added 2026-11-26)
            if (created.ContainsKey("monitor"))
            {
                var monitorGo = created["monitor"];
                var screen = new GameObject(MonitorScreenView.RootName);
                screen.transform.SetParent(monitorGo.transform, false);
                screen.AddComponent<SpriteRenderer>();
                var view = screen.AddComponent<MonitorScreenView>();
                // the glass texture is created by the view; the background is the CRT's dead-tube tone
                view.Initialise(shift, monitorGo, new Color32(0x0b, 0x0e, 0x13, 0xff));
            }

            // ---- time of day: day -> dusk -> night, driven by the shift clock.
            // The three room paintings are already in the kit as L0/L1/L2, so the light can change without
            // a single new asset. RoomLighting tints everything EXCEPT the CRT and the letter.
            var lighting = RoomLighting.Attach(root, LoadRoomStages(manifest));
            if (lighting != null && shift != null)
            {
                var driver = root.gameObject.GetComponent<DeskLightDriver>();
                if (driver == null) driver = root.gameObject.AddComponent<DeskLightDriver>();
                driver.lighting = lighting;
                driver.shift = shift;
            }

            // ---- THE TUBE AS A LIGHT (v120, request E). Attached after EVERY layer exists, and given the
            // LAYOUT rather than one renderer: it captures the room, the monitor, the stand, the papers and
            // the desk, and lights all of them in place. The first attempt handed it `L_background` only --
            // which is the picture MenuView replaces with its own composition while the front door is up, so
            // the tube's light was painted into a menu backdrop and the desk in front of the monitor stayed
            // dark. It also needs the layout for the canvas rects: a light pass whose coordinates are 40 px
            // out lights the ceiling.
            var tube = ScreenLight.Attach(root);
            if (tube != null) tube.Bind(root, layout);

            // ---- THE SECOND PRACTICAL (v130, hardened in v130.1 after BUG-031)
            // The player's screenshot: "[HR//0D] could not start the game: There is no 'SpriteRenderer'
            // attached to the "Lamp" game object". The controller used to add a SpriteRenderer to ITSELF and
            // then copy the lamp layer's sprite onto it -- so the fixture was drawn twice, at two sorting
            // orders, and every run depended on that second renderer existing. It does not need to:
            // L_lamp IS the fixture. The controller is handed that layer and switches its colour.
            var lamp = LampView.Attach(root);
            GameObject lampLayer = null;
            created.TryGetValue("lamp", out lampLayer);
            if (lamp != null)
            {
                if (lampLayer != null)
                {
                    // the layer keeps its own sorting order -- it is already above the wood and under the lip
                    lamp.Bind(lampLayer.GetComponent<SpriteRenderer>());
                }
                else
                {
                    // no lamp layer in the layout: the light still exists, and saying so beats a crash
                    Debug.LogWarning("[HR//0D] the layout has no 'lamp' layer, so the room's lamp has no " +
                                     "fixture to switch. The light is still in LightRig.ForDesk.");
                }
            }
            var clock = RoomClockView.Attach(root);
            var lightDriver = root.gameObject.GetComponent<DeskLightDriver>();
            if (lightDriver != null)
            {
                lightDriver.clock = clock;
                lightDriver.lamp = lamp;
                // v130.5 (BUG-040): the driver is what carries the lamp's switch into the room's light, and
                // `screen` is the one object that owns the surfaces the lamp lands on. It was never assigned,
                // so the lamp could be switched and nothing in the room could hear it.
                lightDriver.surfaces = tube;
            }

            // the applicant gets the idle driver and the whole 4-frame strip
            if (created.ContainsKey("applicant"))
            {
                var app = created["applicant"];
                var view = app.AddComponent<ApplicantView>();
                view.frames = LoadFrames(manifest.Find(layout.Layer("applicant").sprite));
                view.animate = true;
            }

            return root;
        }

        /// <summary>
        /// The three room paintings, in order (day, dusk, night). Missing stages are allowed -- the room
        /// still tints, it just will not swap paintings, which is better than throwing mid-build.
        /// </summary>
        public static Sprite[] LoadRoomStages(SpriteManifest manifest)
        {
            var stages = new Sprite[3];
            var names = new[] { "L0", "L1", "L2" };
            for (int i = 0; i < 3; i++)
            {
                var entry = FindQuiet(manifest, "art/kit/backgrounds/office_" + names[i] + ".png")
                         ?? FindQuiet(manifest, "Assets/Art/Kit/backgrounds/office_" + names[i] + ".png");
                if (entry == null) continue;
                try { stages[i] = LoadPrimarySprite(entry); }
                catch (System.Exception) { stages[i] = null; }
            }
            return stages;
        }

        private static SpriteEntry FindQuiet(SpriteManifest manifest, string path)
        {
            if (manifest == null) return null;
            try { return manifest.Find(path); }
            catch (System.ArgumentException) { return null; }
        }

        public static Vector3 PixelsToUnits(int px, int py, int spriteW, int spriteH, int ppu)
        {
            // json: px from the canvas bottom-left, y up. Unity: sprite pivot is centre by default,
            // so place the centre at anchor + half the sprite size.
            float x = (px + spriteW * 0.5f) / ppu;
            float y = (py + spriteH * 0.5f) / ppu;
            return new Vector3(x, y, 0f);
        }

        public static Sprite LoadPrimarySprite(SpriteEntry entry)
        {
            var all = Resources.LoadAll<Sprite>(UnityPathToResources(entry));
            if (all == null || all.Length == 0)
                throw new System.InvalidOperationException(
                    "sprite not found at Resources/" + UnityPathToResources(entry) +
                    ". Run: python tools/art/publish_to_unity.py");
            // Multiple-mode sheets come back sorted by name; neutral sorts first for applicants.
            System.Array.Sort(all, (a, b) => string.CompareOrdinal(a.name, b.name));
            return all[0];
        }

        /// <summary>
        /// The 4 animation frames of an applicant strip: neutral, breathe, breathe_fidget, blink.
        /// These are child files generated by HR//0D &gt; Import Art Kit (frames/&lt;stem&gt;__&lt;name&gt;.png),
        /// not slices of the sheet -- Unity 6 removed the slicing API and the package that replaced it
        /// is not worth a dependency for four frames per applicant.
        /// </summary>
        public static Sprite[] LoadFrames(SpriteEntry entry)
        {
            if (entry.sprites == null || entry.sprites.Count < 4)
                throw new System.InvalidOperationException(
                    entry.assetPath + " has no frame list in the manifest -- regenerate with " +
                    "tools/art/make_sprite_manifest.py");

            var dir = UnityPathToResources(entry);
            dir = dir.Substring(0, dir.LastIndexOf('/')) + "/frames/" + entry.FileNameNoExt + "__";

            var frames = new Sprite[4];
            for (int i = 0; i < 4; i++)
            {
                var path = dir + entry.sprites[i].name;
                var sprite = Resources.Load<Sprite>(path);
                if (sprite == null)
                    throw new System.InvalidOperationException(
                        "frame not found at Resources/" + path +
                        ". Run HR//0D > Publish Art To Resources, then Import Art Kit.");
                frames[i] = sprite;
            }
            return frames;
        }

        /// <summary>Assets/Art/Kit/applicants/app_x_192x216.png -> Art/Kit/applicants/app_x_192x216</summary>
        public static string UnityPathToResources(SpriteEntry entry)
        {
            var p = entry.assetPath;
            if (p.StartsWith("Assets/")) p = p.Substring("Assets/".Length);
            int dot = p.LastIndexOf('.');
            if (dot >= 0) p = p.Substring(0, dot);
            return p;
        }

        /// <summary>Camera that makes 1 art pixel = 1 screen pixel at 480x270 (Pixel Perfect Camera).</summary>
        public static void ConfigureCamera(Camera cam, CanvasSpec spec)
        {
            cam.orthographic = true;
            // ---- THE ROOM MUST BE WHOLE, NOT WIDE (v130.5, BUG-044).
            //
            // This was `spec.height / (2 * ppu)` with the camera centred on the canvas: a fixed VERTICAL
            // world height. On anything wider than 16:9 -- a windowed editor at 1920x1080 is not, but the
            // envelope the game is played in, 16:9 is -- the camera then shows MORE width than 480 px, and the
            // 480 px composition is letterboxed with the empty background down both sides. Worse in the other
            // direction: a narrower viewport crops the room's right edge, which is where the monitor, the lamp
            // and the clock stand.
            //
            // The fix is one line of arithmetic: fit the WHOLE canvas and let the slack fall outside it, so
            // the composition is always complete and always centred. `Mathf.Max` of the two fits is the
            // "contain" rule every 2D camera should start from.
            float viewAspect = cam.aspect > 0.01f ? cam.aspect : (spec.width / (float)spec.height);
            float fitByHeight = spec.height / (2f * spec.pixelsPerUnit);
            float fitByWidth = spec.width / (2f * spec.pixelsPerUnit * viewAspect);
            cam.orthographicSize = Mathf.Max(fitByHeight, fitByWidth);
            cam.transform.position = new Vector3(spec.width / (2f * spec.pixelsPerUnit),
                                                 spec.height / (2f * spec.pixelsPerUnit), -10f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(0x0b, 0x0e, 0x13, 0xff);
        }
    }
}
