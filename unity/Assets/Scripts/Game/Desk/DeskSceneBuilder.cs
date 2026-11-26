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

            var existing = parent.Find(RootName);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var root = new GameObject(RootName).transform;
            root.SetParent(parent, false);

            var manifest = SpriteManifest.Load();
            var created = new Dictionary<string, GameObject>();

            foreach (var layer in layout.layers)
            {
                var entry = manifest.Find(layer.sprite);
                var go = new GameObject("L_" + layer.id);
                go.transform.SetParent(root, false);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = LoadPrimarySprite(entry);
                sr.sortingOrder = layer.sortingOrder;

                // scaleFactor (json, optional): the applicant is drawn at 0.96 of its authored size.
                // Nearest-neighbour only -- a Point-filtered sprite scaled bilinearly would blur.
                float scale = layer.scaleFactor > 0f ? layer.scaleFactor : 1f;
                int drawW = Mathf.RoundToInt(entry.width * scale);
                int drawH = Mathf.RoundToInt(entry.height * scale);
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
            cam.orthographicSize = spec.height / (2f * spec.pixelsPerUnit);
            cam.transform.position = new Vector3(spec.width / (2f * spec.pixelsPerUnit),
                                                 spec.height / (2f * spec.pixelsPerUnit), -10f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(0x0b, 0x0e, 0x13, 0xff);
        }
    }
}
