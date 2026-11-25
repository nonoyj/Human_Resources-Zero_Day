// HR//0D -- assembles the desk scene from desk_layout.json.
// Assembly: HRZeroDay.Game
// Owner: Dev A. Called by Editor/HRZeroDayMenu.cs ("HR//0D > Build Desk Scene") in the Editor, and
// usable at runtime for tests. Everything it places comes from the same json the twin renderer
// (tools/comp/twin_desk.py) draws, so the two can never disagree about placement.
using System.Collections.Generic;
using UnityEngine;

namespace HRZeroDay.Game
{
    public static class DeskSceneBuilder
    {
        public const string RootName = "DeskRoot";

        /// <summary>Builds (or rebuilds) the desk under `parent`. Returns the root transform.</summary>
        public static Transform Build(DeskLayout layout, Transform parent)
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
                go.transform.localPosition = PixelsToUnits(layer.anchor[0], layer.anchor[1],
                                                           entry.width, entry.height,
                                                           layout.canvas.pixelsPerUnit);
                created[layer.id] = go;
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

        public static Sprite[] LoadFrames(SpriteEntry entry)
        {
            var all = Resources.LoadAll<Sprite>(UnityPathToResources(entry));
            if (all == null || all.Length < 4)
                throw new System.InvalidOperationException(
                    "expected 4 sliced frames for " + entry.assetPath + ", got " +
                    (all == null ? 0 : all.Length));
            System.Array.Sort(all, (a, b) => string.CompareOrdinal(a.name, b.name));
            var frames = new Sprite[4];
            for (int i = 0; i < 4; i++) frames[i] = all[i];
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
