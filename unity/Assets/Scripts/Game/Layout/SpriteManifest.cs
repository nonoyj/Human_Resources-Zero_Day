// HR//0D -- sprite lookup for the applicant across the table.
// Assembly: HRZeroDay.Game
// Owner: Dev A. Reads the manifest the art pipeline generated, so a sprite that exists on disk can
// never be "missing" at runtime without a clear message.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HRZeroDay.Game
{
    [Serializable]
    /// <summary>One named frame inside a Multiple-mode strip (from unity_sprites.json).</summary>
    public class SpriteFrameRect
    {
        public string name;
        public int x, y, width, height;
    }

    [Serializable]
    public class SpriteEntry
    {
        public string source;        // art/kit/...  (repo path, used by tools)
        public string assetPath;     // Assets/Art/Kit/...  (Unity path)
        public int width;
        public int height;
        public int ppu;
        public string spriteMode;    // Single | Multiple
        public string kind;          // applicant | portrait | ui | icon | stamp | desk | background
        public string sha256_16;
        public List<SpriteFrameRect> sprites;   // Multiple-mode frames, in reading order

        public string FileName
        {
            get
            {
                int i = assetPath.LastIndexOf('/');
                return i < 0 ? assetPath : assetPath.Substring(i + 1);
            }
        }

        public string FileNameNoExt
        {
            get
            {
                var n = FileName;
                int dot = n.LastIndexOf('.');
                return dot < 0 ? n : n.Substring(0, dot);
            }
        }
    }

    [Serializable]
    public class SpriteManifest
    {
        public int manifestVersion;
        public List<SpriteEntry> sprites = new List<SpriteEntry>();

        private Dictionary<string, SpriteEntry> _bySource;

        public static SpriteManifest Load()
        {
            var asset = Resources.Load<TextAsset>("unity_sprites");
            if (asset == null)
                throw new InvalidOperationException(
                    "Resources/unity_sprites.json is missing. Run: python tools/art/publish_to_unity.py");
            return JsonUtility.FromJson<SpriteManifest>(asset.text);
        }

        public SpriteEntry Find(string sourceOrAssetPath)
        {
            if (_bySource == null)
            {
                _bySource = new Dictionary<string, SpriteEntry>(StringComparer.Ordinal);
                foreach (var s in sprites) _bySource[s.source] = s;
            }
            SpriteEntry hit;
            if (_bySource.TryGetValue(sourceOrAssetPath, out hit)) return hit;
            for (int i = 0; i < sprites.Count; i++)
                if (sprites[i].assetPath == sourceOrAssetPath) return sprites[i];
            throw new ArgumentException("sprite not in manifest: " + sourceOrAssetPath);
        }

        public IEnumerable<SpriteEntry> OfKind(string kind)
        {
            return sprites.Where(s => s.kind == kind);
        }
    }
}
