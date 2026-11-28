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
using HRZeroDay.Core.Rules;
using UnityEngine;

namespace HRZeroDay.Game
{
    public static class PaletteLadder
    {
        private static Dictionary<Color32, Color32> _up;
        private static Dictionary<Color32, Color32> _down;
        private static Dictionary<string, Color32> _byKey;

        /// <summary>
        /// colour -> one step brighter in the same material.
        ///
        /// IT IS A CHAIN, NOT A PAIR (v120, second pass). The first version read `material_N -> material_N+1`
        /// straight out of the published palette, which is right -- but MenuView's light does not walk the
        /// palette, it walks `MenuLayout.Ramps`, a table of ONE-STEP PAIRS (wood_1->wood_2, wood_2->wood_3,
        /// ...). Following the pairs and joining them gives every material a whole ladder, which is what the
        /// menu's picture is actually made of: a warm wood that can climb four steps toward the lamp instead of
        /// stopping at the second one. Without the chain the desk's light could reach `wood_2` and no further,
        /// so lighting it looked like nothing happened -- the "the desk does not look like the menu" report.
        /// </summary>
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
            // THE MENU'S OWN TABLE FIRST. MenuLayout.Ramps is the authored list of one-step pairs the front
            // door's light walks, so using it means the desk and the menu climb the SAME ladder -- which is
            // the whole point of the round. Each pair is added, and then followed to build the chain, so a
            // material with four authored steps gets four steps here too.
            for (int i = 0; i < MenuLayout.Ramps.GetLength(0); i++)
            {
                Color32 from, to;
                if (!_byKey.TryGetValue(MenuLayout.Ramps[i, 0], out from)) continue;
                if (!_byKey.TryGetValue(MenuLayout.Ramps[i, 1], out to)) continue;
                if (!_up.ContainsKey(from)) _up[from] = to;
                if (!_down.ContainsKey(to)) _down[to] = from;
            }

            // then anything the table does not name, by the palette's own `material_N` naming, so a material
            // added to the palette before it is added to the menu still lights correctly.
            foreach (var pair in _byKey)
            {
                int cut = pair.Key.LastIndexOf('_');
                if (cut <= 0 || cut == pair.Key.Length - 1) continue;
                int n;
                if (!int.TryParse(pair.Key.Substring(cut + 1), out n)) continue;
                Color32 brighter;
                if (!_byKey.TryGetValue(pair.Key.Substring(0, cut) + "_" + (n + 1), out brighter)) continue;
                if (!_up.ContainsKey(pair.Value)) _up[pair.Value] = brighter;
                if (!_down.ContainsKey(brighter)) _down[brighter] = pair.Value;
            }
        }

    }
}
