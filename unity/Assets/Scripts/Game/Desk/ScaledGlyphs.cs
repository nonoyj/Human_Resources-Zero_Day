// HR//0D -- ScaledGlyphs: the 5x7 atlas at 2x, 3x or 4x, for the front door's title.
// Assembly: HRZeroDay.Game  (UnityEngine)
//
// A 5 px glyph cannot carry a title: "HUMAN RESOURCES" at 1x is 103 px wide, a caption. The concept's
// title is a third of the screen, so this wrapper repeats each lit atlas pixel into a scale x scale block.
// Wrapping the real source means the 3x title and the 1x rows come from the SAME atlas -- a second font
// would be a second look, and the game only owns one.
using System;
using HRZeroDay.Core.Rules;
using UnityEngine;
using HRZeroDay.Game;

namespace HRZeroDay.Game.Desk
{
    public sealed class ScaledGlyphs : IGlyphSource
    {
        private readonly IGlyphSource _inner;
        private readonly int _scale;

        public ScaledGlyphs(IGlyphSource inner, int scale)
        {
            if (inner == null) throw new ArgumentNullException(nameof(inner));
            _inner = inner;
            _scale = scale < 1 ? 1 : scale;
        }

        public int CellW { get { return _inner.CellW; } }
        public int CellH { get { return _inner.CellH; } }

        public int Scale { get { return _scale; } }
        public int ScaledGlyphW { get { return _inner.CellW * _scale; } }
        public int ScaledGlyphH { get { return _inner.CellH * _scale; } }

        public bool InkAt(char ch, int gx, int gy)
        {
            if (gx < 0 || gy < 0) return false;
            return _inner.InkAt(ch, gx / _scale, gy / _scale);
        }

        public Color32 Colour(string key) { return _inner.Colour(key); }

        /// <summary>Width in px of `text` at this scale, one trailing letter gap removed.</summary>
        public int WidthOf(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            return text.Length * (ScaledGlyphW + MenuLayout.LetterGap * _scale) - MenuLayout.LetterGap * _scale;
        }
    }
}
