// HR//0D -- desk layout data, read from the SAME json the art pipeline and the twin renderer use.
// Assembly: HRZeroDay.Game  (may reference UnityEngine)
// Owner: Dev A. Do not hand-edit: unity/Assets/Content/desk_layout.json is the source.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace HRZeroDay.Game
{
    [Serializable]
    public class DeskLayer
    {
        public string id;
        public string sprite;
        public int[] anchor;            // px from bottom-left of the canvas, y up
        public int sortingOrder;
        public string frameSprite;
        public AnimatorSpec animator;
        public float scaleFactor = 1f;  // 1 = authored size; 0.96 = the applicant (composition L)
        public ScreenSpec screen;        // only on the monitor layer: the CRT glass rect
    }

    /// <summary>The drawable glass area of the monitor, in layer-local art px, y UP.
    /// Source of truth is desk_layout.json; MonitorScreen.GlassForSprite recomputes the same numbers
    /// from the sprite size so a redraw cannot silently desync the text from the bezel.</summary>
    [Serializable]
    public class ScreenSpec
    {
        public int x, y, w, h;
        public int glyphW = 5, glyphH = 7, letterGap = 2, lineGap = 3;   // legibility pass 2026-11-26
    }

    [Serializable]
    public class AnimatorSpec
    {
        public string[] frames;
        public float fps = 4f;
        public bool loop = true;
    }

    [Serializable]
    public class DeskHotspot
    {
        public string artifact;         // matches TellCatalog ids where possible
        public int[] rect;              // x, y, w, h in applicant-local px, y up
        public string label;

        public Rect ToRect()
        {
            // Unity UI/graphics space is y-DOWN from the pivot, the json is y-UP from the sprite's
            // bottom edge. Callers convert with SpriteLocalToRect; this helper keeps the numbers honest.
            return new Rect(rect[0], rect[1], rect[2], rect[3]);
        }
    }

    [Serializable]
    public class DeskLayout
    {
        public int layoutVersion;
        public CanvasSpec canvas;
        public List<DeskLayer> layers = new List<DeskLayer>();
        public List<DeskHotspot> hotspots = new List<DeskHotspot>();
        public int deskEdgeY;

        public DeskLayer Layer(string id)
        {
            for (int i = 0; i < layers.Count; i++) if (layers[i].id == id) return layers[i];
            throw new InvalidOperationException("desk_layout.json has no layer '" + id + "'");
        }

        /// <summary>Loads from Resources/desk_layout.json (published by tools/art/publish_to_unity.py).</summary>
        public static DeskLayout Load()
        {
            var asset = Resources.Load<TextAsset>("desk_layout");
            if (asset == null)
                throw new FileNotFoundException(
                    "Resources/desk_layout.json is missing. Run: python tools/art/publish_to_unity.py");
            return JsonUtility.FromJson<DeskLayout>(asset.text);
        }
    }

    [Serializable]
    public class CanvasSpec
    {
        public int width;
        public int height;
        public int pixelsPerUnit;
        public float orthographicSize;
    }
}
