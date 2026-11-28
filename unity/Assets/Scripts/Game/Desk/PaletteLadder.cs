// HR//0D -- the palette as RULES, for the light passes at the desk.
// Assembly: HRZeroDay.Game
//
// WHY THIS EXISTS (v120)
//   `MenuView` walks a pixel one step up its own material to light it. It can do that because the MENU is
//   drawn by code, so it knows every colour it has used. The DESK is a baked PNG: by the time it reaches
//   the renderer it is a bag of RGB values, and "which of these is one step brighter than that one" has to
//   be recovered from the palette itself.
//
//   tools/art/art_palette.py is that authority on the art side, and `--check` is its gate. This file is the
//   runtime mirror of it: it reads the PUBLISHED palette (Resources/palette.json, written by the art
//   pipeline) and builds the same ramp ladder. Two consequences worth stating plainly:
//
//     * nothing here is a colour literal. If the palette changes, this changes with it, in the same commit,
//       because it is data.
//     * the ladder is built from the palette's own NAMES the way the art side builds it: a step is
//       `material_N`, and material_N -> material_N+1. That relationship is authored, not guessed from
//       luminance -- guessing is how a cold grey gets "lit" into a warm cream.
//
//   The palette list is also the whitelist: a pixel that is not in it has no step, so an off-palette pixel
//   (there are none in a correct build; the palette gate enforces it) is simply never touched by light.
using System.Collections.Generic;
using UnityEngine;

namespace HRZeroDay.Game
{
    public static class PaletteLadder
    {
        private static Dictionary<Color32, Color32> _up;
        private static Dictionary<Color32, Color32> _down;
        private static Dictionary<string, Color32> _byKey;

        /// <summary>colour -> one step brighter in the same material. Null when the palette is unreadable.</summary>
        public static Dictionary<Color32, Color32> Up { get { Ensure(); return _up; } }

        /// <summary>colour -> one step darker in the same material: the shadow side of a form.</summary>
        public static Dictionary<Color32, Color32> Down { get { Ensure(); return _down; } }

        /// <summary>palette key -> its colour. Used by the light passes to name what they paint.</summary>
        public static Dictionary<string, Color32> ByKey { get { Ensure(); return _byKey; } }

        private static void Ensure()
        {
            if (_up != null) return;
            _up = new Dictionary<Color32, Color32>();
            _down = new Dictionary<Color32, Color32>();
            _byKey = new Dictionary<string, Color32>();

            // ONE READER, NOT TWO (v120). MonitorScreenView.Palette already owns "read the published palette
            // json", including the fact that it ships as a LIST (JsonUtility cannot parse a bare array into a
            // dictionary). The light passes consume that; they do not parse the file a second time, because
            // two parsers of one file is the bug that costs a day.
            _byKey = MonitorScreenView.Palette.FromResources();
            if (_byKey.Count == 0)
            {
                Debug.LogWarning("[HR//0D] palette.json is missing from Resources: the light passes will do " +
                                 "nothing. Run HR//0D > Import Art Kit.");
                return;
            }
            // the ladder: `name_N` -> `name_N+1`, which is exactly what art_palette.py's RAMP holds. A name
            // that does not parse (brass, stamp, paper) simply has no step in that direction.
            foreach (var pair in _byKey)
            {
                int cut = pair.Key.LastIndexOf('_');
                if (cut <= 0 || cut == pair.Key.Length - 1) continue;
                int n;
                if (!int.TryParse(pair.Key.Substring(cut + 1), out n)) continue;
                Color32 brighter;
                if (!_byKey.TryGetValue(pair.Key.Substring(0, cut) + "_" + (n + 1), out brighter)) continue;
                _up[pair.Value] = brighter;
                _down[brighter] = pair.Value;
            }
        }

    }
}
