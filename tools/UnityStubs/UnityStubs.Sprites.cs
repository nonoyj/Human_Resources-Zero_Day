// Stubs for the sprite-slicing API Unity 6 requires (UnityEditor.U2D.Sprites).
//
// WHY THIS IS A SEPARATE FILE: it is deliberately NOT part of TextureImporter. Unity 6.3 removed
// TextureImporter.spritesheet -- it still compiles (with CS0618) and then does nothing. Our importer
// used it and silently produced zero slices, which surfaced as "expected 4 sliced frames" in the scene
// builder on the user's machine. A stub that still exposes spritesheet would let that ship again, so
// the old API is deleted from UnityStubs.cs and only the new one exists here.
//
// Compile-check only: never shipped into the game.
using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor.U2D.Sprites
{
    public class SpriteDataProviderFactories
    {
        public void Init() { }
        public ISpriteEditorDataProvider GetSpriteEditorDataProviderFromObject(object target) { return null; }
    }

    public interface ISpriteEditorDataProvider
    {
        void InitSpriteEditorDataProvider();
        void Apply();
        SpriteRect[] GetSpriteRects();
        void SetSpriteRects(SpriteRect[] rects);
        T GetDataProvider<T>() where T : class;
    }

    public interface ISpriteNameFileIdDataProvider
    {
        void SetNameFileIdPairs(IList<SpriteNameFileIdPair> pairs);
    }

    public class SpriteRect
    {
        public string name;
        public GUID spriteID;
        public Rect rect;
        public SpriteAlignment alignment;
        public Vector2 pivot;
        public Vector4 border;
    }

    public class SpriteNameFileIdPair
    {
        public SpriteNameFileIdPair(string name, GUID id) { }
    }

    public struct GUID
    {
        public static GUID Generate() { return new GUID(); }
    }
}
